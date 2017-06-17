using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

            bool bResult = PrintWindow(hwnd, hdcBitmap, 0);
            if (!bResult)
                throw new Exception("PrintWindow() failed");

            gfxBmp.ReleaseHdc(hdcBitmap);
            gfxBmp.Dispose();

            return bmp;
        }

        public static Bitmap PrintScreen()
        {
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);
            return bmpScreenshot;
        }

        public IntPtr hWnd;
        private Bitmap screenshot;
        // a history of screenshot times for interpolation purposes
        public List<DateTime> timestamp_history = new List<DateTime>() { DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow };
        // keeps a copy of the screenshot so we only get it once and only as often as we need it
        public Bitmap bitmap
        {
            get
            {
                if (screenshot == null)
                {
                    timestamp_history.Insert(0, DateTime.UtcNow);
                    timestamp_history.RemoveAt(5);
                    if (Properties.Settings.Default.ScreenshotWindow)
                        screenshot = PrintWindow(hWnd);
                    else
                        screenshot = PrintScreen();

                    if (Math.Abs(screenshot.Height - 1080) > 10)
                        throw new ArgumentException("Error: screenshot resultion wrong");
                }
                return screenshot;
            }
        }

        /// <summary>
        /// Crop out the central region of the screen (convenience function)
        /// </summary>
        /// <param name="diameter"></param>
        /// <returns></returns>
        public Mat ScreenCentre(int diameter)
        {
            Bitmap s = bitmap;
            OpenCvSharp.Point centre = new OpenCvSharp.Point(s.Width, s.Height);
            Rectangle screenCentre = new Rectangle((int)((s.Width - diameter) * 0.5), (int)((s.Height - diameter) * 0.5), diameter, diameter);
            Bitmap image = CompassSensor.Crop(s, screenCentre);
            return BitmapConverter.ToMat(image);
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