using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

        const uint WM_KEYDOWN = 0x100;
        const uint WM_KEYUP = 0x101;
        const uint WM_SYSCOMMAND = 0x018;
        const uint SC_CLOSE = 0x053;

        const uint VK_A = 0x41;
        const uint VK_SPACE = 0x20;
        const uint VK_0 = 0x30;
        const uint VK_NUMPAD0 = 0x60;
        const uint VK_SHIFT = 0x10;
        const uint VK_CONTROL = 0x11;
        const uint VK_ALT = 0x12;

        public Keyboard()
        {
            pressed_keys = new HashSet<int>();
        }

        /// <summary>
        /// Returns the VK_ keycode of a given letter or number on the keyboard.
        /// </summary>
        public static int LetterToKey(char letter)
        {
            if (letter >= 'A' && letter <= 'Z')
                return (int)letter;
            if (letter >= '0' && letter <= '9')
                return (int)letter;

            throw new Exception("Unrecognized letter or number");
        }

        /// <summary>
        /// Returns the VK_ keycode of one of the numbers on the numpad.
        /// </summary>
        /// <param name="letter"></param>
        /// <returns></returns>
        public static int NumpadToKey(char letter)
        {
            if (letter >= '0' && letter <= '9')
                return (int)VK_NUMPAD0 + ((int)letter - (int)VK_0);

            throw new Exception("Invalid numpad key");
        }

        /// <summary>
        /// Start pressing down on a key. Sends a "keydown" event to the game.
        /// </summary>
        /// <param name="key">The VK_ keycode of the key to press.</param>
        public void Keydown(int key)
        {
            if (pressed_keys.Contains(key))
                return;

            pressed_keys.Add(key);
            PostMessage(hWnd, WM_KEYDOWN, ((IntPtr)key), (IntPtr)0);
        }

        /// <summary>
        /// Immediately release a key.
        /// </summary>
        /// <param name="key">The VK_ keycode of the key to release.</param>
        public void Keyup(int key)
        {
            if (!pressed_keys.Contains(key))
                return;

            pressed_keys.Remove(key);
            PostMessage(hWnd, WM_KEYUP, ((IntPtr)key), (IntPtr)0);
        }

        /// <summary>
        /// Send a keydown and then, soon after, a keyup, event to the game.
        /// </summary>
        /// <param name="key">The key to tap on.</param>
        public void Tap(int key)
        {
            Keydown(key);
            Keyup(key);
        }

        /// <summary>
        /// Release all pressed keys immediately.
        /// </summary>
        public void Clear()
        {
            foreach (int key in pressed_keys)
                Keyup(key);
        }                
    }
}
