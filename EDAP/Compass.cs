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
        
        private Mat template_open = new Mat("target-open.png", ImreadModes.GrayScale);
        private Mat template_closed = new Mat("target-closed.png", ImreadModes.GrayScale);
        private const float match_threshold = 0.7f;
        public CompassRecognizer(PictureBox pictureBox2)
        {
            this.pictureBox2 = pictureBox2;
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

        private CircleSegment FindCircle(Bitmap image)
        {
            Mat source = BitmapConverter.ToMat(image);
            //Convert input images to gray
            Mat reds = Levels(source, channel: 2);
            CircleSegment[] circles = Cv2.HoughCircles(reds, 
                HoughMethods.Gradient, 
                dp:2f, 
                minDist: 500 /* this is huge so we only find the best one */, 
                param1: 200, 
                param2: 50, /* this is quite low so we usually find something */
                minRadius: 22, 
                maxRadius: 28);

            if (circles.Length > 1)
                throw new System.ArgumentException("More than one valid circle...");
            if (circles.Length < 1)
                throw new System.ArgumentException("No valid circles");
            //Graphics g = Graphics.FromImage(image);
            //g.DrawEllipse(new Pen(Color.FromName("green"), 2), c.Center.X - c.Radius, c.Center.Y - c.Radius, c.Radius * 2, c.Radius * 2);
            return circles[0];
        }

        public System.Drawing.Point FindTarget1(Bitmap croppedCompass)
        {
            // I'm not happy with this yet.
            Mat source = BitmapConverter.ToMat(croppedCompass);
            Mat blues = Levels(source, channel: 0);
            CircleSegment[] circles = Cv2.HoughCircles(blues,
                HoughMethods.Gradient,
                dp: 1f, /* full resolution seems to work better */
                minDist: 10, /* if we find more than one then we go to the second analysis, the crosshair is probably blue as well*/
                param1: 500,
                param2: 7, /* small circles are harder to detect, need a lower quality standard*/
                minRadius: 3,
                maxRadius: 8);
                        
            if (circles.Length == 1)
                return new System.Drawing.Point((int)circles[0].Center.X, (int)circles[0].Center.Y);
            if (circles.Length > 1)
                throw new ArgumentException("Too many blue matches");
            return new System.Drawing.Point(0, 0);
        }

        public System.Drawing.PointF FindTarget2(Bitmap croppedCompass)
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
                return ComputeAngles(c, new OpenCvSharp.Point(maxloc_open.X + template_open.Size().Width / 2, maxloc_open.Y + template_open.Size().Height / 2), false);
            }
            else if (maxval_closed > match_threshold)
            {
                CircleSegment c = FindCircle(croppedCompass);
                g.DrawLine(new Pen(Color.Green, 1), c.Center.X, c.Center.Y, maxloc_closed.X + template_closed.Width / 2, maxloc_closed.Y + template_closed.Height / 2);
                return ComputeAngles(c, new OpenCvSharp.Point(maxloc_closed.X + template_closed.Size().Width / 2, maxloc_closed.Y + template_closed.Size().Height / 2), true);
            }
            else
                throw new ArgumentException("Could not find target");
        }
        
        private System.Drawing.PointF ComputeAngles(CircleSegment c, OpenCvSharp.Point t/*target*/, bool forward)
        {
            var x = (t.X - c.Center.X) / c.Radius;
            var y = (t.Y - c.Center.Y) / c.Radius;

            var rollangle = Math.Atan2(x, -y); // wrong order on purpose so that up is 0 degrees roll.

            var target_radius = Math.Sqrt(x * x + y * y);
            target_radius = Math.Min(1, target_radius);
            var pitchangle = Math.Asin(target_radius);
            if (!forward)
                pitchangle = Math.PI - pitchangle;
            return new PointF((float)pitchangle, (float)rollangle);
        }

        public void FindTargetsTest(Bitmap compasses)
        {
            
            Mat source = BitmapConverter.ToMat(compasses);
            /*
            Mat blues = Levels(source, channel: 0);
            CircleSegment[] circles = Cv2.HoughCircles(blues,
                HoughMethods.Gradient,
                dp: 1f, // full resolution seems to work better 
                minDist: 10, // if we find more than one then we go to the second analysis, the crosshair is probably blue as well
                param1: 500,
                param2: 7, // small circles are harder to detect, need a lower quality standard
                minRadius: 3,
                maxRadius: 8);
            Window w = new Window(blues);
            Graphics g = Graphics.FromImage(compasses);
            foreach (CircleSegment circle in circles)
            {
                g.DrawRectangle(new Pen(Color.FromName("red"), 2), circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Radius * 2, circle.Radius * 2);
            }
            */

            Mat[] channels = source.Split();
            Mat blues2 = channels[0];
            Mat clean = new Mat(blues2.Size(), blues2.Type());
            blues2.CopyTo(clean, blues2.InRange(128, 255));
            Window w2 = new Window(clean);
            Graphics g2 = Graphics.FromImage(compasses);
            CircleSegment[] circles2 = Cv2.HoughCircles(clean,
                HoughMethods.Gradient,
                dp: 1f, /* full resolution seems to work better */
                minDist: 10, /* if we find more than one then we go to the second analysis, the crosshair is probably blue as well*/
                param1: 500,
                param2: 7, /* small circles are harder to detect, need a lower quality standard*/
                minRadius: 3,
                maxRadius: 8);
            foreach (CircleSegment circle in circles2)
            {
                g2.DrawRectangle(new Pen(Color.FromName("green"),2), circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Radius * 2, circle.Radius * 2);
            }

            Mat template1 = new Mat("target-open.png", ImreadModes.GrayScale);
            Mat template2 = new Mat("target-closed.png", ImreadModes.GrayScale);
            Mat result = new Mat(clean.Size(), clean.Type());

            //cv::Mat greyMat, colorMat;
            //cv::cvtColor(colorMat, greyMat, cv::COLOR_BGR2GRAY);

            Cv2.MatchTemplate(clean, template2, result, TemplateMatchModes.CCoeffNormed);
            Cv2.Threshold(result, result, 0.8, 1.0, ThresholdTypes.Tozero);

            while (true)
            {
                double minval, maxval, threshold = 0.8;
                OpenCvSharp.Point minloc, maxloc;
                Cv2.MinMaxLoc(result, out minval, out maxval, out minloc, out maxloc);

                if (maxval >= threshold)
                {
                    Rect r = new Rect(maxloc.X, maxloc.Y, template1.Width, template1.Height);
                    g2.DrawRectangle(new Pen(Color.FromName("red"), 1), maxloc.X, maxloc.Y, template1.Width, template1.Height);
                    
                    Rect outRect;
                    Cv2.FloodFill(result, maxloc, new Scalar(0), out outRect, new Scalar(0.1), new Scalar(1.0));
                }
                else
                    break;
            }
        }


        // returns the normalized vector from the compass center to the blue dot
        public PointF GetOrientation(Bitmap compassImage)
        {
            float s = Properties.Settings.Default.Scale;
            Graphics g = Graphics.FromImage(compassImage);
            CircleSegment crosshair = FindCircle(compassImage);
            var r = crosshair.Radius + 12;
            var c = crosshair.Center;
            Rectangle compassRect = new Rectangle((int)(c.X - r), (int)(c.Y - r), (int)(r*2), (int)(r * 2));

            Bitmap croppedCompass = Crop(compassImage, compassRect);
            // work out where the target indicator is
            pictureBox2.Image = croppedCompass;

            return FindTarget2(croppedCompass);            
        }
    }
}
