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

        public Screenshot screen;
        private Keyboard keyboard;
        private PilotJumper pilot;
        private CompassSensor compassRecognizer;
        private CruiseSensor cruiseSensor;

        private IntPtr hwnd;

        private DateTime lastClick = DateTime.UtcNow;

        // make sure we focus back to the game window so that it registers keypresses
        private void Focusize()
        {
            SwitchToThisWindow(hwnd, true);
            Thread.Sleep(10);
        }

        public Form1()
        {
            InitializeComponent();
            Location = new Point(1920 - this.Size.Width, 0);
            keyboard = new Keyboard();
            screen = new Screenshot();
            cruiseSensor = new CruiseSensor();
            cruiseSensor.screen = screen;
            cruiseSensor.debugWindow = pictureBox2;
            compassRecognizer = new CompassSensor(screen, pictureBox2);            
            pilot = new PilotJumper();
            pilot.keyboard = keyboard;
            pilot.compassRecognizer = compassRecognizer;
            pilot.screen = screen;
            pilot.cruiseSensor = cruiseSensor;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            enabled = !enabled;

            if (enabled)
                Sounds.Play("autopilot engaged.mp3");
            else
                Sounds.Play("autopilot disengaged.mp3");

            keyboard.Clear();
            pilot.Reset();
            
            Focusize();

            lastClick = DateTime.UtcNow;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            screen.ClearSaved();

            var t0 = DateTime.UtcNow;
            Process proc = Process.GetProcessesByName(Properties.Settings.Default.ProcName).FirstOrDefault();
            if (proc == null)
            {
                label1.Text = "Could not find main window. Run in Administrator mode, and check settings.";
                return;
            }
            hwnd = proc.MainWindowHandle;
            keyboard.hWnd = hwnd;
            screen.hWnd = hwnd;

            numericUpDown1.Value = pilot.Jumps;

            SetButtonColors();

            if (enabled)
                pilot.Act();
            else
                pilot.Idle();                            
            label1.Text = pilot.status;
            
            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();
            label2.Text = string.Join(", ", keyboard.pressed_keys);            
        }

        private void SetButtonColors()
        {
            buttonAuto.ForeColor = enabled ? Color.Green : Color.Coral;
            buttonCruise.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.Cruise) ? Color.Green : Color.Coral;
            buttonMap.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.SysMap) ? Color.Green : Color.Coral;
            buttonHorn.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.Honk) ? Color.Green : Color.Coral;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer = new System.Windows.Forms.Timer();
            timer.Interval = PilotJumper.TIMERINTERVAL_MS;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Focusize();
            pilot.state ^= PilotJumper.PilotState.Cruise;
            lastClick = DateTime.UtcNow;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            pilot.Jumps = (int)numericUpDown1.Value;
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            pilot.state ^= PilotJumper.PilotState.SysMap;
            Focusize();
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            OpenCVExperiments.FindTriQuadrant();
        }

        private void button1_Click_2(object sender, EventArgs e)
        {
            pilot.state ^= PilotJumper.PilotState.Honk;
            Focusize();
        }
    }
}
