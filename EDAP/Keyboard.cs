using EDAP.SendInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDAP
{
    /// <summary>
    /// This class keeps track of which keys are pressed and automatically unpresses them after a certain amount of time.
    /// </summary>
    class Keyboard
    {        
        public HashSet<ScanCode> pressed_keys;
        public IntPtr hWnd;
        
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;
        const int WM_SYSCOMMAND = 0x018;
        const int SC_CLOSE = 0x053;
        
        public Keyboard()
        {
            pressed_keys = new HashSet<ScanCode>();
        }
        
        public string ToString()
        {
            lock (pressed_keys)
            {
                return string.Join(", ", pressed_keys);
            }
        }

        public void SetKeyState(ScanCode key, bool down)
        {
            if (down)
                Keydown(key);
            else
                Keyup(key);
        }
        
        /// <summary>
        /// Start pressing down on a key. Sends a "keydown" event to the game.
        /// </summary>
        /// <param name="key">The scan code (NOT the VK_ keycode!) of the key to press.</param>
        public void Keydown(ScanCode key)
        {
            lock (pressed_keys)
            {
                if (pressed_keys.Contains(key))
                    return;

                pressed_keys.Add(key);
            }

            // program doesn't recognize this.. 
            // PostMessage(hWnd, WM_KEYDOWN, ((IntPtr)key), (IntPtr)0);

            // need to use sendinput instead
            // this means the target program must be the foreground window :(

            // program also doesn't recognize virtual key codes for anything except text chat.. 

            SendInputWrapper.SendInput(new INPUT
            {
                Type = (uint)InputType.Keyboard,
                Data =
                {
                    Keyboard = new KEYBDINPUT
                    {
                        Vk = 0,
                        Scan = (ushort) key,
                        Flags = (uint)KeyboardFlag.ScanCode,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            });
        }
        
        /// <summary>
        /// Immediately release a key, by sending a keyup event to the game.
        /// </summary>
        /// <param name="key">The scan code (NOT the VK_ keycode!) of the key to release.</param>
        public void Keyup(ScanCode key)
        {
            lock (pressed_keys)
            {
                if (!pressed_keys.Contains(key))
                    return;

                pressed_keys.Remove(key);
            }
            SendInputWrapper.SendInput(new INPUT
            {
                Type = (uint)InputType.Keyboard,
                Data =
                {
                    Keyboard = new KEYBDINPUT
                    {
                        Vk = 0,
                        Scan = (ushort) key,
                        Flags = (uint)KeyboardFlag.ScanCode | (uint)KeyboardFlag.KeyUp,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            });
        }
        
        /// <summary>
        /// Send a keydown and then, soon after, a keyup, event to the game.
        /// </summary>
        /// <param name="key">The key to tap on.</param>
        public void Tap(ScanCode key)
        {
            Keydown(key);
            Thread.Sleep(100); // make sure the game recognizes the keypress!
            Keyup(key);
        }

        /// <summary>
        /// Release all pressed keys immediately.
        /// </summary>
        public void Clear()
        {
            lock (pressed_keys)
            {
                // inefficient but ok because there will only ever be up to three keys pressed at once
                while (pressed_keys.Count > 0)
                    Keyup(pressed_keys.ToArray()[0]);
            }
        }

        /// <summary>
        /// Press a key for the given number of milliseconds.
        /// </summary>
        internal void TimedTap(ScanCode key, int milliseconds)
        {
            Keydown(key);
            Task.Delay(milliseconds).ContinueWith(t => Keyup(key));
        }
    }
}
