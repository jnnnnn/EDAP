using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EDAP
{
    // This class is to get the orientation of the ship relative to the target so that we can point at the target.
    class Compass
    {
        public struct Orientation
        {
            public int longitude;
            public int latitude;
            public Orientation(int longitude_a, int latitude_a)
            {
                longitude = longitude_a;
                latitude = latitude_a;
            }
        }

        public Orientation GetOrientation(Bitmap compassImage)
        {
            Rectangle bmpRec = new Rectangle(0, 0, compassImage.Width, compassImage.Height);
            BitmapData bmpData = compassImage.LockBits(
                bmpRec, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            IntPtr Pointer = bmpData.Scan0;
            int DataBytes = Math.Abs(bmpData.Stride) * compassImage.Height;
            byte[] rgbValues = new byte[DataBytes];
            Marshal.Copy(Pointer, rgbValues, 0, DataBytes);
            compassImage.UnlockBits(bmpData);

            return new Orientation(0,0);
        }

    }
}
