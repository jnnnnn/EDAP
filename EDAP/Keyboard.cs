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
        public HashSet<int> pressed_keys;
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
            pressed_keys = new HashSet<int>();
        }

        /// <summary>
        /// Returns the scan keycode of a given letter or number on the keyboard.
        /// </summary>
        public static int LetterToKey(char letter)
        {
            switch (letter)
            {
                case '0': return (int)ScanCode.KEY_0;
                case '1': return (int)ScanCode.KEY_1;
                case '2': return (int)ScanCode.KEY_2;
                case '3': return (int)ScanCode.KEY_3;
                case '4': return (int)ScanCode.KEY_4;
                case '5': return (int)ScanCode.KEY_5;
                case '6': return (int)ScanCode.KEY_6;
                case '7': return (int)ScanCode.KEY_7;
                case '8': return (int)ScanCode.KEY_8;
                case '9': return (int)ScanCode.KEY_9;
                case 'A': return (int)ScanCode.KEY_A;
                case 'B': return (int)ScanCode.KEY_B;
                case 'C': return (int)ScanCode.KEY_C;
                case 'D': return (int)ScanCode.KEY_D;
                case 'E': return (int)ScanCode.KEY_E;
                case 'F': return (int)ScanCode.KEY_F;
                case 'G': return (int)ScanCode.KEY_G;
                case 'H': return (int)ScanCode.KEY_H;
                case 'I': return (int)ScanCode.KEY_I;
                case 'J': return (int)ScanCode.KEY_J;
                case 'K': return (int)ScanCode.KEY_K;
                case 'L': return (int)ScanCode.KEY_L;
                case 'M': return (int)ScanCode.KEY_M;
                case 'N': return (int)ScanCode.KEY_N;
                case 'O': return (int)ScanCode.KEY_O;
                case 'P': return (int)ScanCode.KEY_P;
                case 'Q': return (int)ScanCode.KEY_Q;
                case 'R': return (int)ScanCode.KEY_R;
                case 'S': return (int)ScanCode.KEY_S;
                case 'T': return (int)ScanCode.KEY_T;
                case 'U': return (int)ScanCode.KEY_U;
                case 'V': return (int)ScanCode.KEY_V;
                case 'W': return (int)ScanCode.KEY_W;
                case 'X': return (int)ScanCode.KEY_X;
                case 'Y': return (int)ScanCode.KEY_Y;
                case 'Z': return (int)ScanCode.KEY_Z;
                default:
                    throw new Exception("Unrecognized letter or number");
            }
        }

        /// <summary>
        /// Returns the scan keycode of one of the numbers on the numpad.
        /// </summary>
        /// <param name="letter"></param>
        /// <returns></returns>
        public static int NumpadToKey(char letter)
        {
            switch (letter)
            {
                case '0': return (int)ScanCode.NUMPAD_0;
                case '1': return (int)ScanCode.NUMPAD_1;
                case '2': return (int)ScanCode.NUMPAD_2;
                case '3': return (int)ScanCode.NUMPAD_3;
                case '4': return (int)ScanCode.NUMPAD_4;
                case '5': return (int)ScanCode.NUMPAD_5;
                case '6': return (int)ScanCode.NUMPAD_6;
                case '7': return (int)ScanCode.NUMPAD_7;
                case '8': return (int)ScanCode.NUMPAD_8;
                case '9': return (int)ScanCode.NUMPAD_9;
                default:
                    throw new Exception("Invalid numpad key");
            }            
        }

        /// <summary>
        /// Start pressing down on a key. Sends a "keydown" event to the game.
        /// </summary>
        /// <param name="key">The scan code (NOT the VK_ keycode!) of the key to press.</param>
        public void Keydown(int key)
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
        public void Keyup(int key)
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
        public void Tap(int key)
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
    }
}
