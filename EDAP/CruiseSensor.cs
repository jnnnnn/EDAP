using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;

namespace EDAP
{
    class CruiseSensor
    {
        public static Point2f? FindTriQuadrant(Bitmap screen)
        {
            // todo: detect the dotted circle that means the target is obscured.

            // See the Experiments for how this works.
            Mat source = BitmapConverter.ToMat(screen);
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 10, 200, 128 }), InputArray.Create(new int[] { 27, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);
            CircleSegment[] circles = sourceHSVFiltered.Split()[2].HoughCircles(
                HoughMethods.Gradient,
                dp: 1f, 
                minDist: 20, 
                param1: 100, 
                param2: 13, 
                minRadius: 45,
                maxRadius: 47);

            if (circles.Length == 1)
                return circles[0].Center - new Point2f(screen.Width / 2, screen.Height / 2);
            else
                return null;
        }
    }
}
