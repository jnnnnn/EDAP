using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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

        /// <summary>
        /// Filter the given image to select certain yellow hues (returned as grayscale)
        /// </summary>
        public static Mat IsolateYellow(Mat source)
        {
            Mat sourceHSV = source.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 10, 200, 128 }), InputArray.Create(new int[] { 30, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);
            Mat valueChannel = sourceHSVFiltered.Split()[2];
            return valueChannel;
        }

        public static void FindCompasses()
        {

            Mat source = new Mat("res3/compass_tests.png");
            Window w0 = new Window(source);

            //Convert input images to gray
            Mat reds = Levels(source, channel: 2);
            reds = IsolateYellow(source);
            Window w239048 = new Window(reds);
            // these parameters were tuned using the test functionality
            CircleSegment[] circles = Cv2.HoughCircles(reds,
                HoughMethods.Gradient,
                dp: 2f, // this was tuned by experimentation
                minDist: 20, // this is huge so we only find the best one
                param1: 200, // this was tuned by experimentation
                param2: 30, // this is quite low so we usually find something
                minRadius: 22,
                maxRadius: 28);
            
            foreach (CircleSegment c in circles)
                source.Circle(c.Center, (int)c.Radius, new Scalar(0, 255, 0));
            Window w1 = new Window(source);
        }

        public static void FindTargetsTest()
        {
            Mat source = new Mat("res3/compass_tests.png");
            Window w0 = new Window(source);


            Mat template_open = new Mat("res3/target-open.png", ImreadModes.GrayScale);
            Mat template_closed = new Mat("res3/target-closed.png", ImreadModes.GrayScale);
            Mat[] channels = source.Split();
            Mat blues2 = channels[0];
            Mat clean = blues2.EmptyClone();
            clean.SetTo(0);
            blues2.CopyTo(clean, blues2.InRange(128, 255));
            
            double minval, maxval_closed, maxval_open;
            OpenCvSharp.Point minloc, maxloc_closed, maxloc_open;

            const float match_threshold = 0.7f;

            Mat result_closed = clean.EmptyClone();
            Cv2.MatchTemplate(clean, template_closed, result_closed, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result_closed, out minval, out maxval_closed, out minloc, out maxloc_closed);
            Cv2.Threshold(result_closed, result_closed, match_threshold, 10000, ThresholdTypes.Tozero);
            Window w1 = new Window(result_closed);

            Mat result_open = clean.EmptyClone();
            Cv2.MatchTemplate(clean, template_open, result_open, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result_open, out minval, out maxval_open, out minloc, out maxloc_open);
            Cv2.Threshold(result_open, result_open, match_threshold, 10000, ThresholdTypes.Tozero);
            Window w2 = new Window(result_open);
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

            Mat blues2 = source.Split()[0];
            Mat clean = blues2.EmptyClone();
            clean.SetTo(0);
            blues2.CopyTo(clean, blues2.InRange(200, 255));
            Window w2 = new Window(clean);

            Mat template = new Mat("res3/safdisengag.png", ImreadModes.GrayScale);
            Mat matches = blues2.MatchTemplate(template, TemplateMatchModes.SqDiffNormed);
            Window w3 = new Window(matches);
            Window w4 = new Window(matches.InRange(0.0, 0.8));
            double minVal, maxVal;
            matches.MinMaxLoc(out minVal, out maxVal);
            return minVal < 0.8; // for SqDiffNormed, perfect match 0.1; no match [0.99 .. 1.0].
        }

        public static void MatchSafDisengag2()
        {
            Bitmap screen = new Bitmap("Screenshot_0022.bmp");
            Bitmap image = CompassSensor.Crop(screen, new Rectangle(800, 700, 350, 150));
            Mat source = BitmapConverter.ToMat(image);
            Mat blues = source.Split()[0];
            Mat clean = blues.EmptyClone();
            clean.SetTo(0); // make sure the matrix is blank.            
            blues.CopyTo(clean, blues.InRange(250, 255));
            Mat matches = clean.MatchTemplate(new Mat("res3/safdisengag250.png", ImreadModes.GrayScale), TemplateMatchModes.CCoeffNormed);
            clean.ImWrite("safdisengag250.png");
            double minVal, maxVal;
            matches.MinMaxLoc(out minVal, out maxVal);
            Window w2 = new Window(clean);
            Window w3 = new Window(matches);
            Window w5 = new Window(matches.InRange(0.4, 1));
        }

        public static void MatchJumpEnd()
        {
            Bitmap screen = new Bitmap("Screenshot_0029.bmp");
            var d = 30;
            Bitmap image = CompassSensor.Crop(screen, new Rectangle(screen.Width / 2 - d, screen.Height / 2 - d, d * 2, d * 2));
            Mat screencentre = BitmapConverter.ToMat(image);
            Window w1 = new Window(screencentre);
            Mat hsv = screencentre.CvtColor(ColorConversionCodes.BGR2HSV);
            var x = hsv.Mean();
        }

        public static void MatchCorona()
        {
            Bitmap screen = new Bitmap("Screenshot_0028.bmp");
            Bitmap cropped = CompassSensor.Crop(screen, screen.Width * 1 / 3, screen.Height * 1 / 3, screen.Width * 2 / 3, screen.Height * 2 / 3);
            Mat screenwhole = BitmapConverter.ToMat(cropped);

            // erase the vivid areas, otherwise the blur subtraction turns yellow near red to green
            Mat brightHSV = screenwhole.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat darkAreasMask = brightHSV.InRange(InputArray.Create(new int[] { 0, 0, 0 }), InputArray.Create(new int[] { 180, 255, 180 }));
            Mat darkAreas = new Mat();
            screenwhole.CopyTo(darkAreas, darkAreasMask);

            Mat screenblur = darkAreas - darkAreas.Blur(new OpenCvSharp.Size(10, 10));
            Window w3 = new Window(screenblur);

            //screenblur.SaveImage("sharplines.png");
            Mat sourceHSV = screenblur.CvtColor(ColorConversionCodes.BGR2HSV);
            /* Paint.Net uses HSV [0..360], [0..100], [0..100].
             * OpenCV uses H: 0 - 180, S: 0 - 255, V: 0 - 255
             * Paint.NET colors:
             * 73   100 18     brightest part of green edge
             * 72   98  9      very dark green
             * suggested range [70..180], [80..100], [8..100] (paint.net)
             * suggested range [35..90], [204..255], [20..255] (openCV)
             * */
            Mat mask = sourceHSV.InRange(InputArray.Create(new int[] { 35, 204, 20 }), InputArray.Create(new int[] { 90, 255, 255 }));
            Mat sourceHSVFiltered = new Mat();
            sourceHSV.CopyTo(sourceHSVFiltered, mask);
            Window w5 = new Window("yellowfilter", sourceHSVFiltered.CvtColor(ColorConversionCodes.HSV2BGR));
            Mat sourceGrey = sourceHSVFiltered.Split()[2].InRange(32, 256); // Value channel is pretty good as a greyscale conversion
            Window w6 = new Window("yellowFilterValue", sourceGrey);
            LineSegmentPoint[] result = sourceGrey.HoughLinesP(1, 3.1415 / 180, 5, 10, 2);
            List<Point2d> points = new List<Point2d>();
            foreach (var line in result)
            {
                points.Add(line.P1);
                points.Add(line.P2);
                darkAreas.Line(line.P1, line.P2, new Scalar(255, 0, 255));
            }
            CircleSegment c = CruiseSensor.ComputeCircle(points);

            darkAreas.Circle(c.Center, (int)c.Radius, new Scalar(255, 255, 0));
            Window w9 = new Window("final", darkAreas);
        }

        public static void MatchImpact()
        {
            Bitmap screen = new Bitmap("ImpactTest.png");
            //Bitmap cropped = CompassSensor.Crop(screen, screen.Width - 400, 0, screen.Width - 100, 300);
            Mat screenwhole = BitmapConverter.ToMat(screen);

            Mat brightHSV = screenwhole.CvtColor(ColorConversionCodes.BGR2HSV);
            Mat redMask = brightHSV.InRange(InputArray.Create(new int[] { 0, 250, 200 }), InputArray.Create(new int[] { 5, 256, 256 }))
                + brightHSV.InRange(InputArray.Create(new int[] { 175, 250, 200 }), InputArray.Create(new int[] { 180, 256, 256 }));
            Mat darkAreas = new Mat();
            screenwhole.CopyTo(darkAreas, redMask);
            Mat red = darkAreas.Split()[2];
            red.SaveImage("impacttemplateraw.png");
            Mat template = new Mat("res3/impacttemplate.png", ImreadModes.GrayScale);
            Mat result = new Mat(red.Size(), red.Type());
            Cv2.MatchTemplate(red, template, result, TemplateMatchModes.CCoeffNormed);
            Window w2 = new Window(red);
            Window w3 = new Window(result);
            Cv2.Threshold(result, result, 0.4, 1.0, ThresholdTypes.Tozero);
            Window w4 = new Window(result);
            Window w1 = new Window(screenwhole);
        }

        public static void MatchMenu()
        {
            Mat mscreen = new Mat("menutest.png");
            Mat template = new Mat("res3/startport_services_selected.png");


            Mat mscreenValue = IsolateYellow(mscreen);
            Mat templateValue = IsolateYellow(template);
            //foreach (var matchmode in Enum.GetValues(typeof(TemplateMatchModes)).Cast<TemplateMatchModes>())
            {
                var matchmode = TemplateMatchModes.CCorrNormed;
                Mat matches = mscreenValue.MatchTemplate(templateValue, matchmode);
                double minVal, maxVal;
                matches.MinMaxLoc(out minVal, out maxVal);
                //Window w1 = new Window(matches);
                Window w2 = new Window(matchmode.ToString(), matches.InRange(0.9, 1));
            }            
        }
    }
}
