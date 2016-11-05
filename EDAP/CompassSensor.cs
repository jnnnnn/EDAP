using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EDAP
{
    // This class is to get the orientation of the ship relative to the target so that we can point at the target.
    class CompassSensor
    {
        private PictureBox pictureBox2;
        
        private Mat template_open = new Mat("res3/target-open.png", ImreadModes.GrayScale);
        private Mat template_closed = new Mat("res3/target-closed.png", ImreadModes.GrayScale);
        private const float match_threshold = 0.7f;
        private Screenshot screen;
        public CompassSensor(Screenshot screen, PictureBox pictureBox2)
        {
            this.pictureBox2 = pictureBox2;
            this.screen = screen;
        }
        
        private int clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
        private int clamp(int value)
        {
            return clamp(value, 0, 255);
        }
        private void DrawRectangle(Bitmap image, Rectangle rect)
        {
            using (Graphics gr = Graphics.FromImage(image))
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen thick_pen = new Pen(Color.Blue, 2))
                {
                    gr.DrawRectangle(thick_pen, rect);
                }
            }
        }

        public static Bitmap Crop(Bitmap input, double x1, double y1, double x2, double y2)
        {
            Rectangle cropArea = new Rectangle((int)x1, (int)y1, (int)(x2-x1), (int)(y2-y1));
            return Crop(input, cropArea);
        }

        public static Bitmap Crop(Bitmap input, Rectangle cropArea)
        {
            if (!new Rectangle(0, 0, input.Width, input.Height).Contains(cropArea))
                throw new ArgumentException("Rectangle outside!");
            return input.Clone(cropArea, input.PixelFormat);
        }

        public static Mat Levels(Mat input, int channel = 0/*default blue of OpenCV's BGR format*/)
        {
            Mat[] channels = input.Split();
            Mat result = new Mat(channels[0].Size(), channels[0].Type());
            channels[channel].ConvertTo(result, -1, alpha: 2, beta: -255); // value -> value * 2 - 255
            return result;
        }

        /// <summary>
        /// Search for a red-channel compass-sized circle in the image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private CircleSegment FindCircle(Bitmap image)
        {
            Mat source = BitmapConverter.ToMat(image);
            //Convert input images to gray
            Mat reds = Levels(source, channel: 2);
            // these parameters were tuned using the test functionality
            CircleSegment[] circles = Cv2.HoughCircles(reds, 
                HoughMethods.Gradient, 
                dp:2f, // this was tuned by experimentation
                minDist: 500, // this is huge so we only find the best one
                param1: 200, // this was tuned by experimentation
                param2: 50, // this is quite low so we usually find something
                minRadius: 22, 
                maxRadius: 28);

            if (circles.Length != 1)
            {
                pictureBox2.Image = image;
                if (circles.Length > 1)
                    throw new System.ArgumentException("More than one valid circle...");
                if (circles.Length < 1)
                    throw new System.ArgumentException("No valid circles");
            }

            return circles[0];
        }

        /// <summary>
        /// Use template matching to find the small blue circle. HoughCircles didn't work because the dot/circle is too small.
        /// </summary>
        /// <param name="croppedCompass"></param>
        /// <returns>The normalized vector from the center of the compass to the blue dot (y values are [1..2] if dot is a circle)</returns>
        public Point2f FindTarget2(Bitmap croppedCompass)
        {
            Mat source = BitmapConverter.ToMat(croppedCompass);

            Mat[] channels = source.Split();
            Mat blues2 = channels[0];
            Mat clean = blues2.EmptyClone();
            clean.SetTo(0);
            blues2.CopyTo(clean, blues2.InRange(128, 255));

            double minval, maxval_closed, maxval_open;
            OpenCvSharp.Point minloc, maxloc_closed, maxloc_open;

            Mat result_closed = clean.EmptyClone();
            Cv2.MatchTemplate(clean, template_closed, result_closed, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result_closed, out minval, out maxval_closed, out minloc, out maxloc_closed);

            Mat result_open = clean.EmptyClone();
            Cv2.MatchTemplate(clean, template_open, result_open, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result_open, out minval, out maxval_open, out minloc, out maxloc_open);
            Graphics g = Graphics.FromImage(croppedCompass);
            if (maxval_open > maxval_closed && maxval_open > match_threshold)
            {
                CircleSegment c = FindCircle(croppedCompass);
                g.DrawLine(new Pen(Color.Green, 2), c.Center.X, c.Center.Y, maxloc_open.X + template_open.Width / 2, maxloc_open.Y + template_open.Height / 2);
                return ComputeVector(c, new OpenCvSharp.Point(maxloc_open.X + template_open.Size().Width / 2, maxloc_open.Y + template_open.Size().Height / 2), false);
            }
            else if (maxval_closed > match_threshold)
            {
                CircleSegment c = FindCircle(croppedCompass);
                g.DrawLine(new Pen(Color.Green, 1), c.Center.X, c.Center.Y, maxloc_closed.X + template_closed.Width / 2, maxloc_closed.Y + template_closed.Height / 2);
                return ComputeVector(c, new OpenCvSharp.Point(maxloc_closed.X + template_closed.Size().Width / 2, maxloc_closed.Y + template_closed.Size().Height / 2), true);
            }
            else
                throw new ArgumentException("Could not find target");
        }
        
        private Point2f ComputeVector(CircleSegment c, OpenCvSharp.Point target, bool forward)
        {
            double x = (target.X - c.Center.X);
            double y = (target.Y - c.Center.Y);
            var maxRadius = Math.Max(c.Radius, Math.Sqrt(x * x + y * y));
            x /= maxRadius;
            y /= maxRadius;

            // could return ship pitch and roll here ...
            /*
            var rollangle = Math.Atan2(x, -y); // wrong order on purpose so that up is 0 degrees roll.            
            var pitchangle = Math.Asin(Math.Sqrt(x*x + y*y) / maxRadius);
            if (!forward)
                pitchangle = Math.PI - pitchangle;
            return new PointF((float)pitchangle, (float)rollangle);
            */

            // but x/y is actually easier to handle since we are only doing a crude alignment, and not computing angular velocities or anything            
            if (!forward)
                y = (y > 0) ? 2-y : -2-y; // if target is behind, add lots of pitch offset so that exactly wrong direction is 2/-2.
            return new Point2f((float)x, (float)y);
        }

        public List<Point2f> history = new List<Point2f>(); // newest element at the front
                
        // returns the normalized vector from the compass center to the blue dot
        public Point2f GetOrientation()
        {
            try
            {
                Bitmap compassArea = Crop(screen.bitmap, 600, 550, 900, 1050);
                CircleSegment crosshair = FindCircle(compassArea);
                var r = crosshair.Radius + 12;
                var c = crosshair.Center;
                Rectangle compassRect = new Rectangle((int)(c.X - r), (int)(c.Y - r), (int)(r * 2), (int)(r * 2));

                Bitmap croppedCompass = Crop(compassArea, compassRect);
                // work out where the target indicator is
                pictureBox2.Image = croppedCompass;

                Point2f result = FindTarget2(croppedCompass);
                history.Insert(0, result);
                if (history.Count > 5)
                    history.RemoveAt(5);
                return result;
            }
            catch
            {
                history.Clear();
                throw;
            }
        }

        /// <summary>
        /// Compute the final velocity from a quadratic fit
        /// </summary>
        /// <returns>dx/dt at t2</returns>
        public static double QuadFitFinalVelocity(double x1, double x2, double x3, DateTime dt1, DateTime dt2, DateTime dt3)
        {
            double t3 = (dt3 - dt3).TotalSeconds;
            double t2 = (dt3 - dt2).TotalSeconds;
            double t1 = (dt3 - dt1).TotalSeconds;

            // http://www.vb-helper.com/howto_find_quadratic_curve.html
            var atop = (x2 - x1) * (t1 - t3) + (x3 - x1) * (t2 - t1);
            var abottom = (t1 - t3) * (t2 * t2 - t1 * t1) + (t2 - t1) * (t3 * t3 - t1 * t1);
            if (abottom == 0.0)
                throw new Exception("no quad fit");

            var btop = (x2 - x1) - atop / abottom * (t2 * t2 - t1 * t1);
            var bbottom = (t2 - t1);
            if (bbottom == 0.0)
                throw new Exception("no quad fit");

            return /*2 * atop / abottom * t3 +*/ btop / bbottom;
        }

        public Point2f GetOrientationVelocity()
        {
            if (history.Count < 2)
                return new Point2f();
            // linear as only two history elements are available
            if (history.Count == 2)
                return history[1] - history[0];
            // quadratic if more than two
            List<DateTime> ts = screen.timestamp_history;
            double vx = QuadFitFinalVelocity(history[2].X, history[1].X, history[0].X, ts[2], ts[1], ts[0]);
            double vy = QuadFitFinalVelocity(history[2].Y, history[1].Y, history[0].Y, ts[2], ts[1], ts[0]);
            return new Point2f((float)vx, (float)vy);
        }


        /// <summary>
        /// Returns true if we can detect on the screen that the jump loading screen has ended (i.e. finished loading, we have arrived at the next star)
        /// </summary>
        public bool MatchFaceplant()
        {
            // if the compass stops jumping around, we've arrived
            if (DetectStationaryCompass())
                return true;

            // If the middle of the screen is saturated, there's a star there and we have arrived.
            var d = 30;
            Bitmap image = CompassSensor.Crop(screen.bitmap, new Rectangle(screen.bitmap.Width / 2 - d, screen.bitmap.Height / 2 - d, 2 * d, 2 * d));
            Mat screencentre = BitmapConverter.ToMat(image);
            Mat hsv = screencentre.CvtColor(ColorConversionCodes.BGR2HSV);
            pictureBox2.Image = BitmapConverter.ToBitmap(hsv.Split()[2]);
            if (hsv.Mean()[2] > 180.0)
                return true; // small star while still loading, average "value" 29; star filling little box: 254-255.   

            return false;         
        }

        List<Point2f> compassHistory = new List<Point2f>(); // up to the last five compass points
        DateTime lastCompassTime = DateTime.UtcNow.AddHours(-1);
        private double sq(double x) { return x * x; }
        private bool DetectStationaryCompass()
        {
            if ((DateTime.UtcNow - lastCompassTime).TotalSeconds > 2)
                compassHistory.Clear();
            lastCompassTime = DateTime.UtcNow;

            const int wobbleFrames = 5;

            Point2f compass;
            try { compass = GetOrientation(); } catch (ArgumentException) { return false; }
            compassHistory.Add(compass);
            if (compassHistory.Count > wobbleFrames)
                compassHistory.RemoveAt(0);
            if (compassHistory.Count < wobbleFrames)
                return false;

            double totalDistance = 0.0;
            for (int i = 1; i < compassHistory.Count; i++)
                totalDistance += sq(compassHistory[i - 1].X - compassHistory[i].X) + sq(compassHistory[i - 1].Y - compassHistory[i].Y);
            return totalDistance < sq(0.1) * wobbleFrames; // max "wobble" of 0.1, in two dimensions
        }
    }
}
