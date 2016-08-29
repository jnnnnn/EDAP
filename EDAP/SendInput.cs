using System;
using System.Runtime.InteropServices;

/// <summary>
/// These structs and dll calls allow us to tell Windows which keyboard keys are pressed
/// </summary>
namespace EDAP.SendInput
{    
    class SendInputWrapper
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInputStructure);
        
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public static void SendInput(INPUT input)
        {
            SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type;
        public MOUSEKEYBDHARDWAREINPUT Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public HARDWAREINPUT Hardware;
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
    
    public enum ScanCode : ushort
    {
        // from http://www.philipstorr.id.au/pcbook/book3/scancode.htm
        // see also https://www.win.tue.nl/~aeb/linux/kbd/scancodes-1.html

        // 29 	` 	~ 	
        KEY_1 = 0x02, // ! 	
        KEY_2 = 0x03, // @
        KEY_3 = 0x04, // # 	
        KEY_4 = 0x05, // $ 	
        KEY_5 = 0x06, // %
        KEY_6 = 0x07, // ^ 	
        KEY_7 = 0x08, // & 	
        KEY_8 = 0x09, // *
        KEY_9 = 0x0A, // ( 	
        KEY_0 = 0x0B, // ) 	
        // 0C 	- 	_
        // 0D 	= 	+ 	
        // 0E 	Backspace 	Backspace 	
        TAB = 0x0F, // Back Tab
        KEY_Q = 0x10,
        KEY_W = 0x11,
        KEY_E = 0x12,
        KEY_R = 0x13,
        KEY_T = 0x14,
        KEY_Y = 0x15,
        KEY_U = 0x16,
        KEY_I = 0x17,
        KEY_O = 0x18,
        KEY_P = 0x19,
        // 1A 	[ 	{ 	
        // 1B 	] 	}
        // 2B 	\ 	| 	
        // 3A note 1 	Caps Lock 	na 	
        KEY_A = 0x1E,
        KEY_S = 0x1F,
        KEY_D = 0x20,
        KEY_F = 0x21,
        KEY_G = 0x22,
        KEY_H = 0x23,
        KEY_J = 0x24,
        KEY_K = 0x25,
        KEY_L = 0x26,
        // 27 	; 	:
        // 28 	' 	" 	
        // 2B note 2 	# 	~ 	
        ENTER = 0x1C, // Enter
        // 2A note 1 	Left Shift 	na 	
        // D5 note 2 	\ 	| 	
        KEY_Z = 0x2C,
        KEY_X = 0x2D,
        KEY_C = 0x2E,
        KEY_V = 0x2F,
        KEY_B = 0x30,
        KEY_N = 0x31,
        KEY_M = 0x32,
        // 33 	, 	< 	
        // 34 	. 	> 	
        // 35 	/ 	?
        // 36 note 1 	Right shift 	na 	
        // 1D note 1 	Left Ctrl 	na 	
        // 38 note 1 	Left Alt 	na
        SPACEBAR = 0x39, // Spacebar
        // E0,38 note 1 	Right Alt 	na 	
        // E0,1D note 1 	Right Ctrl 	na
        // E0,52 	Insert 	na 	
        // E0,53 	Delete 	na 	
        // E0,4B 	Left Arrow 	na
        // E0,47 	Home 	na 	
        // E0,4F 	End 	na 	
        // E0,48 	Up Arrow 	na
        // E0,49 	Pg Up 	na 	
        // E0,51 	Pg Dn 	na 	
        // E0,4D 	Right Arrow 	na
        // 45,C5 note 1 	Num Lock 	na 	
        NUMPAD_7 = 0x47, // Home 	
        NUMPAD_4 = 0x4B, // Left Arrow
        NUMPAD_1 = 0x4F, // End 	
        // E0,35 	Keypad / 	Keypad / 	
        NUMPAD_8 = 0x48, // Up Arrow
        NUMPAD_5 = 0x4C, // na 	
        NUMPAD_2 = 0x50, // Dn Arrow 	
        NUMPAD_0 = 0x52, // Insert
        // E0,37 	Keypad * 	Keypad * 	
        NUMPAD_9 = 0x49, // Pg Up 	
        NUMPAD_6 = 0x4D, // Right Arrow
        NUMPAD_3 = 0x51, // Pg Dn 	
        // 53 	Keypad . 	Delete 	
        // 4A 	Keypad - 	Keypad -
        // 4E 	Keypad + 	Keypad + 	
        // E0,1C 	Keypad Enter 	Keypad Enter 	
        // 01 	Escape 	Escape
        // 3B 	F1 	note 3 	
        // 3C 	F2 	note 3 	
        // 3D 	F3 	note 3
        // 3E 	F4 	note 3 	
        // 3F 	F5 	note 3 	
        // 40 	F6 	note 3
        // 41 	F7 	note 3 	
        // 42 	F8 	note 3 	
        // 43 	F9 	note 3
        // 44 	F10 	note 3 	
        // D9 	F11 	note 3 	
        // DA 	F12 	note 3
        // 2A,37 	Prnt, Scrn 	na 	
        // 46 	Scroll Lock 	na 	
    }

    public enum VKeyCode : ushort
    {
        ADD = 0x6b,
        ALT = 0x12,
        BACKSPACE = 0x8,
        BROWSER_BACK = 0xa6,
        BROWSER_FAVORITES = 0xab,
        BROWSER_FORWARD = 0xa7,
        BROWSER_HOME = 0xac,
        BROWSER_REFRESH = 0xa8,
        BROWSER_SEARCH = 0xaa,
        BROWSER_STOP = 0xa9,
        CANCEL = 0x3,
        CAPS_LOCK = 0x14,
        CONTROL = 0x11,
        DECIMAL = 0x6e,
        DELETE = 0x2e,
        DIVIDE = 0x6f,
        DOWN = 0x28,
        END = 0x23,
        ENTER = 0xd,
        ESC = 0x1b,
        F1 = 0x70,
        F10 = 0x79,
        F11 = 0x7a,
        F12 = 0x7b,
        F13 = 0x7c,
        F14 = 0x7d,
        F15 = 0x7e,
        F16 = 0x7f,
        F17 = 0x80,
        F18 = 0x81,
        F19 = 0x82,
        F2 = 0x71,
        F20 = 0x83,
        F21 = 0x84,
        F22 = 0x85,
        F23 = 0x86,
        F24 = 0x87,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        HOME = 0x24,
        INSERT = 0x2d,
        KEY_0 = 0x30,
        KEY_1 = 0x31,
        KEY_2 = 0x32,
        KEY_3 = 0x33,
        KEY_4 = 0x34,
        KEY_5 = 0x35,
        KEY_6 = 0x36,
        KEY_7 = 0x37,
        KEY_8 = 0x38,
        KEY_9 = 0x39,
        KEY_A = 0x41,
        KEY_B = 0x42,
        KEY_C = 0x43,
        KEY_D = 0x44,
        KEY_E = 0x45,
        KEY_F = 0x46,
        KEY_G = 0x47,
        KEY_H = 0x48,
        KEY_I = 0x49,
        KEY_J = 0x4a,
        KEY_K = 0x4b,
        KEY_L = 0x4c,
        KEY_M = 0x4d,
        KEY_N = 0x4e,
        KEY_O = 0x4f,
        KEY_P = 0x50,
        KEY_Q = 0x51,
        KEY_R = 0x52,
        KEY_S = 0x53,
        KEY_T = 0x54,
        KEY_U = 0x55,
        KEY_V = 0x56,
        KEY_W = 0x57,
        KEY_X = 0x58,
        KEY_Y = 0x59,
        KEY_Z = 0x5a,
        LAUNCH_APP1 = 0xb6,
        LAUNCH_APP2 = 0xb7,
        LAUNCH_MAIL = 0xb4,
        LAUNCH_MEDIA_SELECT = 0xb5,
        LCONTROL = 0xa2,
        LEFT = 0x25,
        LSHIFT = 0xa0,
        LWIN = 0x5b,
        MEDIA_NEXT_TRACK = 0xb0,
        MEDIA_PLAY_PAUSE = 0xb3,
        MEDIA_PREV_TRACK = 0xb1,
        MEDIA_STOP = 0xb2,
        MULTIPLY = 0x6a,
        NUMLOCK = 0x90,
        NUMPAD0 = 0x60,
        NUMPAD1 = 0x61,
        NUMPAD2 = 0x62,
        NUMPAD3 = 0x63,
        NUMPAD4 = 0x64,
        NUMPAD5 = 0x65,
        NUMPAD6 = 0x66,
        NUMPAD7 = 0x67,
        NUMPAD8 = 0x68,
        NUMPAD9 = 0x69,
        OEM_1 = 0xba,
        OEM_102 = 0xe2,
        OEM_2 = 0xbf,
        OEM_3 = 0xc0,
        OEM_4 = 0xdb,
        OEM_5 = 0xdc,
        OEM_6 = 0xdd,
        OEM_7 = 0xde,
        OEM_8 = 0xdf,
        OEM_CLEAR = 0xfe,
        OEM_COMMA = 0xbc,
        OEM_MINUS = 0xbd,
        OEM_PERIOD = 0xbe,
        OEM_PLUS = 0xbb,
        PAGE_UP = 0x21,
        PAGEDOWN = 0x22,
        RCONTROL = 0xa3,
        RIGHT = 0x27,
        RightClick = 0x5d,
        RSHIFT = 0xa1,
        RWIN = 0x5c,
        SHIFT = 0x10,
        SNAPSHOT = 0x2c,
        SPACE_BAR = 0x20,
        SUBTRACT = 0x6d,
        TAB = 0x9,
        UP = 0x26,
        VOLUME_DOWN = 0xae,
        VOLUME_MUTE = 0xad,
        VOLUME_UP = 0xaf,
    }
    
    [Flags]
    internal enum KeyboardFlag : uint
    {
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        ScanCode = 0x0008,
    }

    internal enum InputType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        Hardware = 2,
    }
}
