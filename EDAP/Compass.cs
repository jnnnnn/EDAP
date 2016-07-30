using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
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

        private int clamp(int value)
        {
            return (value < 0) ? 0 : (value > 255) ? 255 : value;
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

        // based on the article http://www.aforgenet.com/articles/shape_checker/            
        private AForge.Point FindCircle(Bitmap image, Color color, int colorRange, double minRadius, double maxRadius)
        {
            ColorFiltering filter = new ColorFiltering();            
            filter.Red = new IntRange(clamp(color.R - colorRange), clamp(color.R + colorRange));
            filter.Green = new IntRange(clamp(color.G - colorRange), clamp(color.G + colorRange));
            filter.Blue = new IntRange(clamp(color.B - colorRange), clamp(color.B + colorRange));
            Bitmap filteredImage = filter.Apply(image);
            pictureBox2.Image = filteredImage;
            Application.DoEvents();
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.ProcessImage(filteredImage);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            SimpleShapeChecker shapeChecker = new SimpleShapeChecker();

            AForge.Point centerPoint = new AForge.Point();
            Graphics g = Graphics.FromImage(image);
            foreach (var blob in blobs)
            {
                AForge.Point center;
                float radius;
                if (shapeChecker.IsCircle(blobCounter.GetBlobsEdgePoints(blob), out center, out radius))
                {
                    g.DrawRectangle(new Pen(Color.FromName("green"), 2), blob.Rectangle);
                    pictureBox2.Image = filteredImage;
                    Application.DoEvents();
                    if (radius > minRadius && radius < maxRadius)
                        return centerPoint = center;
                }
            }
            throw new System.ArgumentException("Couldn't find crosshair or dot.");
        }

        // returns the normalized vector from the compass center to the blue dot
        public AForge.Point GetOrientation(Bitmap compassImage)
        {            
            // work out where the center of the crosshair is
            AForge.Point crosshair = FindCircle(
                image: compassImage,
                color: Color.FromArgb(230, 98, 29), // default orange HUD 
                colorRange: 100,
                minRadius: 23 * Properties.Settings.Default.Scale, 
                maxRadius: 30 * Properties.Settings.Default.Scale);

            // work out where the target indicator is
            AForge.Point target = FindCircle(
                image: compassImage,
                color: Color.FromArgb(104, 180, 249) /* pale blue dot */,
                colorRange: 20,
                minRadius: 1.5 * Properties.Settings.Default.Scale,
                maxRadius: 5 * Properties.Settings.Default.Scale);

            Graphics g = Graphics.FromImage(compassImage);
            g.DrawLine(new Pen(Color.FromName("red"), width: 2), crosshair.X, crosshair.Y, target.X, target.Y);

            return (target - crosshair) / 13;
        }
    }
}
