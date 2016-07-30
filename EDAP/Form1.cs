using AForge;
using AForge.Imaging.Filters;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EDAP
{
    public partial class Form1 : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var t0 = DateTime.UtcNow;
            var settings = Properties.Settings.Default;
            Process proc = Process.GetProcessesByName(settings.ProcName).FirstOrDefault();
            if (proc == null)
            {
                Console.WriteLine("Could not find main window. Run in Administrator mode, and check settings.");
                return;
            }
            IntPtr hwnd = proc.MainWindowHandle;
            using (Bitmap screenshot = Screenshot.PrintWindow(hwnd))
            {
                double scale = Properties.Settings.Default.Scale;
                Rectangle cropArea = new Rectangle(
                    (int)(scale * settings.x1),
                    (int)(scale * settings.y1),
                    (int)(scale * (settings.x2-settings.x1)),
                    (int)(scale * (settings.y2-settings.y1)));
                if (!new Rectangle(0, 0, screenshot.Width, screenshot.Height).Contains(cropArea))
                {
                    pictureBox1.Image = screenshot;
                    Console.WriteLine("Screenshot invalid");
                    return;
                }
                Bitmap compass = screenshot.Clone(cropArea, screenshot.PixelFormat);                
                pictureBox1.Image = compass;
                try
                {
                    CompassRecognizer recognizer = new CompassRecognizer(pictureBox2);
                    AForge.Point vector = recognizer.GetOrientation(compass);
                    label1.Text = vector.ToString();
                } catch (ArgumentException)
                {
                    label1.Text = "Error: target not found";
                }
            }
            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();
            
            SwitchToThisWindow(hwnd, true);
    }

        private void pictureBox2_Click(object sender, EventArgs e)
        {

        }
    }
}
