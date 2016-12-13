# Take the pain out of travelling

Demo: https://youtu.be/k5QqXoVuOv8

Instructions:
1. click Releases, above, 
2. download the archive (it's about 8 MB because of the opencv dll) 
3. modify the key bindings in EDAP.exe.config (or change your in-game bindings to match)
3. run EDAP.exe

Required settings:

0. Key bindings (see EDAP.exe.config)
1. Resolution is hardcoded to 1920x1080 (changing this is gonna mean changing lots of numbers and template images)
2. Executable is hardcoded as the 64-bit version
3. The colours looked for are (if you've changed your HUD colours, these things won't work):
  1. Compass calibration: red channel
  2. Compass dot: blue channel
  3. Triquadrant target: bright yellow/orange (I don't think you can change this)
  4. Saf Diseng: blue channel
4. Disable GUI effects (the animation when you open a side panel, speeds up panel opening)
5. Interface brightness should be set to three pips below max (I don't know how much this matters though, there is some leeway in the detectors)

This project uses OpenCVSharp, which should be downloaded automatically when you try to build the project. (The Nuget package management system is part of Visual Studio.)

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
