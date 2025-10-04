using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using static Windows.APIs;

namespace noisy
{
    public partial class Form1 : Form
    {
        // --- Novos membros para hotkey global / cancelamento ---
        private CancellationTokenSource _cts;
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        // Hotkey configurável aqui: Ctrl + Alt + Q
        // Altere vkTrigger para outra tecla (usando System.Windows.Forms.Keys) se desejar.
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_SHIFT = 0x10;
        private const int VK_Q = (int)Keys.Q;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        // -------------------------------------------------------

        public Form1()
        {
            InitializeComponent();
        }

        public static void Shader1_2(CancellationToken token)
        {
            // Escolhas aleatórias por execução (ajustadas para serem pequenas e realistas
            // em relação aos valores hardcoded originais)
            var startRnd = new Random(unchecked(Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId));

            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // scale aleatório pequeno (1 ou 2). O código original usava 1 por padrão.
            int scale = startRnd.Next(1, 3); // 1..2

            int w = Math.Max(1, screenW / scale);
            int h = Math.Max(1, screenH / scale);

            int sizeSmall = w * h;
            int bytesSmall = sizeSmall * 3;

            // Escolhe offsets/margens aleatórios pequenos para o StretchBlt de destino
            // Valores próximos ao original (ex: deslocamentos de poucos pixels e pequenas
            // reduções de largura/altura como -10 / -20).
            int destX = startRnd.Next(0, Math.Min(6, screenW / 10)); // 0..5
            int destY = startRnd.Next(0, Math.Min(6, screenH / 10)); // 0..5

            // Pequenas reduções fixas na largura/altura para simular os -10 / -20 originais
            int shrinkW = startRnd.Next(4, 12);  // 4..11 (aprox equivalente ao -10 original)
            int shrinkH = startRnd.Next(10, 26); // 10..25 (aprox equivalente ao -20 original)

            int destW = Math.Max(1, screenW - shrinkW - destX);
            int destH = Math.Max(1, screenH - shrinkH - destY);

            // Amplitudes de ruído por canal próximas aos valores originais:
            // original: B: rnd.Next(-3,5)  G: rnd.Next(-4,4)  R: rnd.Next(-5,3)
            // aqui escolhemos pequenas variações por execução, mantendo a mesma ordem de magnitude
            int bRangeLow = -startRnd.Next(1, 4);   // -1 .. -3
            int bRangeHigh = startRnd.Next(3, 6);   // 3  .. 5

            int gRangeLow = -startRnd.Next(2, 5);   // -2 .. -4
            int gRangeHigh = startRnd.Next(2, 4);   // 2  .. 3

            int rRangeLow = -startRnd.Next(3, 6);   // -3 .. -5
            int rRangeHigh = startRnd.Next(1, 4);   // 1  .. 3

            // Parallel options
            var pOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            // Thread-local random para segurança em paralelo
            var threadRand = new ThreadLocal<Random>(() => new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

            IntPtr hdc = IntPtr.Zero;
            IntPtr mdc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldObject = IntPtr.Zero;
            IntPtr ppvBits = IntPtr.Zero;

            // Reuse buffer
            byte[] rgbArray = new byte[bytesSmall];

            try
            {
                hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return;

                mdc = CreateCompatibleDC(hdc);
                if (mdc == IntPtr.Zero) return;

                // Prepare DIB at lower resolution (3 bytes per pixel BGR)
                BITMAPINFO bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                        biWidth = w,
                        biHeight = -h, // top-down
                        biPlanes = 1,
                        biBitCount = 24,
                        biCompression = 0 //RGB
                    },
                    bmiColors = new RGBQUAD[1]
                };

                bitmap = CreateDIBSection(hdc, ref bmi, 0, out ppvBits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero || ppvBits == IntPtr.Zero) return;

                oldObject = SelectObject(mdc, bitmap);

                // Use fast nearest-neighbor scaling when stretching
                SetStretchBltMode(hdc, StretchBltMode.DELETESCANS);

                // Main loop: capture small, noisy in parallel, stretch to full screen
                while (true)
                {
                    if (token.IsCancellationRequested) break;

                    // Capture scaled-down screen into the small DIB by stretching the desktop into mdc
                    // StretchBlt from hdc (screen) to mdc (small DIB)
                    StretchBlt(mdc, 0, 0, w, h, hdc, 0, 0, screenW, screenH, SRCCOPY);

                    // Copy small DIB into managed array
                    Marshal.Copy(ppvBits, rgbArray, 0, bytesSmall);

                    // Parallel noise modification — process por linhas para melhor localidade
                    Parallel.For(0, h, pOptions, y =>
                    {
                        var rnd = threadRand.Value;
                        int rowStart = y * w * 3;
                        for (int xPos = 0; xPos < w; xPos++)
                        {
                            int idx = rowStart + xPos * 3;
                            // aplicar ruído com amplitudes escolhidas no início
                            rgbArray[idx + 0] = (byte)(rgbArray[idx + 0] + rnd.Next(bRangeLow, bRangeHigh)); // B
                            rgbArray[idx + 1] = (byte)(rgbArray[idx + 1] + rnd.Next(gRangeLow, gRangeHigh)); // G
                            rgbArray[idx + 2] = (byte)(rgbArray[idx + 2] + rnd.Next(rRangeLow, rRangeHigh)); // R
                        }
                    });

                    // Copy back to the small DIB
                    Marshal.Copy(rgbArray, 0, ppvBits, bytesSmall);

                    // Stretch the small DIB to full screen rapidamente usando valores aleatórios por execução,
                    // mas próximos aos offsets/margens originais
                    StretchBlt(hdc, destX, destY, destW, destH, mdc, 0, 0, w, h, SRCCOPY);

                    // Sem sleep: tempo-real o mais rápido possível
                }
            }
            finally
            {
                if (mdc != IntPtr.Zero && oldObject != IntPtr.Zero)
                    SelectObject(mdc, oldObject);

                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);

                if (mdc != IntPtr.Zero)
                    DeleteDC(mdc);

                if (hdc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, hdc);

                threadRand.Dispose();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Cria token de cancelamento para encerrar o shader de forma limpa
            _cts = new CancellationTokenSource();

            // Start the shader on a dedicated background thread (long-running) to avoid threadpool starvation
            Task.Factory.StartNew(() => Shader1_2(_cts.Token), TaskCreationOptions.LongRunning);

            // Instala hook global de teclado
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cancelar shader e remover hook
            try
            {
                _cts?.Cancel();
            }
            catch { }

            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            base.OnFormClosing(e);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            IntPtr moduleHandle = IntPtr.Zero;
            try
            {
                var curProcess = System.Diagnostics.Process.GetCurrentProcess();
                var curModule = curProcess.MainModule;
                moduleHandle = GetModuleHandle(curModule.ModuleName);
            }
            catch
            {
                // fallback: NULL module handle (funciona na maioria dos cenários para LL hook)
                moduleHandle = IntPtr.Zero;
            }

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    // Detecta Ctrl + Alt + Q (modifique as checagens conforme desejar)
                    bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                    // bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

                    if (vkCode == VK_Q && ctrlDown && altDown)
                    {
                        // Gatilho: cancela o shader e encerra a aplicação
                        try
                        {
                            _cts?.Cancel();
                        }
                        catch { }

                        // Remover hook antes de sair
                        if (_hookID != IntPtr.Zero)
                        {
                            UnhookWindowsHookEx(_hookID);
                            _hookID = IntPtr.Zero;
                        }

                        // Solicita fechamento da UI thread de forma segura
                        try
                        {
                            if (this.IsHandleCreated)
                                this.BeginInvoke((Action)(() => Application.Exit()));
                            else
                                Environment.Exit(0);
                        }
                        catch
                        {
                            Environment.Exit(0);
                        }
                    }
                }
            }
            catch
            {
                // não propagar exceções do hook
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}