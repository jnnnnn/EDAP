using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EDAP
{
    // This class is to get the orientation of the ship relative to the target so that we can point at the target.
    class CompassRecognizer
    {
        private PictureBox pictureBox2;
        
        private Mat template_open = new Mat("res3/target-open.png", ImreadModes.GrayScale);
        private Mat template_closed = new Mat("res3/target-closed.png", ImreadModes.GrayScale);
        private const float match_threshold = 0.7f;
        private Screen screen;
        public CompassRecognizer(Screen screen, PictureBox pictureBox2)
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

            if (circles.Length > 1)
                throw new System.ArgumentException("More than one valid circle...");
            if (circles.Length < 1)
                throw new System.ArgumentException("No valid circles");
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
            Mat clean = new Mat(blues2.Size(), blues2.Type());
            blues2.CopyTo(clean, blues2.InRange(128, 255));

            double minval, maxval_closed, maxval_open;
            OpenCvSharp.Point minloc, maxloc_closed, maxloc_open;

            Mat result_closed = new Mat(clean.Size(), clean.Type());
            Cv2.MatchTemplate(clean, template_closed, result_closed, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result_closed, out minval, out maxval_closed, out minloc, out maxloc_closed);

            Mat result_open = new Mat(clean.Size(), clean.Type());
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
        
        // returns the normalized vector from the compass center to the blue dot
        public Point2f GetOrientation()
        {
            float s = Properties.Settings.Default.Scale;
            Bitmap compassArea = Crop(screen.bitmap, 600, 550, 900, 1050);
            Graphics g = Graphics.FromImage(compassArea);
            CircleSegment crosshair = FindCircle(compassArea);
            var r = crosshair.Radius + 12;
            var c = crosshair.Center;
            Rectangle compassRect = new Rectangle((int)(c.X - r), (int)(c.Y - r), (int)(r*2), (int)(r * 2));

            Bitmap croppedCompass = Crop(compassArea, compassRect);
            // work out where the target indicator is
            pictureBox2.Image = croppedCompass;

            return FindTarget2(croppedCompass);            
        }
    }
}
