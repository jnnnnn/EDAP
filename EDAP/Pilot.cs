using EDAP.SendInput;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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

        [Flags]
        public enum PilotState
        {
            None = 0,
            firstjump = 1 << 0, // is this the first jump? (skip waiting and swooping and go straight to aligning)
            clearedJump = 1 << 1, // at the start of the jump-loop process, we need to reset everything
            jumpCharge = 1 << 2, 
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
            ScoopAlign = 1 << 14, // have we aligned for scooping yet
            scoopComplete = 1 << 15, // have we finished scooping yet
            Scoop = 1 << 16, // do we want to scoop at each star?
            HonkComplete = 1 << 17, // have we fired the discovery scanner yet
        }

        public PilotState state;
        public Screenshot screen;
        internal CompassSensor compassRecognizer;
        internal CruiseSensor cruiseSensor;

        double SecondsSinceLastJump { get { return (DateTime.UtcNow - last_jump_time).TotalSeconds; } }
        double SecondsSinceFaceplant {  get { return (DateTime.UtcNow - last_faceplant_time).TotalSeconds; } }

        public void Reset(bool soft)
        {
            // soft reset (after every jump)
            last_faceplant_time = DateTime.UtcNow.AddHours(-1);
            state &= PilotState.Enabled | PilotState.SysMap | PilotState.Cruise | PilotState.Honk | PilotState.Scoop; // clear per-jump flags
            if (soft)
                return;

            // hard reset (when user activates autopilot)            
            state |= PilotState.firstjump;
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
                if (Align())
                    Jump();
                return;
            }

            // charging frendship drive (15s) / countdown (5s) / loading screen (~14-16s)
            if (SecondsSinceLastJump < 30)
                return;

            // just in case, we should make sure no keys have been forgotten about
            if (OncePerJump(PilotState.clearedJump))
            {
                keyboard.Tap(ScanCode.KEY_X); // cut throttle
                keyboard.Clear();
            }

            // wait until we hit the star at the end of the loading screen (up to 100 seconds)
            if (!state.HasFlag(PilotState.Faceplant) && SecondsSinceLastJump < 100)
            {
                if (compassRecognizer.MatchFaceplant())
                {
                    state |= PilotState.Faceplant;
                    last_faceplant_time = DateTime.UtcNow;
                }
                else
                    return; // keep waiting
            }

            if (SecondsSinceFaceplant < 1)
                return;

            if (state.HasFlag(PilotState.Honk) && OncePerJump(PilotState.HonkComplete))
            {
                keyboard.Keydown(ScanCode.KEY_O); // hooooooooooooonk
                Task.Delay(10000).ContinueWith(t => keyboard.Keyup(ScanCode.KEY_O)); // stop honking after ten seconds
            }

            // If we've finished jumping and are not cruising, just stop and point at the star (scan and makes scooping easier).
            if (jumps_remaining < 1 && !state.HasFlag(PilotState.Cruise))
            {                      
                if (OncePerJump(PilotState.swoopEnd))
                    keyboard.Tap(ScanCode.KEY_X); // cut throttle

                if (OncePerJump(PilotState.SelectStar))
                    SelectStar();

                Align();
                return;
            }

            if (state.HasFlag(PilotState.Scoop) && !state.HasFlag(PilotState.scoopComplete) && jumps_remaining > 0)
            {
                Scoop();
                return;
            }
            else
            {
                // swoop a bit more if last jump because slow ship kept hitting star
                if (SecondsSinceFaceplant < (jumps_remaining < 1 ? 10 : 5))
                {
                    Swoop();
                    return;
                }

                if (OncePerJump(PilotState.swoopEnd))
                {
                    keyboard.Tap(ScanCode.KEY_F); // full throttle                    
                }
            }

            // cruise away from the star for an extra ten seconds after the last jump to make it less likely that manual intervention is required to dodge the star
            if (SecondsSinceFaceplant < 15 && jumps_remaining < 1 && state.HasFlag(PilotState.Cruise))
            {
                keyboard.Clear();
                return;
            }

            // make sure we are travelling directly away from the star so that even if our next jump is directly behind it our turn will parallax it out of the way.
            // don't do it for the supcruz at the end because we can't reselect the in-system destination with the "N" key.
            if (!state.HasFlag(PilotState.AwayFromStar) && jumps_remaining > 0)
            {
                if (OncePerJump(PilotState.SelectStar))                
                    SelectStar();
                
                // 10 because we want to make sure the honk finishes before opening the system map
                if (AntiAlign() && SecondsSinceFaceplant > 10)
                {
                    state |= PilotState.AwayFromStar;
                    keyboard.Tap(ScanCode.KEY_N); // select the next destination
                    keyboard.Tap(ScanCode.KEY_F); // full throttle                    
                }
                else
                    return;
            }

            // okay, by this point we are cruising away from the star and are ready to align and jump. We can't start 
            // charging to jump until 10 seconds after witchspace ends, but we can start aligning.
            
            if (jumps_remaining < 1 && state.HasFlag(PilotState.Cruise))
            {
                Align();
                Cruise();
            }
            else if (jumps_remaining > 0 && Align())
                Jump();
        }

        private void Scoop()
        {
            if (OncePerJump(PilotState.SelectStar))
            {
                keyboard.Clear();
                SelectStar();
                keyboard.Tap(ScanCode.KEY_P); // 50% throttle        
            }

            if (!state.HasFlag(PilotState.ScoopAlign))
            {
                if (Align(x: 0, y: 0.82f, align_margin: 0.05f))
                {
                    state |= PilotState.ScoopAlign;
                    scoopStart = DateTime.UtcNow;
                    ClearAlignKeys();
                    Task.Delay(10000).ContinueWith(t => keyboard.Tap(ScanCode.KEY_F)); // full throttle for flybyscooping
                }
                status += "Scoop align\n";
                return;
            }
            
            // cruise past the star (through the corona, hopefully)
            double ScoopTime = (DateTime.UtcNow - scoopStart).TotalSeconds;

            // parallel after five seconds...
            Align(x: 0, y: (ScoopTime > 5 ? 0.95f : 0.85f), align_margin: 0.05f);

            status += String.Format("Scoop wait + {0:0.0}\n", ScoopTime);
            
            if (ScoopTime > 20)
                state |= PilotState.scoopComplete;
        }

        // select star
        private void SelectStar()
        {
            keyboard.Tap(ScanCode.KEY_1); // nav menu
            Thread.Sleep(100); // game takes a while to catch up with this.
            keyboard.Tap(ScanCode.KEY_D); // right to select nearest object in system (the central star)
            keyboard.Tap(ScanCode.SPACEBAR); // open menu
            Thread.Sleep(100);
            keyboard.Tap(ScanCode.SPACEBAR); // select the object
            Thread.Sleep(100);
            keyboard.Tap(ScanCode.KEY_1); // close nav menu
        }

        private void Jump()
        {
            ClearAlignKeys();
            if (OncePerJump(PilotState.jumpCharge))
            {
                keyboard.Tap(ScanCode.KEY_G); // jump (frameshift drive charging)
                last_jump_time = DateTime.UtcNow;
            }
            else
                last_jump_time = DateTime.UtcNow.AddSeconds(-15); // because we might have charged for up to 15 seconds...

            keyboard.Tap(ScanCode.KEY_F); // full throttle
            jumps_remaining -= 1;

            // reset everything
            Reset(soft: true);

            if (state.HasFlag(PilotState.SysMap))
            {
                keyboard.Tap(ScanCode.KEY_6); // open system map
                Task.Delay(6000).ContinueWith(t => keyboard.Keydown(ScanCode.KEY_K)); // scroll right on system map
                Task.Delay(7000).ContinueWith(t => keyboard.Keyup(ScanCode.KEY_K));
                Task.Delay(10000).ContinueWith(t => keyboard.Tap(ScanCode.F10)); // screenshot the system map                
            }

            if (jumps_remaining < 1)
            {
                Sounds.PlayOneOf("this is the last jump.mp3", "once more with feeling.mp3", "one jump remaining.mp3");
                Task.Delay(30000).ContinueWith(t =>
                {
                    // 30 seconds after last tap of jump key (after being in witchspace for 10 seconds)
                    keyboard.Keydown(ScanCode.KEY_X);  // cut throttle
                });
                Task.Delay(50000).ContinueWith(_ =>
                {
                    keyboard.Keyup(ScanCode.KEY_X);
                    Sounds.Play("you have arrived.mp3");
                });
            }
        }

        /// <summary>
        /// Run the recognition stuff but don't press any keys
        /// </summary>
        internal void Idle()
        {
            try
            {

                int centreBox = 150;
                Rectangle screenCentre = new Rectangle(1920 / 2 - centreBox, 1080 / 2 - centreBox, centreBox * 2, centreBox * 2);
                cruiseSensor.FindTriQuadrant(CompassSensor.Crop(screen.bitmap, screenCentre));
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
                alignFrames = 0;
                status = e.Message + "\n" + status;
                return;
            }
        }
        
        private void ClearAlignKeys()
        {
            keyboard.Keyup(ScanCode.NUMPAD_7);
            keyboard.Keyup(ScanCode.NUMPAD_9);
            keyboard.Keyup(ScanCode.NUMPAD_5);
            keyboard.Keyup(ScanCode.NUMPAD_8);
            keyboard.Keyup(ScanCode.NUMPAD_4);
            keyboard.Keyup(ScanCode.NUMPAD_6);
            lastClear = DateTime.UtcNow;
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
        /// <returns>true if we are pointing at the target</returns>
        private bool Align(float x = 0, float y = 0, float align_margin = 0.15f)
        {
            bool toTarget = x == 0.0f && y == 0.0f;
            if (toTarget)
            {
                // start by looking for the triquadrant target (because that is more reliably recognized, it doesn't change size / skew depending on ship movement)
                status = "";
                try
                {
                    return FineAlign();
                }
                catch (Exception e)
                {
                    status = e.Message;
                }
            }

            // see if we can find the compass
            Point2f compass;
            try
            {
                compass = compassRecognizer.GetOrientation();
                status = string.Format("{0:0.0}, {1:0.0}\n", compass.X, compass.Y) + status;
                compass.X -= x;
                compass.Y -= y;
            }
            catch (Exception e)
            {
                ClearAlignKeys();
                alignFrames = 0;
                status = e.Message + "\n" + status;
                return false;
            }
            
            // re-press keys regularly in case the game missed a keydown (maybe because it wasn't focused)
            if ((DateTime.UtcNow - lastClear).TotalSeconds > 1)
                ClearAlignKeys();

            // press whichever keys will point us toward the target. Coordinate system origin is bottom right
            keyboard.SetKeyState(ScanCode.NUMPAD_9, compass.X < -0.3); // roll right
            keyboard.SetKeyState(ScanCode.NUMPAD_7, compass.X > 0.3); // roll left
            keyboard.SetKeyState(ScanCode.NUMPAD_5, compass.Y < -align_margin); // pitch up
            keyboard.SetKeyState(ScanCode.NUMPAD_8, compass.Y > align_margin); // pitch down
            keyboard.SetKeyState(ScanCode.NUMPAD_4, compass.X < -align_margin); // yaw left
            keyboard.SetKeyState(ScanCode.NUMPAD_6, compass.X > align_margin); // yaw right

            if (toTarget)
                return false; // only fine align can confirm we are aligned to the target

            return (Math.Abs(compass.Y) < align_margin && Math.Abs(compass.X) < align_margin);
        }

        private List<Tuple<DateTime, Point2f>> finehistory = new List<Tuple<DateTime, Point2f>>();
        /// <summary>
        /// try to point accurately at the target by centering the little yellow square in the triquadrant (the target) on the screen 
        /// </summary>
        private bool FineAlign()
        {
            int centreBox = 150;
            Rectangle screenCentre = new Rectangle(1920 / 2 - centreBox, 1080 / 2 - centreBox, centreBox * 2, centreBox * 2);
            Point2f offset;

            Point2f triquadrant = cruiseSensor.FindTriQuadrant(CompassSensor.Crop(screen.bitmap, screenCentre));                
            offset = -triquadrant;
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
                velocity.X = -(float)CompassSensor.QuadFitFinalVelocity(finehistory[2].Item2.X, finehistory[1].Item2.X, finehistory[0].Item2.X, ts[2], ts[1], ts[0]);
                velocity.Y = -(float)CompassSensor.QuadFitFinalVelocity(finehistory[2].Item2.Y, finehistory[1].Item2.Y, finehistory[0].Item2.Y, ts[2], ts[1], ts[0]);
                Console.WriteLine(string.Format("velocity: {0}; {1}", velocity, compassRecognizer.GetOrientationVelocity()));
            }
            else
            {
                // I tried to use the compass' velocity here instead of fine velocity but it was too 
                // noisy for a quadratic prediction, the fine and rough velocities were basically uncorrelated.
                // Instead, pause until the fine velocity prediction has enough frames.
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
                keyboard.TimedTap(ScanCode.NUMPAD_8, (int)(tY * 1000)); // pitch down when offset.Y < 0
            else
                keyboard.Keyup(ScanCode.NUMPAD_8);
            if (offset.Y > deadzone && aY < 0 && tY > 0.05)
                keyboard.TimedTap(ScanCode.NUMPAD_5, (int)(tY * 1000)); // pitch up when offset.Y > 0
            else
                keyboard.Keyup(ScanCode.NUMPAD_5);

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
                keyboard.TimedTap(ScanCode.NUMPAD_6, (int)(tX * 1000)); // yaw right when offset.X < 0
            else
                keyboard.Keyup(ScanCode.NUMPAD_6);
            if (offset.X > deadzone && aX < 0 && tX > 0.05)
                keyboard.TimedTap(ScanCode.NUMPAD_4, (int)(tX * 1000)); // yaw left when offset.X > 0
            else
                keyboard.Keyup(ScanCode.NUMPAD_4);

            status = string.Format("{0:0}, {1:0}\n", offset.X, offset.Y);

            return Math.Abs(offset.X) < 50 && Math.Abs(offset.Y) < 50; // && Math.Abs(velocity.X) < 100 && Math.Abs(velocity.Y) < 100;
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

            keyboard.SetKeyState(ScanCode.NUMPAD_7, compass.X > 0.3); // roll right
            keyboard.SetKeyState(ScanCode.NUMPAD_9, compass.X < -0.3); // roll left
            keyboard.SetKeyState(ScanCode.NUMPAD_5, compass.Y > 0 && compass.Y < 1.9); // pitch up
            keyboard.SetKeyState(ScanCode.NUMPAD_8, compass.Y < 0 && compass.Y > -1.9); // pitch down
            keyboard.SetKeyState(ScanCode.NUMPAD_4, compass.X > 0.1); // yaw left
            keyboard.SetKeyState(ScanCode.NUMPAD_6, compass.X < -0.1); // yaw right

            // antialign doesn't need much accuracy... this will just stop accidental noise
            alignFrames = (Math.Abs(compass.X) < 0.2 && Math.Abs(compass.Y) > 1.0) ? alignFrames + 1 : 0;
            if (alignFrames > 3)
            {
                ClearAlignKeys();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// At the end of a jump we are always just about to crash into the star. FFS. Pitch up for 5-15 seconds 
        /// (depending on how long witchspace took) at 50% throttle to avoid it.
        /// </summary>
        private void Swoop()
        {
            if (SecondsSinceFaceplant > 2 && OncePerJump(PilotState.swoopStart))
                keyboard.Tap(ScanCode.KEY_P); // set throttle to 50%

            keyboard.Keyup(ScanCode.NUMPAD_5);
            Thread.Sleep(10);
            keyboard.Keydown(ScanCode.NUMPAD_5); // pitch up for ~5 seconds on arrival to avoid star.
            Thread.Sleep(100);
            return;
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
                keyboard.Tap(ScanCode.KEY_F); // full throttle
                keyboard.Tap(ScanCode.KEY_Q); // drop 25% throttle, to 75%
            }

            if (!state.HasFlag(PilotState.CruiseEnd) && cruiseSensor.MatchSafDisengag())
            {
                keyboard.Tap(ScanCode.KEY_G); // "Safe Disengage"
                state |= PilotState.CruiseEnd;
                state &= ~PilotState.Enabled; // disable! we've arrived!
                // these commands will initiate docking if we have a computer
                Task.Delay(6000).ContinueWith(t => keyboard.Tap(ScanCode.TAB)); // boost
                Task.Delay(10000).ContinueWith(t => keyboard.Tap(ScanCode.KEY_X)); // cut throttle
                Task.Delay(12000).ContinueWith(t => // request docking
                {
                    if (!state.HasFlag(PilotState.Cruise))
                        return; // abort docking thing if cruise gets turned off

                    Sounds.PlayOneOf("time to dock.mp3", "its dock oclock.mp3", "autopilot disengaged.mp3");
                    keyboard.Tap(ScanCode.KEY_1); // nav menu
                    Thread.Sleep(200); // game needs time to open this menu
                    keyboard.Tap(ScanCode.KEY_E); // tab right
                    Thread.Sleep(200); // game needs time to realise key was unpressed
                    keyboard.Tap(ScanCode.KEY_E); // tab right
                    keyboard.Tap(ScanCode.SPACEBAR); // select first contact (the station)
                    keyboard.Tap(ScanCode.KEY_S); // down to the second option (request docking)
                    keyboard.Tap(ScanCode.SPACEBAR); // select request docking
                    keyboard.Tap(ScanCode.KEY_1); // close nav menu

                    state &= ~PilotState.Cruise; // disable! we've arrived!
                });
            }
        }
    }
}
