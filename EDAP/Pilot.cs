using EDAP.SendInput;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EDAP
{
    /// <summary>
    /// This was originally a very simple controller. It waits a certain amount of time, then starts aligning to the compass. 
    /// When it is aligned, it presses "G". Then the cycle starts again.
    /// Once the count of jumps remaining reaches zero, it will cruise to the destination, and try to initiate an auto-dock.
    /// If cruise is not selected, it will aim at the star and cut throttle (waiting for a manual fuel scooping procedure because I haven't implemented OCR).
    /// 
    /// </summary>
    class PilotJumper
    {
        private DateTime last_jump_time;  // time at which the jump key was (most recently) pressed
        private DateTime last_faceplant_time; // time at which the last faceplant occurred
        private DateTime lastClear = DateTime.UtcNow.AddHours(-1);
        private DateTime scoopStart = DateTime.UtcNow.AddHours(-1);
        public Keyboard keyboard;
        private int jumps_remaining = 0;
        private uint alignFrames;
        public const int TIMERINTERVAL_MS = 100;
        public string status = "";

        private ScanCode keyThrottle0 = parseKeyBinding(Properties.Settings.Default.keyThrottle0);
        private ScanCode keyThrottle50 = parseKeyBinding(Properties.Settings.Default.keyThrottle50);
        private ScanCode keyThrottleReduce25 = parseKeyBinding(Properties.Settings.Default.keyThrottleReduce25);
        private ScanCode keyThrottle100 = parseKeyBinding(Properties.Settings.Default.keyThrottle100);
        private ScanCode keyBoost = parseKeyBinding(Properties.Settings.Default.keyBoost);
        private ScanCode keyNextDestination = parseKeyBinding(Properties.Settings.Default.keyNextDestination);
        private ScanCode keyFire1 = parseKeyBinding(Properties.Settings.Default.keyFire1);
        private ScanCode keyHyperspace = parseKeyBinding(Properties.Settings.Default.keyHyperspace);
        private ScanCode keySuperCruise = parseKeyBinding(Properties.Settings.Default.keySuperCruise);

        private ScanCode keyNavMenu = parseKeyBinding(Properties.Settings.Default.keyNavMenu);
        private ScanCode keyRight = parseKeyBinding(Properties.Settings.Default.keyRight);
        private ScanCode keySelect = parseKeyBinding(Properties.Settings.Default.keySelect);
        private ScanCode keyMenuTabRight = parseKeyBinding(Properties.Settings.Default.keyMenuTabRight);
        private ScanCode keyDown = parseKeyBinding(Properties.Settings.Default.keyDown);
        private ScanCode keyUp = parseKeyBinding(Properties.Settings.Default.keyUp);
        private ScanCode keySystemMap = parseKeyBinding(Properties.Settings.Default.keySystemMap);
        private ScanCode keySysMapScrollRight = parseKeyBinding(Properties.Settings.Default.keySysMapScrollRight);
        private ScanCode keyScreenshot = parseKeyBinding(Properties.Settings.Default.keyScreenshot);

        private ScanCode keyRollLeft = parseKeyBinding(Properties.Settings.Default.keyRollLeft);
        private ScanCode keyRollRight = parseKeyBinding(Properties.Settings.Default.keyRollRight);
        private ScanCode keyPitchUp = parseKeyBinding(Properties.Settings.Default.keyPitchUp);
        private ScanCode keyPitchDown = parseKeyBinding(Properties.Settings.Default.keyPitchDown);
        private ScanCode keyYawLeft = parseKeyBinding(Properties.Settings.Default.keyYawLeft);
        private ScanCode keyYawRight = parseKeyBinding(Properties.Settings.Default.keyYawRight);

        static private ScanCode parseKeyBinding(string s)
        {
            return (ScanCode)Enum.Parse(typeof(ScanCode), s);
        }

        [Flags]
        public enum PilotState
        {
            None = 0,
            firstjump = 1 << 0, // is this the first jump? (skip waiting and swooping and go straight to aligning)
            clearedJump = 1 << 1, // at the start of the jump-loop process, we need to reset everything
            jumpCharge = 1 << 2, // have we started charging for the jump yet
            swoopStart = 1 << 3, // have we set the throttle to 50% at the start of the swoop
            swoopEnd = 1 << 4, // have we finished turning away from the star
            cruiseStart = 1 << 5, // have we set throttle to 75% to start cruise at destination
            AwayFromStar = 1 << 6, // have we finished pointing away from the star
            SelectStar = 1 << 7, // have we selected the star yet (so we can point away from it)
            SysMap = 1 << 8, // whether to open the system map after jumping
            Cruise = 1 << 9, // whether to aim at the target once the jump count reaches zero
            CruiseEnd = 1 << 10, // whether we have pressed "safe disengage"
            Honk = 1 << 11, // fire discovery scanner when arriving in system?
            Enabled = 1 << 12, // is the pilot enabled?
            Faceplant = 1 << 13, // have we faceplanted the star yet
            ScoopStart = 1 << 14, // have we done the initial setup for scooping yet
            ScoopMiddle = 1 << 15, // are we nearly done with scooping yet
            ScoopDeactive = 1 << 16, // has our scoop turned off yet
            scoopComplete = 1 << 17, // have we finished scooping yet
            Scoop = 1 << 18, // do we want to scoop at each star?
            HonkComplete = 1 << 19, // have we fired the discovery scanner yet
            SkipThisScoop = 1 << 20, // we want to skip scooping for this jump
            DisengageStarted = 1 << 21, // have we pressed the Safe Disengage key?
        }

        public PilotState state;
        public Screenshot screen;
        internal CompassSensor compassRecognizer;
        internal CruiseSensor cruiseSensor;

        double SecondsSinceLastJump { get { return (DateTime.UtcNow - last_jump_time).TotalSeconds; } }
        double SecondsSinceFaceplant {  get { return (DateTime.UtcNow - last_faceplant_time).TotalSeconds; } }
        double SecondsUntilScoopComplete;

        public PilotJumper()
        {
            state = PilotState.Scoop | PilotState.Honk | PilotState.Cruise; // these modes are enabled by default, but user can disable
        }

        public void Reset(bool soft)
        {
            // soft reset (after every jump)
            last_faceplant_time = DateTime.UtcNow.AddHours(-1);
            SecondsUntilScoopComplete = 0.0;
            state &= PilotState.Enabled | PilotState.SysMap | PilotState.Cruise | PilotState.Honk | PilotState.Scoop; // clear per-jump flags
            if (soft)
                return;

            // hard reset (when user activates autopilot)            
            state |= PilotState.firstjump;
            state |= PilotState.Faceplant; // no need to dodge the star until we have initiated a jump -- stops cruising from trying to dodge a star
            alignFrames = 0;
            last_jump_time = DateTime.UtcNow.AddHours(-1);
        }

        public int Jumps
        {
            get { return jumps_remaining; }
            set { jumps_remaining = value; }
        }

        private bool OncePerJump(PilotState flag)
        {
            if (!state.HasFlag(flag))
            {
                state |= flag;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Handle an input frame by setting which keys are pressed.
        /// </summary>        
        public void Act()
        {
            status = "";

            if (!state.HasFlag(PilotState.Enabled))
            {
                Idle();
                return;
            }

            // perform the first alignment/jump immediately
            if (state.HasFlag(PilotState.firstjump) && jumps_remaining > 0)
            {
                StartJump();
                if (AlignTarget())
                    Jump();
                return;
            }

            // charging frendship drive (15s) / countdown (5s) / loading screen (~14-16s)
            if (SecondsSinceLastJump < 30)
            {
                status = string.Format("Waiting for jump, {0:0.0}", SecondsSinceLastJump);
                return;
            }

            // just in case, we should make sure no keys have been forgotten about
            if (OncePerJump(PilotState.clearedJump))
            {
                keyboard.Tap(keyThrottle0); // cut throttle
                keyboard.Clear();
            }

            // wait until we hit the star at the end of the loading screen (up to 100 seconds)
            if (!state.HasFlag(PilotState.Faceplant))
            {
                bool stationaryCompass = compassRecognizer.DetectStationaryCompass();
                if (stationaryCompass || compassRecognizer.MatchFaceplant() || SecondsSinceLastJump > 100)
                {
                    state |= PilotState.Faceplant;
                    last_faceplant_time = DateTime.UtcNow;
                    if (stationaryCompass)
                    {
                        // if the jump ended without detecting a star, it's probably a neutron star or black hole, so skip fuel scooping.
                        state |= PilotState.SkipThisScoop;
                    }
                }
                else
                {
                    status = string.Format("Waiting for jump end, {0:0.0}", SecondsSinceLastJump);
                    return; // keep waiting
                }
                    
            }

            // don't do anything for a second after faceplant detection because the game sometimes doesn't register inputs 
            if (SecondsSinceFaceplant < 1)
                return;
            
            if (state.HasFlag(PilotState.Honk) && OncePerJump(PilotState.HonkComplete))
            {
                keyboard.Keydown(keyFire1); // hooooooooooooonk
                Task.Delay(10000).ContinueWith(t => keyboard.Keyup(keyFire1)); // stop honking after ten seconds
                return;
            }
                        
            // If we've finished jumping and are not cruising, just stop and point at the star (scan and makes scooping easier).
            if (jumps_remaining < 1 && !state.HasFlag(PilotState.Cruise))
            {                      
                if (OncePerJump(PilotState.swoopEnd))
                    keyboard.Tap(keyThrottle0); // cut throttle

                if (OncePerJump(PilotState.SelectStar))
                    SelectStar();

                AlignTarget();
                return;
            }

            if (!state.HasFlag(PilotState.scoopComplete))
            {
                if (state.HasFlag(PilotState.Scoop)
                && !state.HasFlag(PilotState.SkipThisScoop)
                && jumps_remaining > 0)
                {
                    Scoop();
                    return;
                }
                else
                {
                    // swoop a bit more if last jump because slow ship kept hitting star
                    if (SecondsSinceFaceplant < 30)
                    {
                        Swoop();
                        return;
                    }
                }
            }
            
            // make sure we are travelling away from the star so that even if our next jump is directly behind it our turn will parallax it out of the way.
            // don't do it for the supcruz at the end because we can't reselect the in-system destination with the "N" key.
            if (!state.HasFlag(PilotState.AwayFromStar) && jumps_remaining > 0)
            {
                if (OncePerJump(PilotState.SelectStar))                
                    SelectStar();
                
                // 10 because we want to make sure the honk finishes before opening the system map
                if (AntiAlign() && SecondsSinceFaceplant > 10)
                {
                    state |= PilotState.AwayFromStar;
                    keyboard.Tap(keyNextDestination);
                    keyboard.Tap(keyThrottle100);
                }

                return;
            }

            // okay, by this point we are cruising away from the star and are ready to align and jump. We can't start 
            // charging to jump until 10 seconds after witchspace ends, but we can start aligning.

            if (jumps_remaining < 1 && state.HasFlag(PilotState.Cruise))
            {
                AlignTarget();
                Cruise();
            }
            else if (jumps_remaining > 0)
            {                
                // start charging before we are aligned (saves time)
                if (!state.HasFlag(PilotState.SysMap))
                    StartJump();

                if (AlignTarget())
                    Jump(); // do everything else
            }
        }

        private DateTime lastChargeTime = DateTime.UtcNow;
        private void StartJump()
        {
            if (OncePerJump(PilotState.jumpCharge))
            {
                keyboard.Tap(keyHyperspace); // jump (frameshift drive charging)
                lastChargeTime = DateTime.UtcNow;
            }
        }

        // select star
        private bool SelectStar()
        {
            keyboard.TapWait(keyNavMenu); // nav menu            
            keyboard.TapWait(keyRight); // right to select the list of stars

            //Initial case: It's the first thing under the cursor
            keyboard.TapWait(keySelect); // open menu            
            keyboard.Tap(keySelect); // select the object
            //keyboard.Tap(keyNavMenu); // close nav menu

            //:toot:
            if (cruiseSensor.CurrentLocationLocked())
            {
                keyboard.Tap(keyNavMenu);
                return true;
            }

            //Most initial failure cases seem to involve the current location being above the default
            keyboard.TapWait(keyUp); // Jump back up the list
            keyboard.TapWait(keySelect); // open menu            
            keyboard.Tap(keySelect); // select the object
            //keyboard.Tap(keyNavMenu); // close nav menu
            
            screen.ClearSaved();
            if (cruiseSensor.CurrentLocationLocked())
            {
                keyboard.Tap(keyNavMenu);
                return true;
            }

            //Bounce down two so we're past the original default
            keyboard.TapWait(keyDown);
            keyboard.TapWait(keyDown);

            for (int i=0; i<5; i++)
            {
                keyboard.TapWait(keySelect); // open menu            
                keyboard.Tap(keySelect); // select the object
                screen.ClearSaved();
                if (cruiseSensor.CurrentLocationLocked()) //Check to see if the blue "current location" icon is now hidden
                {
                    keyboard.Tap(keyNavMenu);
                    return true;
                }
                keyboard.TapWait(keyDown);
            }

            //Wehoops
            //keyboard.Tap(keyNavMenu); // close nav menu
            return false;
        }

        private void Jump()
        {
            ClearAlignKeys();
            StartJump();
            last_jump_time = lastChargeTime;
            
            // If it took us a long time to align, fix the timer
            if ((DateTime.UtcNow - last_jump_time).TotalSeconds > 15)
                last_jump_time = DateTime.UtcNow.AddSeconds(-15);

            keyboard.Tap(keyThrottle100); // full throttle
            jumps_remaining -= 1;

            // reset everything
            Reset(soft: true);

            if (state.HasFlag(PilotState.SysMap))
            {
                keyboard.Tap(keySystemMap); // open system map
                Task.Delay(6000).ContinueWith(t => keyboard.Keydown(keySysMapScrollRight)); // scroll right on system map
                Task.Delay(7000).ContinueWith(t => keyboard.Keyup(keySysMapScrollRight));
                Task.Delay(10000).ContinueWith(t => keyboard.Tap(keyScreenshot)); // screenshot the system map                
            }

            if (jumps_remaining < 1)
            {
                Sounds.PlayOneOf("this is the last jump.mp3", "once more with feeling.mp3", "one jump remaining.mp3");                
            }
        }

        /// <summary>
        /// Run the recognition stuff but don't press any keys
        /// </summary>
        internal void Idle()
        {
            try
            {
                cruiseSensor.FindTriQuadrant(screen.ScreenCentre(diameter:300));
                return;
            }
            catch (Exception e)
            {
                status = e.Message;
            }
            
            Point2f compass;
            try
            {
                compass = compassRecognizer.GetOrientation();
                status = string.Format("{0:0.0}, {1:0.0}\n", compass.X, compass.Y) + status;
            }
            catch (Exception e)
            {
                ClearAlignKeys();
                status = e.Message + "\n" + status;
                return;
            }
        }
        
        private void ClearAlignKeys()
        {
            keyboard.Keyup(keyRollLeft);
            keyboard.Keyup(keyRollRight);
            keyboard.Keyup(keyPitchUp);
            keyboard.Keyup(keyPitchDown);
            keyboard.Keyup(keyYawLeft);
            keyboard.Keyup(keyYawRight);
            lastClear = DateTime.UtcNow;
        }

        private bool AlignTarget()
        {
            try
            {
                Mat screenCentre = screen.ScreenCentre(diameter: 300);
                Point2f offset = cruiseSensor.FindTriQuadrant(screenCentre) - cruiseSensor.FindShipPointer(CruiseSensor.IsolateYellow(screenCentre));
                return FineAlign(-offset);
            }
            catch (Exception e)
            {
                status = e.Message;
            }
            AlignCompass();
            return false; // only fine align can confirm we are aligned to the target
        }

        /// <summary>
        /// Press whichever keys will make us point more towards the target.
        /// When rolling, try to keep the target down so that when we turn around the sun it doesn't shine on our compass.
        /// </summary>
        /// <param name="compass">The normalized vector pointing from the centre of the compass to the blue dot</param>
        /// <param name="target">The offset of the compass that we desire. Null for centered.</param>
        /// <param name="align_margin">0.15 is the default because 
        ///     more -- fine adjustment won't work because target not in view. 
        ///     less -- fine adjustment not fully utilized; noise from compass may cause problems
        /// </param>
        /// <param name="bPitchYaw">If this is false, roll the ship but don't pitch or yaw.</param>
        /// <returns>true if we are pointing at the target</returns>
        private bool AlignCompass(bool bPitchYaw=true, bool bRoll=true)
        {
            // see if we can find the compass
            Point2f compass;
            try
            {
                compass = compassRecognizer.GetOrientation();
                status = string.Format("{0:0.0}, {1:0.0}\n", compass.X, compass.Y) + status;
            }
            catch (Exception e)
            {
                Point2f? maybeOldCompass = compassRecognizer.GetLastGoodOrientation(ageSeconds: 1.0);
                if (maybeOldCompass.HasValue)
                {
                    // If we have an old value for the compass, use that. Rolling gets a bit crazy though so don't do that part.
                    bRoll = false;
                    compass = maybeOldCompass.Value;
                }
                else
                {
                    // otherwise just unpress everything and wait for a better compass reading
                    ClearAlignKeys();
                    alignFrames = 0;
                    status = e.Message + "\n" + status;
                    return false;
                }
            }

            // re-press keys regularly in case the game missed a keydown (maybe because it wasn't focused)
            if ((DateTime.UtcNow - lastClear).TotalSeconds > 1)
                ClearAlignKeys();
            
            // press whichever keys will point us toward the target. Coordinate system origin is bottom right
            const float align_margin = 0.15f;
            double xMargin = compass.Y > -0.1 ? 0.3 : 0.0; // always roll if target is above us
            keyboard.SetKeyState(keyRollRight, bRoll && compass.X < -xMargin); // roll right
            keyboard.SetKeyState(keyRollLeft, bRoll && compass.X > xMargin); // roll left
            
            keyboard.SetKeyState(keyPitchUp, bPitchYaw && compass.Y < -align_margin); // pitch up
            keyboard.SetKeyState(keyPitchDown, bPitchYaw && compass.Y > align_margin); // pitch down
            keyboard.SetKeyState(keyYawLeft, bPitchYaw && compass.X < -align_margin); // yaw left
            keyboard.SetKeyState(keyYawRight, bPitchYaw && compass.X > align_margin); // yaw right
            
            return (Math.Abs(compass.Y) < align_margin && Math.Abs(compass.X) < align_margin);
        }
        
        private List<Tuple<DateTime, Point2f>> finehistory = new List<Tuple<DateTime, Point2f>>();
        /// <summary>
        /// try to reduce the offset to within a threshold by pressing buttons
        /// <param name="offset">The offset (in pixels) that we want to reduce</param>
        /// </summary>
        private bool FineAlign(Point2f offset)
        {
            finehistory.Insert(0, new Tuple<DateTime, Point2f>(screen.timestamp_history[0], offset));
            if (finehistory.Count > 3)
                finehistory.RemoveAt(3);
            Point2f velocity = new Point2f();
            List<DateTime> ts = screen.timestamp_history;
            // if we have the two previous frames of finehistory, use that to estimate velocity
            if (finehistory.Count > 2 &&
                finehistory[0].Item1 == ts[0] &&
                finehistory[1].Item1 == ts[1] &&
                finehistory[2].Item1 == ts[2])
            {
                velocity.X = -(float)Controller.QuadFitFinalVelocity(finehistory[2].Item2.X, finehistory[1].Item2.X, finehistory[0].Item2.X, ts[2], ts[1], ts[0]);
                velocity.Y = -(float)Controller.QuadFitFinalVelocity(finehistory[2].Item2.Y, finehistory[1].Item2.Y, finehistory[0].Item2.Y, ts[2], ts[1], ts[0]);
                Console.WriteLine(string.Format("velocity: {0}; {1}", velocity, compassRecognizer.GetOrientationVelocity()));
            }
            else
            {
                // I tried to use the compass' velocity here instead of fine velocity but it was too 
                // noisy for a quadratic prediction, the fine and rough velocities were basically uncorrelated.
                // Instead, pause until the fine velocity prediction has enough frames.
                alignFrames = 0;
                ClearAlignKeys();
                return false;
            }

            const float deadzone = 20; // size of deadzone (in pixels)

            /* I've had a few goes at this. This algorithm predicts the effect of pressing a key, assumes constant acceleration while the key is pressed, and constant when released to stop at exactly the right spot. 
             * This is not quite accurate as:
             *  - the game will cut acceleration to 0 once we reach the maximum pitching speed
             *  - our measured initial velocity is probably going to be inaccurate due to sampling
             *  - acceleration is damped at low velocities, presumably to allow for fine adjustments. This is ok as we will just underestimate how long to press the key for.
             *  
             *  Measured pitch acceleration in supcruz was 1440px/s/s at 1080p up to a maximum pitch rate of 142px/s at optimal speed/throttle (75%) for a python on 2016-10-09. In normal space pitch acceleration can be > 5000px/s/s which is difficult to deal with because even the rough alignment overshoots.
            * 
            * The main thing is that we get closer to 0 fairly quickly without holding down the key for too long and causing oscillation.
            * 
            * We get t from constant acceleration for t seconds and then constant deceleration to v=0 at x=0. 
            * solve x + v * t + 0.5 * a * t^2 = -(v/a + t) * (v + at) + 0.5 * a * (v/a + t)^2 for t 
            * gives t = -v/a +/- 1/(a*2**.5) * (v*v - 2*a*x)**.5. 
            * (see constantaccel.py for a demonstration.)
            * 
            * todo: would be good to constantly refine aX and aY from previous key presses ( would also require quadratic fitting )
            * 
            * If you notice bouncing around or oscillation around the centre, increase aY and aX so the predictor knows how much effect pressing a key has.
            */
            double aY = 3000; // pitch acceleration while key pressed (deceleration while unpressed), in px/s/s. It doesn't hurt us much if this is high, just takes longer to get to the middle. Too low causes bouncing / oscillation.
            double vY = velocity.Y; // px/s
            double xY = offset.Y; // px

            if (vY * vY - 2 * aY * xY < 0)
                aY *= -1; // make sure sqrt is not imaginary
            double rootpartY = Math.Sqrt(0.5 * (vY * vY - 2 * aY * xY));
            double tY1 = -vY / aY + 1 / aY * rootpartY;
            double tY2 = -vY / aY - 1 / aY * rootpartY;
            double tY = Math.Max(tY1, tY2); // in seconds

            tY -= 0.02; // take off 20ms to stop overshooting

            if (offset.Y < -deadzone && aY > 0 && tY > 0.05 /* don't press a key for less than 50ms */)
                keyboard.TimedTap(keyPitchDown, (int)(tY * 1000)); // pitch down when offset.Y < 0
            else
                keyboard.Keyup(keyPitchDown);
            if (offset.Y > deadzone && aY < 0 && tY > 0.05)
                keyboard.TimedTap(keyPitchUp, (int)(tY * 1000)); // pitch up when offset.Y > 0
            else
                keyboard.Keyup(keyPitchUp);

            // now do it all again for the x axis

            double aX = 2000; // // yaw acceleration while key pressed (deceleration while unpressed), in px/s/s.
            double vX = velocity.X; // px/s
            double xX = offset.X; // px

            if (vX * vX - 2 * aX * xX < 0)
                aX *= -1;
            double rootpartX = Math.Sqrt(0.5 * (vX * vX - 2 * aX * xX));
            double tX1 = -vX / aX + 1 / aX * rootpartX;
            double tX2 = -vX / aX - 1 / aX * rootpartX;
            double tX = Math.Max(tX1, tX2); // s

            //status = string.Format("v = {0:0}, x = {1}, t = {2:0}", vX, xX, tX * 1000);
            if (offset.X < -deadzone && aX > 0 && tX > 0.05)
                keyboard.TimedTap(keyYawRight, (int)(tX * 1000)); // yaw right when offset.X < 0
            else
                keyboard.Keyup(keyYawRight);
            if (offset.X > deadzone && aX < 0 && tX > 0.05)
                keyboard.TimedTap(keyYawLeft, (int)(tX * 1000)); // yaw left when offset.X > 0
            else
                keyboard.Keyup(keyYawLeft);

            status = string.Format("{0:0}, {1:0}\n", offset.X, offset.Y);

            alignFrames = (Math.Abs(offset.X) < 50 && Math.Abs(offset.Y) < 50) ? alignFrames + 1 : 0;
            if (alignFrames > 2)
            {
                ClearAlignKeys();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Press whichever keys will make us point more away from the target.
        /// </summary>
        /// <param name="compass">The normalized vector pointing from the centre of the compass to the blue dot</param>
        /// <returns>true if we are pointing directly away from the target</returns>
        private bool AntiAlign()
        {
            Point2f compass;
            try
            {
                compass = compassRecognizer.GetOrientation();
                status = string.Format("{0:0.0}, {1:0.0}\n", compass.X, compass.Y);
            }
            catch (Exception e)
            {
                ClearAlignKeys();
                alignFrames = 0;
                status = e.Message;
                return false;
            }
                        
            // re-press keys regularly in case the game missed a keydown (maybe because it wasn't focused)
            if ((DateTime.UtcNow - lastClear).TotalSeconds > 1)
                ClearAlignKeys();

            keyboard.SetKeyState(keyRollLeft, compass.X > 0.3); // roll left
            keyboard.SetKeyState(keyRollRight, compass.X < -0.3); // roll right
            keyboard.SetKeyState(keyPitchUp, compass.Y > 0 && compass.Y < 1.9); // pitch up
            keyboard.SetKeyState(keyPitchDown, compass.Y < 0 && compass.Y > -1.9); // pitch down
            keyboard.SetKeyState(keyYawLeft, compass.X > 0.1); // yaw left
            keyboard.SetKeyState(keyYawRight, compass.X < -0.1); // yaw right

            // If we're scooping, make sure we're really antialigned because we're going closer to the star
            var margin = state.HasFlag(PilotState.Scoop) ? 0.8 : 0.8;

            // antialign doesn't need much accuracy... this will just stop accidental noise
            alignFrames = (Math.Abs(compass.X) < 0.2 && Math.Abs(compass.Y) > 2 - margin) ? alignFrames + 1 : 0;
            if (alignFrames > 3)
            {
                ClearAlignKeys();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// At the end of a jump we are always just about to crash into the star. FFS. Pitch up for 5-15 seconds 
        /// (depending on how long the loading screen took) at 50% throttle to avoid it.
        /// </summary>
        private void Swoop()
        {
            status += string.Format("Swoop wait + {0:0.0}\n", SecondsSinceFaceplant);

            if (SecondsSinceFaceplant < 2)
                return; 

            if (OncePerJump(PilotState.swoopStart))
            {
                keyboard.Tap(keyThrottle50); // set throttle to 50%
                keyboard.Tap(keyThrottleReduce25); // set throttle to 25%
                return;
            }

            // pitch up for ~5 seconds on arrival to avoid star.
            if (SecondsSinceFaceplant < 7)
            {
                keyboard.SetKeyState(keyPitchUp, true);
                return;
            }
            
            if (OncePerJump(PilotState.swoopEnd))
                keyboard.Tap(keyThrottle100);

            bool collisionImminent = cruiseSensor.MatchImpact() || compassRecognizer.MatchFaceplant();
            keyboard.SetKeyState(keyPitchUp, collisionImminent);
            return;
        }

        /// <summary>
        /// Fly close past the star
        /// </summary>
        private void Scoop()
        {
            int scoopWaitSeconds = Properties.Settings.Default.scoopWaitSeconds;
            int scoopFinishSeconds = Properties.Settings.Default.scoopFinishSeconds;

            //RIP-- WIP, I guess
            if (cruiseSensor.EmergencyDrop())
            {
                keyboard.Tap(keyThrottle0); 
                state &= ~PilotState.Enabled; // disable! we're fucked!
                Sounds.Play("oh fuck.mp3");
                return;
            }
           
            // if we try to select the star earlier than this, sometimes it selects the wrong thing
            if (SecondsSinceFaceplant < 2)
                return;

            // roll so that the star is below (makes pitch up quicker -- less likely to collide in slow ships)
            if (OncePerJump(PilotState.SelectStar))
            {
                if (!SelectStar())
                {
                    keyboard.TapWait(keyThrottle0); 
                    state &= ~PilotState.Enabled; // disable! we're fucked!
                    keyboard.TapWait(keySupercruise);
                    Sounds.Play("oh fuck.mp3");
                    return;
                }

            }
            AlignCompass(bPitchYaw: false);
                        
            //Set a value to confirm that we've actually started scooping
            if (OncePerJump(PilotState.ScoopStart)){
                keyboard.Tap(keyThrottle50); // 50% throttle
            }
            // (barely) avoid crashing into the star
            bool collisionImminent = cruiseSensor.MatchImpact() || compassRecognizer.MatchFaceplant();
            keyboard.SetKeyState(keyPitchUp, collisionImminent);

            // start speeding up towards the end so we don't crash/overheat
            // Keep the scoopwaitseconds as a "general idea" of when to start pulling up, at least until maybe a check for "full tank"
            if (SecondsSinceFaceplant > scoopWaitSeconds && OncePerJump(PilotState.ScoopMiddle))
                keyboard.Tap(keyThrottle100);

            //If the fueling is complete, we probably want to GTFO
            if (cruiseSensor.FuelComplete()) {
                if (OncePerJump(PilotState.ScoopMiddle)) {
                    keyboard.Tap(keyThrottle100);
                }

            }


            //If we've passed the expected wait point and the Scoop Active display is gone, flag appropriately and wait for scoopFinishSeconds to pass
            if (state.HasFlag(PilotState.ScoopMiddle) && !state.HasFlag(PilotState.ScoopDeactive) && !cruiseSensor.MatchScooping()) {
                SecondsUntilScoopComplete = SecondsSinceFaceplant + scoopFinishSeconds;
                Console.WriteLine(string.Format("Match scooping is not true at {0}", SecondsSinceFaceplant));
                OncePerJump(PilotState.ScoopDeactive);
                status += string.Format("Finalizing scoop + {0:0.0}\n", SecondsSinceFaceplant);


            //We're far enough away from the scoop range for heat to be a non-issue
            }else if (state.HasFlag(PilotState.ScoopDeactive) && SecondsSinceFaceplant > SecondsUntilScoopComplete) {
                state |= PilotState.scoopComplete;
                status += string.Format("Finalizing scoop + {0:0.0}\n", SecondsSinceFaceplant);
            }else{
                status += string.Format("Scoop wait + {0:0.0}\n", SecondsSinceFaceplant);
            }


        }

        /// <summary>
        /// This handles cruising behaviour. The main function is already keeping us aligned; 
        /// we just need to drop to 75% speed.
        /// This function could also do the final part: 
        ///  1. press G when the "SAFE DISENGAGE" graphic is detected
        ///  2. Wait 5 seconds
        ///  3. Press Tab, X, 1,E,E,Space,S,Space to boost, cut throttle, and request docking (so the docking computer takes over).
        /// </summary>
        private void Cruise()
        {
            if (SecondsSinceFaceplant > 20 && OncePerJump(PilotState.cruiseStart))
            {
                Sounds.Play("cruise mode engaged.mp3");
                keyboard.Tap(keyThrottle100); // full throttle
                keyboard.Tap(keyThrottleReduce25); // drop 25% throttle, to 75%
            }

            if (!state.HasFlag(PilotState.CruiseEnd) && cruiseSensor.MatchSafDisengag())
            {
                keyboard.Tap(keyHyperspace); // "Safe Disengage"
                state |= PilotState.DisengageStarted;
                return;
            }

            // disengage can take a while so wait for it to finish before continuing
            if (!state.HasFlag(PilotState.CruiseEnd) && 
                state.HasFlag(PilotState.DisengageStarted) && 
                !cruiseSensor.MatchSafDisengag())
            {                
                state |= PilotState.CruiseEnd;
                state &= ~PilotState.Enabled; // disable! we've arrived!
                // these commands will initiate docking if we have a computer
                Task.Delay(1000).ContinueWith(t => keyboard.Tap(keyBoost)); // boost
                Task.Delay(5000).ContinueWith(t => keyboard.Tap(keyThrottle0)); // cut throttle
                Task.Delay(8000).ContinueWith(t => // request docking
                {
                    if (!state.HasFlag(PilotState.Cruise))
                        return; // abort docking request if cruise gets turned off

                    Sounds.PlayOneOf("time to dock.mp3", "its dock oclock.mp3", "autopilot disengaged.mp3");
                    keyboard.TapWait(keyNavMenu); // nav menu
                    keyboard.TapWait(keyMenuTabRight); // tab right
                    keyboard.Tap(keyMenuTabRight); // tab right
                    keyboard.Tap(keySelect); // select first contact (the station)
                    keyboard.Tap(keyDown); // down to the second option (request docking)
                    keyboard.Tap(keySelect); // select request docking
                    keyboard.Tap(keyNavMenu); // close nav menu

                    state &= ~PilotState.Cruise; // disable! we've arrived!
                });
            }
        }
    }
}
