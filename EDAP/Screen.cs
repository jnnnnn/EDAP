using System;
using System.Drawing;

namespace EDAP
{
    public class Screen
    {
        public IntPtr hWnd;
        private Bitmap screenshot;
        // keeps a copy of the screenshot so we only get it once and only as often as we need it
        public Bitmap bitmap
        {
            get
            {
                if (screenshot == null)
                {
                    screenshot = Screenshot.PrintWindow(hWnd);

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
