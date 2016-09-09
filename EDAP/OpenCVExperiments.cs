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

        public static void FindTriQuadrant()
        {
            Bitmap image = (Bitmap)Image.FromFile("res3/supercruisetarget.png");
            Mat source = BitmapConverter.ToMat(image);
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);
            /* Paint.Net uses HSV [0..360], [0..100], [0..100].
             * OpenCV uses H: 0 - 180, S: 0 - 255, V: 0 - 255
             * Paint.NET colors:
             * 50   94  100     bright yellow
             * 27   93  90      orange
             * 24   91  74      brown
             * 16   73  25      almost background (low V)
             * suggested range [20..55], [80..100], [50..100] (paint.net)
             * suggested range [10..27], [200..255], [128..255] (openCV
             * */
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 10, 200, 128 }), InputArray.Create(new int[] { 27, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);
            Window w3 = new Window("yellowfilter", sourceHSVFiltered.CvtColor(ColorConversionCodes.HSV2BGR));            
            Mat sourceGrey = sourceHSVFiltered.Split()[2]; // Value channel is pretty good as a greyscale conversion
            Window w4 = new Window("yellowFilterValue", sourceGrey);
            CircleSegment[] circles2 = sourceGrey.HoughCircles(
                HoughMethods.Gradient,
                dp: 1f, /* resolution scaling factor?  full resolution seems to work better */
                minDist: 100, /* if we find more than one then we go to the second analysis, the crosshair is probably blue as well*/
                param1: 100, /* default was fine after experimentation */
                param2: 13, /* required quality factor. 9 finds too many, 14 finds too few */
                minRadius: 40,
                maxRadius: 47);
            foreach (CircleSegment circle in circles2)
            {
                var quarterCircle = new OpenCvSharp.Point2f(circle.Radius, circle.Radius);
                source.Rectangle(circle.Center - quarterCircle, circle.Center + quarterCircle, new Scalar(0, 255, 0));
            }


            Mat templatepointer = new Mat("res3/squaretarget.png", ImreadModes.GrayScale);            
            Mat matches = sourceGrey.MatchTemplate(templatepointer, TemplateMatchModes.CCoeffNormed);
            Window w6 = new Window("pointer", matches);
            OpenCvSharp.Point minloc, maxloc;
            matches.MinMaxLoc(out minloc, out maxloc);

            source.Rectangle(maxloc, maxloc + new OpenCvSharp.Point(templatepointer.Size().Width, templatepointer.Size().Height), new Scalar(255, 255, 0));

            Window w5 = new Window("result", source);
        }

        public static void FindTriQuadrantTime()
        {
            // This is gonna be tricky
            /* Possible algorithms: 
             *  - template match each character
             *  - tesseract
             */
            
        }
        
        public static bool MatchSafDisengag()
        {
            // MatchTemplate doesn't allow for scaling / rotation. Allow more leeway by reducing resolution?

            Bitmap image = (Bitmap)Image.FromFile("res3/safdisengagtest.png");
            Mat source = BitmapConverter.ToMat(image);
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);

            Mat[] channels = source.Split();
            Mat blues2 = channels[0];
            Mat clean = new Mat(blues2.Size(), blues2.Type());
            blues2.CopyTo(clean, blues2.InRange(128, 255));
            Window w2 = new Window(clean);

            Mat template = new Mat("res3/safdisengag.png", ImreadModes.GrayScale);
            Mat matches = blues2.MatchTemplate(template, TemplateMatchModes.SqDiffNormed);
            Window w3 = new Window(matches);
            Window w4 = new Window(matches.InRange(0.0, 0.9));
            double minVal, maxVal;
            matches.MinMaxLoc(out minVal, out maxVal);
            return minVal < 0.5; // for SqDiffNormed, perfect match 0.1; no match [0.99 .. 1.0].
        }
    }
}
