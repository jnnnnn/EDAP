using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EDAP
{
    class MenuSensor
    {
        private PictureBox debugImage;
        public Mat template_start = new Mat("res3/start_unselected.png");
        public Mat template_landedmenu = new Mat("res3/startport_services_selected.png");
        public Mat template_mission_unselected = new Mat("res3/missionboard_unselected.png");
        public Mat template_mission_selected = new Mat("res3/missionboard_selected.png");
        public Mat template_yes = new Mat("res3/menuyes.png");

        private const float match_threshold = 0.7f;
        private Screenshot screen;
        public MenuSensor(Screenshot screen, PictureBox pictureBox2)
        {
            this.debugImage = pictureBox2;
            this.screen = screen;
        }
        
        /// <summary>
        /// See if we can find the given image anywhere on the screen.
        /// </summary>
        /// <param name="template">The image to look for</param>        
        public bool MatchScreen(Mat template)
        {
            Mat mscreen = BitmapConverter.ToMat(screen.bitmap);
            Mat mscreenValue = mscreen.CvtColor(ColorConversionCodes.BGR2HSV).Split()[2];
            Mat templateValue = template.CvtColor(ColorConversionCodes.BGR2HSV).Split()[2];

            Mat matches = mscreenValue.MatchTemplate(templateValue, TemplateMatchModes.CCoeffNormed);
            double minVal, maxVal;
            matches.MinMaxLoc(out minVal, out maxVal);
            return maxVal > match_threshold;
        }


    }
}
