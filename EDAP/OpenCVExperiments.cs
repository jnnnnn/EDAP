using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;

namespace EDAP
{
    class OpenCVExperiments
    {
        public static Mat Levels(Mat input, int channel = 0/*default blue of OpenCV's BGR format*/)
        {
            Mat[] channels = input.Split();
            Mat result = new Mat(channels[0].Size(), channels[0].Type());
            channels[channel].ConvertTo(result, -1, alpha: 2, beta: -255); // value -> value * 2 - 255
            return result;
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

        public static void FindTargetsTest(Bitmap compasses)
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
                g2.DrawRectangle(new Pen(Color.FromName("green"), 2), circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Radius * 2, circle.Radius * 2);
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
    }
}
