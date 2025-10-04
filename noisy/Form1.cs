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
        public Form1()
        {
            InitializeComponent();
        }

        public static void Shader1_2()
        {
            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // Aggressive downscale for speed. Bigger = faster, lower quality.
            const int scale = 1; // try 4/6/8. 6 gives large speedup.
            int w = Math.Max(1, screenW / scale);
            int h = Math.Max(1, screenH / scale);

            int sizeSmall = w * h;
            int bytesSmall = sizeSmall * 3;

            // Parallel options
            var pOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            // Thread-local random for safe parallel randoms
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
                    // Capture scaled-down screen into the small DIB by stretching the desktop into mdc
                    // StretchBlt from hdc (screen) to mdc (small DIB)
                    StretchBlt(mdc, 0, 0, w, h, hdc, 0, 0, screenW, screenH, SRCCOPY);

                    // Copy small DIB into managed array
                    Marshal.Copy(ppvBits, rgbArray, 0, bytesSmall);

                    // Parallel noise modification — process by rows to improve locality
                    Parallel.For(0, h, pOptions, y =>
                    {
                        var rnd = threadRand.Value;
                        int rowStart = y * w * 3;
                        // Very low-quality: modify every pixel but minimal math to be fast
                        for (int xPos = 0; xPos < w; xPos++)
                        {
                            int idx = rowStart + xPos * 3;
                            // quick random-ish changes; clamp naturally by byte overflow (wrap is acceptable)
                            rgbArray[idx + 0] += (byte)rnd.Next(-3, 5);
                            rgbArray[idx + 1] += (byte)rnd.Next(-4, 4);
                            rgbArray[idx + 2] += (byte)rnd.Next(-5, 3);
                        }
                    });

                    // Copy back to the small DIB
                    Marshal.Copy(rgbArray, 0, ppvBits, bytesSmall);

                    // Stretch the small DIB to full screen quickly
                    StretchBlt(hdc, 4, 4, screenW-10, screenH-20, mdc, 0, 0, w, h, SRCCOPY);

                    // No sleep: real-time as fast as possible. This will use a lot of CPU/GPU.
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
            // Start the shader on a dedicated background thread (long-running) to avoid threadpool starvation
            Task.Factory.StartNew(() => Shader1_2(), TaskCreationOptions.LongRunning);
        }
    }
}
