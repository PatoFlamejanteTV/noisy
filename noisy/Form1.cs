using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static Windows.APIs;

namespace noisy
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public static void Shader1_2()
        {
            int x = GetSystemMetrics(SM_CXSCREEN);
            int y = GetSystemMetrics(SM_CYSCREEN);
            int size = x * y;
            int bytes = size * 3;

            // Qualidade vs velocidade: processa apenas 1 em 'step' pixels
            const int step = 4; // aumentar para menor qualidade e mais velocidade

            Random rand = new Random();

            IntPtr hdc = IntPtr.Zero;
            IntPtr mdc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldObject = IntPtr.Zero;
            IntPtr ppvBits = IntPtr.Zero;

            // Reutiliza buffer para evitar alocações por frame
            byte[] rgbArray = new byte[bytes];

            try
            {
                hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return;

                mdc = CreateCompatibleDC(hdc);
                if (mdc == IntPtr.Zero) return;

                BITMAPINFO bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                        biWidth = x,
                        biHeight = -y, // top-down
                        biPlanes = 1,
                        biBitCount = 24,
                        biCompression = 0
                    },
                    bmiColors = new RGBQUAD[1]
                };

                bitmap = CreateDIBSection(hdc, ref bmi, 0, out ppvBits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero || ppvBits == IntPtr.Zero) return;

                oldObject = SelectObject(mdc, bitmap);

                // Loop principal (infinito) - leve e rápido
                while (true)
                {
                    // Captura tela para o DIB
                    BitBlt(mdc, 0, 0, x, y, hdc, 0, 0, SRCCOPY);

                    // Copia para o buffer gerenciado (reutilizado)
                    Marshal.Copy(ppvBits, rgbArray, 0, bytes);

                    // Modifica apenas 1 em 'step' pixels (perda de qualidade intencional)
                    for (int i = 8; i < size; i += step)
                    {
                        int idx = i * 3;
                        // pequenas variações nos canais BGR
                        rgbArray[idx + 2] = (byte)(rgbArray[idx + 2] + rand.Next(2));
                        rgbArray[idx + 1] = (byte)(rgbArray[idx + 1] + rand.Next(5));
                        rgbArray[idx + 0] = (byte)(rgbArray[idx + 0] + rand.Next(6));
                    }

                    // Volta para o DIB e pinta a tela
                    Marshal.Copy(rgbArray, 0, ppvBits, bytes);
                    BitBlt(hdc, 1, 1, x, y, mdc, 0, 0, SRCCOPY);

                    // Pequena pausa para ceder CPU (mantém alto frame-rate sem travar completamente)
                    //System.Threading.Thread.Sleep(1);
                }
            }
            finally
            {
                // Restaura e libera recursos com segurança
                if (mdc != IntPtr.Zero && oldObject != IntPtr.Zero)
                    SelectObject(mdc, oldObject);

                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);

                if (mdc != IntPtr.Zero)
                    DeleteDC(mdc);

                if (hdc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Executa o shader em segundo plano para não bloquear a UI
            Task.Run(() => Shader1_2());
        }
    }
}
