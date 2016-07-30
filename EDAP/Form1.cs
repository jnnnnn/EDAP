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
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Timer t = new Timer();
            t.Start();

            Bitmap screenshot = new Bitmap(1920, 1080);
            try
            {
                IntPtr hwnd;

                Process proc = Process.GetProcessesByName(Properties.Settings.Default.ProcName)[0];
                hwnd = proc.MainWindowHandle;

                screenshot = Screenshot.PrintWindow(hwnd);
            } catch
            {
                Console.WriteLine("Could not find main window. Run in Administrator mode, and check settings.");
            }

            pictureBox1.Image = screenshot;
            t.Stop();

            Text = t.ToString();
        }
        
    }
}
