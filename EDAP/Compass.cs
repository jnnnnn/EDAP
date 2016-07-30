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

        private System.Drawing.Point FindCircle(Bitmap image)
        {
            Mat source = BitmapConverter.ToMat(image);
            //Convert input images to gray
            Mat imgGray = source.CvtColor(ColorConversionCodes.BGR2GRAY);
            CircleSegment[] circles = new CircleSegment[] { };
            float dp = 1.5f;
            for (; dp > 1f && circles.Length < 1; dp -= 0.1f)
            {
                circles = Cv2.HoughCircles(imgGray, HoughMethods.Gradient, dp, minDist: 30, param1: 200, param2: 100, minRadius: 22, maxRadius: 28);
            }
            Console.WriteLine(string.Format("dp = {0}", dp));
            if (circles.Length > 1)
                throw new System.ArgumentException("More than one valid circle...");
            if (circles.Length < 1)
                throw new System.ArgumentException("No valid circles");
            var c = circles[0];
            Graphics g = Graphics.FromImage(image);
            g.DrawEllipse(new Pen(Color.FromName("green"), 2), c.Center.X - c.Radius, c.Center.Y - c.Radius, c.Radius * 2, c.Radius * 2);
            return new System.Drawing.Point((int)c.Center.X, (int)c.Center.Y);
            
            /* looking for circle works better than matching template */
            /*
            Mat matchtarget = new Mat("compass_template.png", ImreadModes.Color);
            Mat gtpl = matchtarget.CvtColor(ColorConversionCodes.BGR2GRAY);

            Mat res = new Mat(source.Rows - matchtarget.Rows + 1, source.Cols - matchtarget.Cols + 1, MatType.CV_32FC1);
            Cv2.MatchTemplate(source, matchtarget, res, TemplateMatchModes.CCoeffNormed);
            Cv2.Threshold(res, res, 0.8, 1.0, ThresholdTypes.Tozero);

            for(int i =0; i < 1; i++)
            {
                double minval, maxval, threshold = 0.8;
                OpenCvSharp.Point minloc, maxloc;
                Cv2.MinMaxLoc(res, out minval, out maxval, out minloc, out maxloc);

                if (maxval >= threshold)
                    return new System.Drawing.Point(maxloc.X + 40, maxloc.Y + 43);
            }+
            throw new System.ArgumentException("Couldn't find crosshair or dot.");
            */
        }

        private System.Drawing.Point FindTarget(Bitmap croppedCompass)
        {
            Mat source = BitmapConverter.ToMat(croppedCompass);
            //Convert input images to gray
            Mat imgGray = source.CvtColor(ColorConversionCodes.BGR2GRAY);
            int threshold = 80;
            //imgGray = imgGray.Blur(new OpenCvSharp.Size(5, 5)).Canny(threshold, threshold * 3);
            var circles = Cv2.HoughCircles(imgGray, HoughMethods.Gradient, 5, minDist: 30, param1: 200, param2: 100, minRadius: 1, maxRadius: 8);
            new Window("Hough_line_standard", WindowMode.AutoSize, imgGray);

            if (circles.Length > 1)
                throw new System.ArgumentException("More than one valid circle...");
            if (circles.Length < 1)
                throw new System.ArgumentException("No valid circles");
            var c = circles[0];
            return new System.Drawing.Point((int)c.Center.X, (int)c.Center.Y);
        }

        
        // returns the normalized vector from the compass center to the blue dot
        public PointF GetOrientation(Bitmap compassImage)
        {
            float s = Properties.Settings.Default.Scale;
            Graphics g = Graphics.FromImage(compassImage);
            System.Drawing.Point crosshair = FindCircle(compassImage);
            int outerRadius = 35;
            Rectangle compassRect = new Rectangle(crosshair.X - outerRadius, crosshair.Y - outerRadius, outerRadius*2, outerRadius*2);

            Bitmap croppedCompass = Crop(compassImage, compassRect);
            // work out where the target indicator is
            pictureBox2.Image = croppedCompass;

            System.Drawing.Point target = FindTarget(croppedCompass);
            Graphics g2 = Graphics.FromImage(croppedCompass);
            g2.DrawRectangle(new Pen(Color.FromName("red"), 3), target.X - 3, target.Y - 3, 6, 6);
            g.DrawRectangle(new Pen(Color.FromName("red"), 2), compassRect);
            return new PointF(0, 0);
        }
    }
}
