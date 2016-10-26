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
                minDist: 100, /* set this high so that we only find one (also seems to improve accuracy) */
                param1: 100, /* default was fine after experimentation */
                param2: 13, /* required quality factor. 9 finds too many, 14 finds too few */
                minRadius: 40,
                maxRadius: 47);

            Point2f shipPointer = FindShipPointer(valueChannel);


            // draw some debug stuff for display: found circles, line to shippointer.
            foreach (CircleSegment circle in circles)
                valueChannel.Circle(circle.Center, (int)circle.Radius, 128);
            if (circles.Length == 1) 
                valueChannel.Line(circles[0].Center, shipPointer, 255);
            debugWindow.Image = BitmapConverter.ToBitmap(valueChannel);

            if (circles.Length > 1)
                throw new Exception("Too many possible triquadrants.");
            if (circles.Length < 1)
                throw new Exception("No possible triquadrants.");
            
            return circles[0].Center - FindShipPointer(valueChannel);            
        }

        Mat templatepointer = new Mat("res3/squaretarget.png", ImreadModes.GrayScale);

        /// <summary>
        /// Find the little square dot that indicates where the nose of the ship is pointing.
        /// </summary>
        public Point2f FindShipPointer(Mat screen)
        {
            Mat matches = screen.MatchTemplate(templatepointer, TemplateMatchModes.CCoeffNormed);
            OpenCvSharp.Point minloc, maxloc;
            matches.MinMaxLoc(out minloc, out maxloc);
            return maxloc + new OpenCvSharp.Point(templatepointer.Size().Width, templatepointer.Size().Height) * 0.5;            
        }
                
        Mat templatesaf = new Mat("res3/safdisengag250.png", ImreadModes.GrayScale);
        
        public bool MatchSafDisengag()
        {
            // MatchTemplate doesn't allow for scaling / rotation. Allow more leeway by reducing resolution?

            Bitmap image = CompassSensor.Crop(screen.bitmap, new Rectangle(800, 650, 300, 200));
            Mat source = BitmapConverter.ToMat(image);            
            Mat blues = source.Split()[0];
            Mat clean = blues.EmptyClone();
            clean.SetTo(0); // make sure the matrix is blank.            
            blues.CopyTo(clean, blues.InRange(250, 255));            
            Mat matches = clean.MatchTemplate(templatesaf, TemplateMatchModes.CCoeffNormed);
            double minVal, maxVal;
            matches.MinMaxLoc(out minVal, out maxVal);
            
            return maxVal > 0.5; // see experiments
        }
    }
}
