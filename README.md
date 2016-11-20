# Take the pain out of travelling

Demo: https://youtu.be/k5QqXoVuOv8

Build/Run:
Open EDAP.sln with Visual Studio Community 2015 and press F5.

Required settings:

1. Resolution is hardcoded to 1920x1080 (changing this is gonna mean changing lots of numbers and template images)
2. Executable is hardcoded as the 64-bit version
3. The colours looked for are (if you've changed your HUD colours, these things won't work):
  1. Compass calibration: red channel
  2. Compass dot: blue channel
  3. Triquadrant target: bright yellow/orange (I don't think you can change this)
  4. Saf Diseng: blue channel
4. Disable GUI effects (the animation when you open a side panel, speeds up panel opening)
5. Interface brightness should be set to three pips below max (I don't know how much this matters though, there is some leeway in most of the detectors)

To see the key mappings used you will need to read the code, see Pilot.cs. You can (obviously) change them before build/run. The numpad is used for orientation; F, P, X are various throttle, etc.

This project uses OpenCVSharp, which should be downloaded automatically when you try to build the project. (The Nuget package management system is part of Visual Studio.)

# Watch out

Things that may kill you if you leave this running unattended include, but are not limited to:

1. int er dicts
2. binaries
