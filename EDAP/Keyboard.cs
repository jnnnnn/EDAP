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

        /// <summary>
        /// Returns the scan keycode of a given letter or number on the keyboard.
        /// </summary>
        public static ScanCode LetterToKey(char letter)
        {
            switch (letter)
            {
                case '0': return ScanCode.KEY_0;
                case '1': return ScanCode.KEY_1;
                case '2': return ScanCode.KEY_2;
                case '3': return ScanCode.KEY_3;
                case '4': return ScanCode.KEY_4;
                case '5': return ScanCode.KEY_5;
                case '6': return ScanCode.KEY_6;
                case '7': return ScanCode.KEY_7;
                case '8': return ScanCode.KEY_8;
                case '9': return ScanCode.KEY_9;
                case 'A': return ScanCode.KEY_A;
                case 'B': return ScanCode.KEY_B;
                case 'C': return ScanCode.KEY_C;
                case 'D': return ScanCode.KEY_D;
                case 'E': return ScanCode.KEY_E;
                case 'F': return ScanCode.KEY_F;
                case 'G': return ScanCode.KEY_G;
                case 'H': return ScanCode.KEY_H;
                case 'I': return ScanCode.KEY_I;
                case 'J': return ScanCode.KEY_J;
                case 'K': return ScanCode.KEY_K;
                case 'L': return ScanCode.KEY_L;
                case 'M': return ScanCode.KEY_M;
                case 'N': return ScanCode.KEY_N;
                case 'O': return ScanCode.KEY_O;
                case 'P': return ScanCode.KEY_P;
                case 'Q': return ScanCode.KEY_Q;
                case 'R': return ScanCode.KEY_R;
                case 'S': return ScanCode.KEY_S;
                case 'T': return ScanCode.KEY_T;
                case 'U': return ScanCode.KEY_U;
                case 'V': return ScanCode.KEY_V;
                case 'W': return ScanCode.KEY_W;
                case 'X': return ScanCode.KEY_X;
                case 'Y': return ScanCode.KEY_Y;
                case 'Z': return ScanCode.KEY_Z;
                default:
                    throw new Exception("Unrecognized letter or number");
            }
        }

        /// <summary>
        /// Returns the scan keycode of one of the numbers on the numpad.
        /// </summary>
        /// <param name="letter"></param>
        /// <returns></returns>
        public static ScanCode NumpadToKey(char letter)
        {
            switch (letter)
            {
                case '0': return ScanCode.NUMPAD_0;
                case '1': return ScanCode.NUMPAD_1;
                case '2': return ScanCode.NUMPAD_2;
                case '3': return ScanCode.NUMPAD_3;
                case '4': return ScanCode.NUMPAD_4;
                case '5': return ScanCode.NUMPAD_5;
                case '6': return ScanCode.NUMPAD_6;
                case '7': return ScanCode.NUMPAD_7;
                case '8': return ScanCode.NUMPAD_8;
                case '9': return ScanCode.NUMPAD_9;
                default:
                    throw new Exception("Invalid numpad key");
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
            if (pressed_keys.Contains(key))
                return;

            pressed_keys.Add(key);

            // program doesn't recognize this.. 
            // PostMessage(hWnd, WM_KEYDOWN, ((IntPtr)key), (IntPtr)0);

            // need to use sendinput instead
            // this means the target program must be the foreground window :(

            // program also doesn't recognize virtual key codes.. 

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
        /// Immediately release a key.
        /// </summary>
        /// <param name="key">The scan code (NOT the VK_ keycode!) of the key to release.</param>
        public void Keyup(ScanCode key)
        {
            if (!pressed_keys.Contains(key))
                return;

            pressed_keys.Remove(key);
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
            // inefficient but ok because there will only ever be up to three keys pressed at once
            while (pressed_keys.Count > 0)
                Keyup(pressed_keys.ToArray()[0]);
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
