# Take the pain out of travelling

Demo: https://youtu.be/k5QqXoVuOv8

Build/Run:
1. Run EDAP.exe (after Elite starts so that it appears on top).

Required settings:

0. Key bindings. Edit EDAP.exe.config with your key bindings. A list of recognized keys is at the end of this document. 
1. Resolution is hardcoded to 1920x1080 (changing this is gonna mean changing lots of numbers and template images)
2. Executable is hardcoded as the 64-bit version
3. The colours looked for are (if you've changed your HUD colours, these things won't work):
  1. Compass calibration: red channel
  2. Compass dot: blue channel
  3. Triquadrant target: bright yellow/orange (I don't think you can change this)
  4. Saf Diseng: blue channel
4. Disable GUI effects (the animation when you open a side panel, speeds up panel opening)
5. Interface brightness should be set to three pips below max (I don't know how much this matters though, there is some leeway in the detectors)

This project uses OpenCVSharp.

# Algorithm

See Pilot.cs. tldr: 

1. press jump, throttle 100%
2. wait 30 seconds then throttle 0%
3. wait until centre of screen goes bright (star appears -- (1)) or compass stops moving (non-sequence star -- (2))
  1. 50% throttle, pitch up if IMPACT warning is displayed, wait 20 seconds (scoop)
  2. select star, use compass to point away from star
4. select next destination, use compass to point at it
5. go to step 1 until jump counter is 0
6. set throttle to 75%, point at target continuously
7. if "safe disengage" is displayed, press docking key sequence (disengage, wait, boost, 0% throttle, request docking)

# Watch out

Things that may kill you if you leave this running unattended include:

1. interdiction (if you use this in the bubble, keep an eye on it)
2. binaries / multiple stars (it will pitch up to avoid collisions while scooping but otherwise takes no evasive measures)
3. white dwarfs, black holes, and neutron stars (it won't try to scoop these but may still hit the cones)
4. overheating near big stars -- the 20-second scoop wait is not long enough for some stars, it will start charging to jump too early
5. bugs / bad code -- i've tested it fairly extensively but it may stil have weird shit
6. aliens

# Recognized keys for config file

ALT_L
CTRL_L
ENTER
ESC
F1 
F10
F11
F12
F2 
F3 
F4 
F5 
F6 
F7 
F8 
F9 
KEY_0
KEY_1
KEY_2
KEY_3
KEY_4
KEY_5
KEY_6
KEY_7
KEY_8
KEY_9
KEY_A
KEY_APOSTROPHE
KEY_B
KEY_BACKQUOTE
KEY_BACKSLASH
KEY_BACKSPACE
KEY_C
KEY_CAPS_LOCK
KEY_COMMA
KEY_D
KEY_DOT
KEY_E
KEY_EQUALS
KEY_F
KEY_G
KEY_H
KEY_I
KEY_J
KEY_K
KEY_L
KEY_LEFTSQUAREBRACKET
KEY_M
KEY_MINUS
KEY_N
KEY_O
KEY_P
KEY_Q
KEY_R
KEY_RIGHTSQUAREBRACKET
KEY_S
KEY_SEMICOLON
KEY_SLASH
KEY_T
KEY_U
KEY_V
KEY_W
KEY_X
KEY_Y
KEY_Z
NUMPAD_0
NUMPAD_1
NUMPAD_2
NUMPAD_3
NUMPAD_4
NUMPAD_5
NUMPAD_6
NUMPAD_7
NUMPAD_8
NUMPAD_9
NUMPAD_DOT
NUMPAD_MINUS
NUMPAD_PLUS
SHIFT_L
SHIFT_R
SPACEBAR
TAB
