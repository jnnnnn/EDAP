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
            Mat matchtarget = new Mat("compass_template.png", ImreadModes.Color);
            Mat res = new Mat(source.Rows - matchtarget.Rows + 1, source.Cols - matchtarget.Cols + 1, MatType.CV_32FC1);

            //Convert input images to gray
            Mat gref = source.CvtColor(ColorConversionCodes.BGR2GRAY);
            Mat gtpl = matchtarget.CvtColor(ColorConversionCodes.BGR2GRAY);

            Cv2.MatchTemplate(gref, gtpl, res, TemplateMatchModes.CCoeffNormed);
            Cv2.Threshold(res, res, 0.8, 1.0, ThresholdTypes.Tozero);

            while (true)
            {
                double minval, maxval, threshold = 0.8;
                OpenCvSharp.Point minloc, maxloc;
                Cv2.MinMaxLoc(res, out minval, out maxval, out minloc, out maxloc);

                if (maxval >= threshold)
                    return new System.Drawing.Point(maxloc.X + 40, maxloc.Y + 43);
            }
            throw new System.ArgumentException("Couldn't find crosshair or dot.");
        }
        
        // returns the normalized vector from the compass center to the blue dot
        public PointF GetOrientation(Bitmap compassImage)
        {
            float s = Properties.Settings.Default.Scale;
            Graphics g = Graphics.FromImage(compassImage);
            System.Drawing.Point crosshair = FindCircle(compassImage);
            Rectangle compassRect = new Rectangle(crosshair.X - 27, crosshair.Y - 27, 54, 54);

            // work out where the target indicator is
            pictureBox2.Image = Crop(compassImage, compassRect);
            
            g.DrawRectangle(new Pen(Color.FromName("blue"), 2), compassRect);
            /*
            Graphics g = Graphics.FromImage(compassImage);
            g.DrawLine(new Pen(Color.FromName("red"), width: 2), crosshair.X, crosshair.Y, target.X, target.Y);

            return (target - crosshair) / 13;
            */
            return new PointF(0, 0);
        }
    }
}
