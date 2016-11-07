using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EDAP
{
    class CruiseSensor
    {
        public Screenshot screen;

        public PictureBox debugWindow;
        
        /// <summary>
        /// Filter the given image to select certain yellow hues (returned as grayscale)
        /// </summary>
        public Mat IsolateYellow(Mat source)
        {
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 10, 200, 128 }), InputArray.Create(new int[] { 27, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);
            Mat valueChannel = sourceHSVFiltered.Split()[2];
            return valueChannel;
        }

        public Point2f FindTriQuadrant(Bitmap screen)
        {
            // todo: detect the dotted circle that means the target is obscured.

            // See the Experiments for how this works.
            Mat yellowValue = IsolateYellow(BitmapConverter.ToMat(screen));

            CircleSegment[] circles = yellowValue.HoughCircles(
                HoughMethods.Gradient,
                dp: 1f, /* resolution scaling factor?  full resolution seems to work better */
                minDist: 100, /* set this high so that we only find one (also seems to improve accuracy) */
                param1: 100, /* default was fine after experimentation */
                param2: 13, /* required quality factor. 9 finds too many, 14 finds too few */
                minRadius: 40,
                maxRadius: 47);

            Point2f shipPointer = FindShipPointer(yellowValue);
            
            // draw some debug stuff for display: found circles, line to shippointer.
            foreach (CircleSegment circle in circles)
                yellowValue.Circle(circle.Center, (int)circle.Radius, 128);
            if (circles.Length == 1)
                yellowValue.Line(circles[0].Center, shipPointer, 255);
            debugWindow.Image = BitmapConverter.ToBitmap(yellowValue);

            if (circles.Length > 1)
                throw new Exception("Too many possible triquadrants.");
            if (circles.Length < 1)
                throw new Exception("No possible triquadrants.");
            
            return circles[0].Center - FindShipPointer(yellowValue);            
        }

        Mat templatepointer = new Mat("res3/squaretarget.png", ImreadModes.GrayScale);

        /// <summary>
        /// Find the little square dot that indicates where the nose of the ship is pointing.
        /// </summary>
        public Point2f FindShipPointer(Mat yellowValue)
        {
            Mat matches = yellowValue.MatchTemplate(templatepointer, TemplateMatchModes.CCoeffNormed);
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
            
            return maxVal > 0.4; // see experiments, MatchSafDisengag2
        }

        /// <summary>
        /// Try to match (part of) a large green circle on the screen.
        /// </summary>
        public CircleSegment FindCorona()
        {
            // see the Experiments for how this works
            Bitmap cropped = CompassSensor.Crop(screen.bitmap,
                screen.bitmap.Width * 1 / 3,
                screen.bitmap.Height * 1 / 3,
                screen.bitmap.Width * 2 / 3,
                screen.bitmap.Height * 2 / 3);
            Mat screenwhole = BitmapConverter.ToMat(cropped);

            Point2f ShipPointerOffset = new Point2f(0, 0);
            try
            {
                ShipPointerOffset = FindShipPointer(IsolateYellow(screenwhole));
            }
            catch (Exception)
            {
                // If we can't find the ship pointer (it's hard to see it against the sun) then use the middle of the screen.
            }

            // erase the vivid areas, otherwise the blur subtraction turns yellow near red to green
            Mat brightHSV = screenwhole.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat darkAreasMask = brightHSV.InRange(InputArray.Create(new int[] { 0, 0, 0 }), InputArray.Create(new int[] { 180, 255, 180 }));
            Mat darkAreas = new Mat();
            screenwhole.CopyTo(darkAreas, darkAreasMask);

            Mat screenblur = darkAreas - darkAreas.Blur(new OpenCvSharp.Size(10, 10));
            Mat sourceHSV = screenblur.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 35, 204, 20 }), InputArray.Create(new int[] { 90, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);
            Mat sourceGrey = sourceHSVFiltered.Split()[2].InRange(32, 256);
            LineSegmentPoint[] result = sourceGrey.HoughLinesP(1, 3.1415 / 180, 5, 10, 2);
            List<Point2d> points = new List<Point2d>();
            foreach (var line in result)
            {
                points.Add(line.P1);
                points.Add(line.P2);
            }
            if (points.Count < 8)
                throw new ArgumentException("Not enough points in corona circle");
            CircleSegment c = ComputeCircle(points);
            sourceGrey.Line(c.Center, ShipPointerOffset, 255);
            c.Center -= ShipPointerOffset; // adjust for camera movement by taking ship pointer offset
            sourceGrey.Circle(c.Center, (int)c.Radius, 255);
            debugWindow.Image = BitmapConverter.ToBitmap(sourceGrey);
            return c;
        }

        /// <summary>
        /// Given a list of points, find a circle that roughly passes through them all.
        /// </summary>
        public static CircleSegment ComputeCircle(System.Collections.Generic.IEnumerable<Point2d> l)
        {
            // https://www.scribd.com/document/14819165/Regressions-coniques-quadriques-circulaire-spherique
            // via http://math.stackexchange.com/questions/662634/find-the-approximate-center-of-a-circle-passing-through-more-than-three-points

            var n = l.Count();
            var sumx = l.Sum(p => p.X);
            var sumxx = l.Sum(p => p.X * p.X);
            var sumy = l.Sum(p => p.Y);
            var sumyy = l.Sum(p => p.Y * p.Y);

            var d11 = n * l.Sum(p => p.X * p.Y) - sumx * sumy;

            var d20 = n * sumxx - sumx * sumx;
            var d02 = n * sumyy - sumy * sumy;

            var d30 = n * l.Sum(p => p.X * p.X * p.X) - sumxx * sumx;
            var d03 = n * l.Sum(p => p.Y * p.Y * p.Y) - sumyy * sumy;

            var d21 = n * l.Sum(p => p.X * p.X * p.Y) - sumxx * sumy;
            var d12 = n * l.Sum(p => p.Y * p.Y * p.X) - sumyy * sumx;

            var x = ((d30 + d12) * d02 - (d03 + d21) * d11) / (2 * (d20 * d02 - d11 * d11));
            var y = ((d03 + d21) * d20 - (d30 + d12) * d11) / (2 * (d20 * d02 - d11 * d11));

            var c = (sumxx + sumyy - 2 * x * sumx - 2 * y * sumy) / n;
            var r = Math.Sqrt(c + x * x + y * y);

            return new CircleSegment(new Point2f((float)x, (float)y), (float)r);
        }

    }
}
