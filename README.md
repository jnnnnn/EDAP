# Take the pain out of travelling

Build/Run:
Open EDAP.sln with Visual Studio Community 2015 and press F5.

Required settings:

1. Resolution is hardcoded to 1920x1080
2. Executable is hardcoded as the 64-bit version
3. The colours looked for are:
  1. Compass calibration: red channel
  2. Compass dot: blue channel
  3. Triquadrant target: bright yellow/orange (I don't think you can change this)
  4. Saf Diseng: blue channel

To see the key mappings used you will need to read the code, see Pilot.cs. You can (obviously) change them before build/run. The numpad is used for orientation; F, P, X are various throttle, etc.

This project uses OpenCVSharp, which should be downloaded automatically when you try to build the project.

# Watch out

Things that may kill you if you leave this running unattended include, but are not limited to:

1. int er dicts
2. binaries
3. less than 5s or more than 20s in the system loading screen
4. brown system centres seem to be easy to crash into (sorry)
5. running out of hydrogen
