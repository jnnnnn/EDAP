using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace EDAP
{
    class CruiseSensor
    {
        public Screenshot screen;

        public PictureBox debugWindow;
        public Point2f FindTriQuadrant(Bitmap screen)
        {
            // todo: detect the dotted circle that means the target is obscured.

            // See the Experiments for how this works.
            Mat source = BitmapConverter.ToMat(screen);
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 10, 200, 128 }), InputArray.Create(new int[] { 27, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);            
            Mat valueChannel = sourceHSVFiltered.Split()[2];
            CircleSegment[] circles = valueChannel.HoughCircles(
                HoughMethods.Gradient,
                dp: 1f, /* resolution scaling factor?  full resolution seems to work better */
                minDist: 100, /* if we find more than one then we go to the second analysis, the crosshair is probably blue as well*/
                param1: 100, /* default was fine after experimentation */
                param2: 13, /* required quality factor. 9 finds too many, 14 finds too few */
                minRadius: 40,
                maxRadius: 47);
            
            foreach (CircleSegment circle in circles)
                valueChannel.Circle(circle.Center, (int)circle.Radius, 128);

            valueChannel.Line(screen.Height / 2, 0, screen.Height / 2, screen.Width, 255);
            valueChannel.Line(0, screen.Width / 2, screen.Height, screen.Width / 2, 255);

            debugWindow.Image = BitmapConverter.ToBitmap(valueChannel);

            if (circles.Length > 1)
                throw new Exception("Too many possible triquadrants.");
            if (circles.Length < 1)
                throw new Exception("No possible triquadrants.");
            
            return circles[0].Center - new Point2f(screen.Width / 2, screen.Height / 2);            
        }
        
        Mat templatesaf = new Mat("res3/safdisengag.png", ImreadModes.GrayScale);

        public bool MatchSafDisengag()
        {
            // MatchTemplate doesn't allow for scaling / rotation. Allow more leeway by reducing resolution?

            Bitmap image = CompassRecognizer.Crop(screen.bitmap, new Rectangle(800, 700, 300, 100));
            Mat source = BitmapConverter.ToMat(image);
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);            
            Mat blues = source.Split()[0];
            Mat clean = new Mat(blues.Size(), blues.Type());
            blues.CopyTo(clean, blues.InRange(128, 255));
            Mat matches = clean.MatchTemplate(templatesaf, TemplateMatchModes.SqDiffNormed);
            double minVal, maxVal;
            matches.MinMaxLoc(out minVal, out maxVal);
            
            return minVal < 0.5; // for SqDiffNormed, perfect match 0.1; no match [0.99 .. 1.0].
        }
    }
}
