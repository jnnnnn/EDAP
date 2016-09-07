using EDAP.SendInput;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace EDAP
{
    /// <summary>
    /// This is a very simple controller. It waits a certain amount of time, then starts aligning to the compass. 
    /// When it is aligned, it presses "G". Then the cycle starts again.
    /// </summary>
    class PilotJumper
    {
        private DateTime last_jump_time;  // time since the jump key was pressed        
        private DateTime lastClear = DateTime.UtcNow.AddHours(-1);
        public Keyboard keyboard;
        private int jumps_remaining = 0;
        private uint alignFrames;

        public string status = "";

        [Flags]
        public enum PilotState
        {
            None            = 0,
            firstjump       = 1 << 0,
            clearedJump     = 1 << 1,
            jumpTick        = 1 << 2,
            swoopStart      = 1 << 3,
            swoopEnd        = 1 << 4,
            cruiseStart     = 1 << 5,
            AwayFromStar    = 1 << 6,
            SelectStar      = 1 << 7,
            SysMap          = 1 << 8, // whether to open the system map after jumping
            Cruise          = 1 << 9,
            CruiseEnd       = 1 << 10,
        }

        public PilotState state;
        public Screen screen;
        internal CompassRecognizer compassRecognizer;
        internal CruiseSensor cruiseSensor;

        double SecondsSinceLastJump { get { return (DateTime.UtcNow - last_jump_time).TotalSeconds; } }
        
        public void Reset()
        {
            state &= PilotState.SysMap | PilotState.Cruise; // clear per-jump flags
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

        private void Jump()
        {
            ClearAlignKeys();
            keyboard.Tap(Keyboard.LetterToKey('G')); // jump
            keyboard.Tap(Keyboard.LetterToKey('F')); // full throttle
            last_jump_time = DateTime.UtcNow;
            jumps_remaining -= 1;
            state &= PilotState.SysMap | PilotState.Cruise; // clear per-jump flags

            if (state.HasFlag(PilotState.SysMap))
            {
                keyboard.Tap(Keyboard.LetterToKey('6')); // open system map
                Task.Delay(6000).ContinueWith(t => keyboard.Keydown(Keyboard.LetterToKey('K'))); // scroll right on system map
                Task.Delay(7000).ContinueWith(t => keyboard.Keyup(Keyboard.LetterToKey('K')));
                Task.Delay(10000).ContinueWith(t => keyboard.Tap((int)ScanCode.F10)); // screenshot the system map                
            }
            if (jumps_remaining < 1)
            {
                Sounds.PlayOneOf("this is the last jump.mp3", "once more with feeling.mp3", "one jump remaining.mp3");
                Task.Delay(30000).ContinueWith(t =>
                {
                    // 30 seconds after last tap of jump key (after being in witchspace for 10 seconds)
                    keyboard.Keydown(Keyboard.LetterToKey('X'));  // cut throttle
                });
                Task.Delay(50000).ContinueWith(_ => 
                {
                    keyboard.Keyup(Keyboard.LetterToKey('X'));
                    Sounds.Play("you have arrived.mp3");
                });                
            }
        }

        internal void Idle()
        {
            // todo: recognize but don't act
        }
        
        /// <summary>
        /// Handle an input frame by setting which keys are pressed.
        /// </summary>        
        public void Act()
        {
            // perform the first alignment/jump immediately
            if (state.HasFlag(PilotState.firstjump) && jumps_remaining > 0)
            {
                if (Align())
                    Jump();
                return;
            }

            // charging friendship drive (15s) / countdown (5s) / witchspace (~14-16s)
            if (SecondsSinceLastJump < 30)
                return; 

            // just in case, we should make sure no keys have been forgotten about
            if (OncePerJump(PilotState.clearedJump))            
                keyboard.Clear();

            // dodge the star
            if (SecondsSinceLastJump < 40)
            {
                Swoop();
                return;
            }

            if (OncePerJump(PilotState.swoopEnd))
            {
                keyboard.Tap(Keyboard.LetterToKey('F')); // full throttle                    
                keyboard.Keydown(Keyboard.LetterToKey('O')); // hooooooooooooonk
                Task.Delay(10000).ContinueWith(t => keyboard.Keyup(Keyboard.LetterToKey('O'))); // stop honking after ten seconds
            }            

            // make sure we are travelling directly away from the star so that even if our next jump is directly behind it our turn will parallax it out of the way.
            // don't do it for the supercruise at the end because we can't reselect the in-system destination with the "N" key.
            if (!state.HasFlag(PilotState.AwayFromStar) && jumps_remaining > 0)
            {
                // select star
                if (OncePerJump(PilotState.SelectStar))
                {    
                    keyboard.Tap(Keyboard.LetterToKey('1'));
                    Thread.Sleep(100); // game takes a while to catch up with this.
                    keyboard.Tap(Keyboard.LetterToKey('D'));
                    keyboard.Tap((int)ScanCode.SPACEBAR);
                    Thread.Sleep(100);
                    keyboard.Tap((int)ScanCode.SPACEBAR);
                    Thread.Sleep(100);
                    keyboard.Tap(Keyboard.LetterToKey('1'));
                }

                // 45 because we want to make sure the honk finishes before opening the system map
                if (AntiAlign() && SecondsSinceLastJump > 45)
                {
                    state |= PilotState.AwayFromStar;
                    keyboard.Tap(Keyboard.LetterToKey('N')); // select the next destination
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

        private void ClearAlignKeys()
        {
            keyboard.Keyup(Keyboard.NumpadToKey('7'));
            keyboard.Keyup(Keyboard.NumpadToKey('9'));
            keyboard.Keyup(Keyboard.NumpadToKey('5'));
            keyboard.Keyup(Keyboard.NumpadToKey('8'));
            keyboard.Keyup(Keyboard.NumpadToKey('4'));
            keyboard.Keyup(Keyboard.NumpadToKey('6'));
            lastClear = DateTime.UtcNow;
        }

        /// <summary>
        /// Press whichever keys will make us point more towards the target.
        /// When rolling, try to keep the target down so that when we turn around the sun it doesn't shine on our compass.
        /// </summary>
        /// <param name="compass">The normalized vector pointing from the centre of the compass to the blue dot</param>
        /// <returns>true if we are pointing at the target</returns>
        private bool Align()
        {
            bool result = false;
            Point2f compass;
            try
            {
                compass = compassRecognizer.GetOrientation();
                status = string.Format("{0:0.0}, {1:0.0}", compass.X, compass.Y);
            }
            catch (Exception e)
            {
                ClearAlignKeys();
                alignFrames = 0;
                status = e.Message;
                return false;
            }

            float margin = 0.1f;
            double wrongness = Math.Sqrt(compass.X * compass.X + compass.Y * compass.Y);
            if (wrongness < 0.1)
            { 
                alignFrames += 1;
                result = alignFrames > 5;
            }
            else
                alignFrames = 0;

            if (wrongness < 0.1 && state.HasFlag(PilotState.Cruise))
            {
                int centreBox = 200;
                Rectangle screenCentre = new Rectangle(1920 / 2 - centreBox, 1080 / 2 - centreBox, centreBox * 2, centreBox * 2);
                try
                {
                    Point2f triquadrant = CruiseSensor.FindTriQuadrant(CompassRecognizer.Crop(screen.bitmap, screenCentre));
                    compass = triquadrant * margin * (1f / centreBox);
                    status = string.Format("{0:0.00}, {1:0.00}", compass.X, compass.Y);
                    margin /= 4;
                }
                catch (Exception e)
                {
                    status = e.Message;
                }
            }

            // re-press keys regularly in case the game missed a keydown (maybe because it wasn't focused)
            if ((DateTime.UtcNow - lastClear).TotalSeconds > 1)
                ClearAlignKeys();

            if (compass.X < -0.3)
                keyboard.Keydown(Keyboard.NumpadToKey('9')); // roll right
            else
                keyboard.Keyup(Keyboard.NumpadToKey('9'));
            if (compass.X > 0.3)
                keyboard.Keydown(Keyboard.NumpadToKey('7')); // roll left
            else
                keyboard.Keyup(Keyboard.NumpadToKey('7'));

            if (compass.Y < -margin)
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up
            else
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
            if (compass.Y > margin)
                keyboard.Keydown(Keyboard.NumpadToKey('8')); // pitch down
            else
                keyboard.Keyup(Keyboard.NumpadToKey('8'));

            if (compass.X < -margin)
                keyboard.Keydown(Keyboard.NumpadToKey('4')); // yaw left
            else
                keyboard.Keyup(Keyboard.NumpadToKey('4'));
            if (compass.X > margin)
                keyboard.Keydown(Keyboard.NumpadToKey('6')); // yaw right
            else
                keyboard.Keyup(Keyboard.NumpadToKey('6'));

            return result;
        }

        /// <summary>
        /// Press whichever keys will make us point more away from the target.
        /// </summary>
        /// <param name="compass">The normalized vector pointing from the centre of the compass to the blue dot</param>
        /// <returns>true if we are pointing directly away from the target</returns>
        private bool AntiAlign()
        {
            bool result = false;
            Point2f compass;
            try
            {
                compass = compassRecognizer.GetOrientation();
                status = string.Format("{1:0.0}, {2:0.0}", compass.X, compass.Y);
            }
            catch (Exception e)
            {
                ClearAlignKeys();
                alignFrames = 0;
                status = e.Message;
                return false;
            }

            if (Math.Abs(compass.X) < 0.1 && Math.Abs(compass.Y) > 1.9)
            {
                ClearAlignKeys();
                alignFrames += 1;
                return alignFrames > 3; // antialign doesn't need much accuracy... this will just stop accidental noise
            }
            else
                alignFrames = 0;

            // re-press keys regularly in case the game missed a keydown (maybe because it wasn't focused)
            if ((DateTime.UtcNow - lastClear).TotalSeconds > 1)
                ClearAlignKeys();

            if (compass.X < -0.3)
                keyboard.Keydown(Keyboard.NumpadToKey('7')); // roll left
            else
                keyboard.Keyup(Keyboard.NumpadToKey('7'));
            if (compass.X > 0.3)
                keyboard.Keydown(Keyboard.NumpadToKey('9')); // roll right
            else
                keyboard.Keyup(Keyboard.NumpadToKey('9'));

            if (compass.Y > 0 && compass.Y < 1.9)
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up
            else
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
            if (compass.Y < 0 && compass.Y > -1.9)
                keyboard.Keydown(Keyboard.NumpadToKey('8')); // pitch down
            else
                keyboard.Keyup(Keyboard.NumpadToKey('8'));

            if (compass.X < -0.1)
                keyboard.Keydown(Keyboard.NumpadToKey('6')); // yaw right
            else
                keyboard.Keyup(Keyboard.NumpadToKey('6'));
            if (compass.X > 0.1)
                keyboard.Keydown(Keyboard.NumpadToKey('4')); // yaw left
            else
                keyboard.Keyup(Keyboard.NumpadToKey('4'));

            return false;
        }

        /// <summary>
        /// At the end of a jump we are always just about to crash into the star. FFS. Pitch up for 5-15 seconds 
        /// (depending on how long witchspace took) at 50% throttle to avoid it.
        /// </summary>
        private void Swoop()
        {
            if (SecondsSinceLastJump < 40)
            {
                if (OncePerJump(PilotState.swoopStart))
                    keyboard.Tap(Keyboard.LetterToKey('P')); // set throttle to 50%
            
                // maybe in witchspace, maybe facing star
                // todo: better detection of the end of witchspace (sometimes it's way longer and antialign has trouble seeing the compass to turn away from the star)
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
                Thread.Sleep(10);
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up for ~10 seconds on arrival to avoid star.
                Thread.Sleep(100);
                return;
            }            
        }

        /// <summary>
        /// This handles cruising behaviour. The main function is already keeping us aligned; 
        /// we just need to drop to 75% speed.
        /// This function could also do the final part: 
        ///  1. press G when the "SAFE DISENGAGE" graphic is detected
        ///  2. Wait 5 seconds
        ///  3. Press Tab, X, 1,E,E,Space,S,Space so the docking computer takes over.
        /// </summary>
        private void Cruise()
        {
            if (SecondsSinceLastJump > 60 && OncePerJump(PilotState.cruiseStart))
            {
                Sounds.Play("cruise mode engaged.mp3");
                keyboard.Tap(Keyboard.LetterToKey('F')); // full throttle
                keyboard.Tap(Keyboard.LetterToKey('Q')); // drop 25% throttle
            }

            if (!state.HasFlag(PilotState.CruiseEnd) && cruiseSensor.MatchSafDisengag())
            {
                keyboard.Tap(Keyboard.LetterToKey('G')); // disengage
                state |= PilotState.CruiseEnd;
            }
        }
    }
}
