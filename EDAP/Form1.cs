using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
            Bitmap screenshot = Screenshot.PrintWindow(hwnd);
            Rectangle cropArea = new Rectangle(settings.x1, settings.y1, settings.x2 - settings.x1, settings.y2 - settings.y1);
            Bitmap compass = screenshot.Clone(cropArea, screenshot.PixelFormat);
            pictureBox1.Image = compass;
            Text = (DateTime.UtcNow - t0).TotalMilliseconds.ToString();

            SwitchToThisWindow(hwnd, true);
    }
    }
}
