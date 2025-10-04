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

            while (true)
            {

                IntPtr hdc = GetDC(IntPtr.Zero);
                IntPtr mdc = CreateCompatibleDC(hdc);

                BITMAPINFO bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                        biWidth = x,
                        biHeight = -y,
                        biPlanes = 1,
                        biBitCount = 24,
                        biCompression = 0
                    },
                    bmiColors = new RGBQUAD[256] // Alterado de RGBTRIPLE para RGBQUAD
                };

                IntPtr bitmap = CreateDIBSection(hdc, ref bmi, 0, out IntPtr ppvBits, IntPtr.Zero, 0);
                IntPtr oldObject = SelectObject(mdc, bitmap);

                Random rand = new Random();

                BitBlt(mdc, 0, 0, x, y, hdc, 0, 0, SRCCOPY);
                byte[] rgbArray = new byte[size * 3]; // Each RGBTRIPLE has 3 bytes
                Marshal.Copy(ppvBits, rgbArray, 0, rgbArray.Length);
                for (int i = 8; i < size; i++)
                {
                    rgbArray[i * 3 + 2] = (byte)(rgbArray[i * 3 + 2] + rand.Next(2));
                    rgbArray[i * 3 + 1] = (byte)(rgbArray[i * 3 + 1] + rand.Next(5));
                    rgbArray[i * 3 + 0] = (byte)(rgbArray[i * 3 + 0] + rand.Next(6));


                }
                Marshal.Copy(rgbArray, 0, ppvBits, rgbArray.Length);
                BitBlt(hdc, 1, 1, x, y, mdc, 0, 0, SRCCOPY);
                //Thread.Sleep(5);


                SelectObject(mdc, oldObject);
                ReleaseDC(IntPtr.Zero, hdc);
                DeleteObject(bitmap);
                DeleteDC(hdc);
                DeleteDC(mdc);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Shader1_2();
        }
    }
}
