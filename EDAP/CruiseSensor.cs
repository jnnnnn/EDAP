using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;

namespace EDAP
{
    class CruiseSensor
    {
        public Screen screen;

        public static Point2f FindTriQuadrant(Bitmap screen)
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
            
            return minVal < 0.9; // for SqDiffNormed, perfect match 0.1; no match [0.99 .. 1.0].
        }
    }
}
