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

To see the key mappings used you will need to read the code, see Pilot.cs. You can change them when you build it. The numpad is used for orientation; F, P, X are various throttle, etc.

This project uses OpenCVSharp, which should be downloaded automatically when you try to build the project.