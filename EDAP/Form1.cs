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

        private Timer timer;
        private bool enabled = false; 

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            enabled = !enabled;   
        }

        private void WhereAmI()
        {
            var t0 = DateTime.UtcNow;
            var ss = Properties.Settings.Default;
            Process proc = Process.GetProcessesByName(ss.ProcName).FirstOrDefault();
            if (proc == null)
            {
                Console.WriteLine("Could not find main window. Run in Administrator mode, and check settings.");
                return;
            }
            IntPtr hwnd = proc.MainWindowHandle;
            using (Bitmap screenshot = Screenshot.PrintWindow(hwnd))
            {
                try
                {
                    double scale = Properties.Settings.Default.Scale;
                    if (Math.Abs(screenshot.Height - 1080 * scale) > 10)
                        throw new ArgumentException("Error: screenshot resultion wrong");
                    Bitmap compass = CompassRecognizer.Crop(screenshot,
                    ss.x1 * scale, ss.y1 * scale, ss.x2 * scale, ss.y2 * scale);

                    CompassRecognizer recognizer = new CompassRecognizer(pictureBox2);
                    PointF vector = recognizer.GetOrientation(compass);
                    pictureBox1.Image = compass;
                    label1.Text = vector.ToString();
                }
                catch (Exception err)
                {
                    label1.Text = "Error: " + err.ToString();
                    Console.WriteLine(err.ToString());
                }
            }
            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();

            //SwitchToThisWindow(hwnd, true);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer = new Timer();
            timer.Interval = 300;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (enabled)
                WhereAmI();
        }
    }
}
