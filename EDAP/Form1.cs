using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EDAP
{
    public partial class Form1 : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        private System.Windows.Forms.Timer timer;
        
        public Screenshot screen;
        private Keyboard keyboard;
        private PilotJumper pilot;
        private CompassSensor compassRecognizer;
        private CruiseSensor cruiseSensor;
        private MenuSensor menuSensor;
        private Relogger relogger;
        private IntPtr hwnd;
        
        private DateTime lastClick = DateTime.UtcNow;

        // make sure we focus back to the game window so that it registers keypresses
        private void Focusize()
        {
            SwitchToThisWindow(hwnd, true);
            Thread.Sleep(100);
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
            menuSensor = new MenuSensor(screen, pictureBox2);
            pilot = new PilotJumper();
            pilot.keyboard = keyboard;
            pilot.compassRecognizer = compassRecognizer;
            pilot.screen = screen;
            pilot.cruiseSensor = cruiseSensor;
            relogger = new Relogger();
            relogger.keyboard = keyboard;
            relogger.menuSensor = menuSensor;

            // If you want to run one of the experiments, call it here so it runs on startup
            //OpenCVExperiments.MatchSafDisengag2();
            //OpenCVExperiments.MatchCorona();
            //OpenCVExperiments.MatchImpact();
            //OpenCVExperiments.MatchMenu();
            //OpenCVExperiments.FindTargetsTest();
            //OpenCVExperiments.FindCompasses();
            //OpenCVExperiments.Subtarget();
            //OpenCVExperiments.Kalman();
            //OpenCVExperiments.CurrentLocationLocked();
        }

        private void buttonAuto_MouseDown(object sender, MouseEventArgs e)
        {
            pilot.state ^= PilotJumper.PilotState.Enabled;

            if (pilot.state.HasFlag(PilotJumper.PilotState.Enabled))
                Sounds.Play("autopilot engaged.mp3");

            keyboard.Clear();
            pilot.Reset(soft:false);

            Focusize();

            lastClick = DateTime.UtcNow;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // emergency kill
            if (System.Windows.Input.KeyStates.Down == System.Windows.Input.Keyboard.GetKeyStates(System.Windows.Input.Key.Escape))
            {
                pilot.state = 0;
                relogger.state = 0;
            }

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
            if (relogger.state.HasFlag(Relogger.MenuState.Enabled))
                relogger.Act();
            else
                pilot.Act();
            label1.Text = pilot.status;

            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();
            label2.Text = keyboard.ToString();
        }

        private void SetButtonColors()
        {
            buttonAuto.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.Enabled) ? Color.Green : Color.Coral;
            buttonCruise.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.Cruise) ? Color.Green : Color.Coral;
            buttonMap.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.SysMap) ? Color.Green : Color.Coral;
            buttonHorn.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.Honk) ? Color.Green : Color.Coral;
            buttonScoop.ForeColor = pilot.state.HasFlag(PilotJumper.PilotState.Scoop) ? Color.Green : Color.Coral;
            button_relog_group.ForeColor = relogger.state.HasFlag(Relogger.MenuState.Enabled) && !relogger.state.HasFlag(Relogger.MenuState.Solo) ? Color.Green : Color.Coral;
            button_relog_solo.ForeColor = relogger.state.HasFlag(Relogger.MenuState.Enabled) && relogger.state.HasFlag(Relogger.MenuState.Solo) ? Color.Green : Color.Coral;
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
            pilot.state ^= PilotJumper.PilotState.Scoop;
            Focusize();
        }

        private void button1_Click_2(object sender, EventArgs e)
        {
            pilot.state ^= PilotJumper.PilotState.Honk;
            Focusize();
        }
        
        private void numericUpDown1_MouseDown(object sender, MouseEventArgs e)
        {
            // disable when the mouse goes down on the jump count control (so that key presses don't interrupt user input)
            pilot.state &= ~PilotJumper.PilotState.Enabled;
        }
        
        private void button_relog_Click(object sender, EventArgs e)
        {
            Focusize();
            if (relogger.state.HasFlag(Relogger.MenuState.Enabled))
                relogger.state ^= Relogger.MenuState.Enabled;
            else
                relogger.state = Relogger.MenuState.Enabled | Relogger.MenuState.Solo;
        }

        private void relogGroup_Click(object sender, EventArgs e)
        {
            Focusize();
            if (relogger.state.HasFlag(Relogger.MenuState.Enabled))
                relogger.state ^= Relogger.MenuState.Enabled;
            else
                relogger.state = Relogger.MenuState.Enabled;
        }        
    }
}
