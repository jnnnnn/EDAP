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

        private Keyboard keyboard;
        private PilotJumper pilot;
        
        public Form1()
        {
            InitializeComponent();
            keyboard = new Keyboard();
            pilot = new PilotJumper();
            pilot.keyboard = keyboard;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            enabled = !enabled;
            keyboard.Clear();
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
            keyboard.hWnd = hwnd;
            using (Bitmap screenshot = Screenshot.PrintWindow(hwnd))
            {
                Bitmap compass = new Bitmap(10, 10);
                try
                {
                    double scale = Properties.Settings.Default.Scale;
                    if (Math.Abs(screenshot.Height - 1080 * scale) > 10)
                        throw new ArgumentException("Error: screenshot resultion wrong");
                    compass = CompassRecognizer.Crop(screenshot,
                    ss.x1 * scale, ss.y1 * scale, ss.x2 * scale, ss.y2 * scale);

                    CompassRecognizer recognizer = new CompassRecognizer(pictureBox2);
                    PointF vector = recognizer.GetOrientation(compass);
                    pictureBox1.Image = compass;
                    label1.Text = String.Format("{0:0.0},{1:0.0}", vector.X, vector.Y);
                    pilot.Respond(vector);
                }
                catch (Exception err)
                {
                    pictureBox1.Image = compass;
                    label1.Text = "Error: " + err.ToString();
                    Console.WriteLine(err.ToString());
                }
            }
            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();
            label2.Text = pilot.jumps_remaining.ToString() + " " + string.Join(", ", keyboard.pressed_keys);
            //SwitchToThisWindow(hwnd, true);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer = new Timer();
            timer.Interval = 100;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (enabled)
                WhereAmI();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            CompassRecognizer recognizer = new CompassRecognizer(pictureBox2);
            Bitmap image = (Bitmap)Image.FromFile("compass_tests.png");
            recognizer.FindTargetsTest(image);
            pictureBox2.Image = image;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            pilot.QueueJump();
        }
    }
}
