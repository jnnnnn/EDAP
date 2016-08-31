using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EDAP
{
    public partial class Form1 : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        private System.Windows.Forms.Timer timer;
        private bool enabled = false;

        private Keyboard keyboard;
        private PilotJumper pilot;
        private bool StopAtEnd = true;

        private IntPtr hwnd;

        private DateTime lastClick = DateTime.UtcNow;

        private void Focusize()
        {
            SwitchToThisWindow(hwnd, true);
            Thread.Sleep(10);
        }

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
            pilot.jumps_remaining = 0;
            
            Focusize();

            lastClick = DateTime.UtcNow;
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
            hwnd = proc.MainWindowHandle;
            keyboard.hWnd = hwnd;

            if (pilot.jumps_remaining < 1 && StopAtEnd && (DateTime.UtcNow - lastClick).TotalSeconds > 10)
            {
                // finished! stop.
                keyboard.Tap(Keyboard.LetterToKey('X'));
            }

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
                    label1.Text = string.Format("{0:0.0},{1:0.0}", vector.X, vector.Y);

                    pilot.Respond(vector);
                }
                catch (Exception err)
                {
                    pilot.Respond(null);
                    pictureBox1.Image = compass;
                    label1.Text = "Error: " + err.ToString();
                    Console.WriteLine(err.ToString());                    
                }
            }
            
            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();
            label2.Text = pilot.jumps_remaining.ToString() + (StopAtEnd ? " " : "C ") + string.Join(", ", keyboard.pressed_keys);            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer = new System.Windows.Forms.Timer();
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
            Focusize();
            pilot.QueueJump();
            StopAtEnd = true;
            lastClick = DateTime.UtcNow;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Focusize();
            StopAtEnd = false;
            lastClick = DateTime.UtcNow;
        }
    }
}
