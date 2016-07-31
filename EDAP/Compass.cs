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

        public static Mat Levels(Mat input, int channel = 0/*default blue of OpenCV's BGR format*/)
        {
            Mat[] channels = input.Split();
            channels[channel].ConvertTo(channels[channel], -1, alpha: 2, beta: -255); // value -> value * 2 - 255
            return channels[channel];
        }

        private System.Drawing.Point FindCircle(Bitmap image)
        {
            Mat source = BitmapConverter.ToMat(image);
            //Convert input images to gray
            Mat reds = Levels(source, channel: 2);
            CircleSegment[] circles = Cv2.HoughCircles(reds, 
                HoughMethods.Gradient, 
                dp:2f, 
                minDist: 500 /* this is huge so we only find the best one */, 
                param1: 200, 
                param2: 50, /* this is really low so we usually find something */
                minRadius: 22, 
                maxRadius: 28);

            if (circles.Length > 1)
                throw new System.ArgumentException("More than one valid circle...");
            if (circles.Length < 1)
                throw new System.ArgumentException("No valid circles");
            var c = circles[0];
            //Graphics g = Graphics.FromImage(image);
            //g.DrawEllipse(new Pen(Color.FromName("green"), 2), c.Center.X - c.Radius, c.Center.Y - c.Radius, c.Radius * 2, c.Radius * 2);
            return new System.Drawing.Point((int)c.Center.X, (int)c.Center.Y);
        }

        public System.Drawing.Point FindTarget(Bitmap croppedCompass)
        {            
            // todo: use blue/white threshold then opencvsharp.blob. or try to figure out the circle detector.
            //cv::HoughCircles(mat, circles, CV_HOUGH_GRADIENT, 2, 50, 100, 8, 2, 8);
            // Parameters 1 and 2 don't affect accuracy as such, more reliability. Param 1 will set the sensitivity; how strong the edges of the circles need to be. Too high and it won't detect anything, too low and it will find too much clutter. Param 2 will set how many edge points it needs to find to declare that it's found a circle. Again, too high will detect nothing, too low will declare anything to be a circle. The ideal value of param 2 will be related to the circumference of the circles.
            // blur then blue/brightness filter then blob detection is probably the way to go here
            Mat source = BitmapConverter.ToMat(croppedCompass);
            Mat blue = Levels(source, channel: 0);
            Window w = new Window("Blues", blue);
            // Binary search threshold until we get only one circle..
            var circles = Cv2.HoughCircles(blue, HoughMethods.Gradient, 0.1, minDist: 30, param1: 200, param2: 100, minRadius: 3, maxRadius: 3);

            //.CvtColor(ColorConversionCodes.BGR2GRAY).MinMaxLoc(out minval, out maxval, out minloc, out maxloc);
            return new System.Drawing.Point(0, 0);
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
