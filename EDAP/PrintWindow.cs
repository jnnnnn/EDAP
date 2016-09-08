using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EDAP
{
    // from http://stackoverflow.com/a/911225/412529
    // We need this class to get a screenshot of the ED window so we can "look" at the compass to align the next jump
    public class Screenshot
    {
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        public static Bitmap PrintWindow(IntPtr hwnd)
        {
            RECT rc;
            GetWindowRect(hwnd, out rc);

            Bitmap bmp = new Bitmap(rc.Width, rc.Height, PixelFormat.Format24bppRgb);
            Graphics gfxBmp = Graphics.FromImage(bmp);
            IntPtr hdcBitmap = gfxBmp.GetHdc();

            PrintWindow(hwnd, hdcBitmap, 0);

            gfxBmp.ReleaseHdc(hdcBitmap);
            gfxBmp.Dispose();

            return bmp;
        }

        public IntPtr hWnd;
        private Bitmap screenshot;
        // keeps a copy of the screenshot so we only get it once and only as often as we need it
        public Bitmap bitmap
        {
            get
            {
                if (screenshot == null)
                {
                    screenshot = PrintWindow(hWnd);

                    if (Math.Abs(screenshot.Height - 1080) > 10)
                        throw new ArgumentException("Error: screenshot resultion wrong");
                }
                return screenshot;
            }
        }

        public void ClearSaved()
        {
            if (screenshot != null)
            {
                screenshot.Dispose();
                screenshot = null;
            }
        }
    }
}