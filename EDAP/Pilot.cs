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

        const float align_margin = 0.15f; // more -- jumps don't work (not aligned). less -- fine adjustment not allowed to work.

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
            Honk            = 1 << 11,
        }

        public PilotState state;
        public Screenshot screen;
        internal CompassRecognizer compassRecognizer;
        internal CruiseSensor cruiseSensor;

        double SecondsSinceLastJump { get { return (DateTime.UtcNow - last_jump_time).TotalSeconds; } }
        
        public void Reset()
        {
            state &= PilotState.SysMap | PilotState.Cruise | PilotState.Honk; // clear per-jump flags
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
            state &= PilotState.SysMap | PilotState.Cruise | PilotState.Honk; // clear per-jump flags
            last_jump_time = DateTime.UtcNow;
            jumps_remaining -= 1;            

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
                if (state.HasFlag(PilotState.Honk))
                {
                    keyboard.Keydown(Keyboard.LetterToKey('O')); // hooooooooooooonk
                    Task.Delay(10000).ContinueWith(t => keyboard.Keyup(Keyboard.LetterToKey('O'))); // stop honking after ten seconds
                }
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

            double wrongness = Math.Sqrt(compass.X * compass.X + compass.Y * compass.Y);
            if (wrongness < align_margin)
            { 
                alignFrames += 1;
                result = alignFrames > 5;
                FineAlign();
                return true;
            }
            else
                alignFrames = 0;
            
            // re-press keys regularly in case the game missed a keydown (maybe because it wasn't focused)
            if ((DateTime.UtcNow - lastClear).TotalSeconds > 1)
                ClearAlignKeys();
            
            keyboard.SetKeyState(Keyboard.NumpadToKey('9'), compass.X < -0.3); // roll right
            keyboard.SetKeyState(Keyboard.NumpadToKey('7'), compass.X > 0.3); // roll left
            keyboard.SetKeyState(Keyboard.NumpadToKey('5'), compass.Y < -align_margin); // pitch up
            keyboard.SetKeyState(Keyboard.NumpadToKey('8'), compass.Y > align_margin); // pitch down
            keyboard.SetKeyState(Keyboard.NumpadToKey('4'), compass.X < -align_margin); // yaw left
            keyboard.SetKeyState(Keyboard.NumpadToKey('6'), compass.X > align_margin); // yaw right
            
            return result;
        }

        private Point2f oldOffset = new Point2f();
        private int missedFineFrames = 0;
        /// <summary>
        /// try to point accurately at the target by centering the triquadrant on the screen 
        /// </summary>
        private void FineAlign()
        {
            int centreBox = 150;
            Rectangle screenCentre = new Rectangle(1920 / 2 - centreBox, 1080 / 2 - centreBox, centreBox * 2, centreBox * 2);
            Point2f offset;
            Point2f velocity;
            try
            {
                Point2f triquadrant = cruiseSensor.FindTriQuadrant(CompassRecognizer.Crop(screen.bitmap, screenCentre));
                offset = -triquadrant;
                velocity = (offset - oldOffset) * (1f / (screen.timestamp - screen.oldTimestamp).TotalSeconds); // pixels / s   
            }
            catch (Exception e)
            {
                status = e.Message;
                return;
            }
                                     
            if (oldOffset.X == 0 && oldOffset.Y == 0)
            {
                missedFineFrames += 1;
                ClearAlignKeys();
                status = string.Format("{0:0.00}, {1:0.00} ({2} old offset not available)", offset.X, offset.Y, missedFineFrames);
            }
            else
            {
                status = string.Format("{0:0.00}, {1:0.00}", offset.X, offset.Y);
                missedFineFrames = 0;
                const float fineMargin = 50; // size of dead zone (in pixels)
                const float fineVelocityCoeff = 0.1f; // target centering velocity, in pixels per second per pixel offset
                keyboard.SetKeyState(Keyboard.NumpadToKey('8'), offset.Y < -fineMargin && velocity.Y / -offset.Y < fineVelocityCoeff); // pitch up
                keyboard.SetKeyState(Keyboard.NumpadToKey('5'), offset.Y > fineMargin && -velocity.Y / offset.Y < fineVelocityCoeff); // pitch down
                keyboard.SetKeyState(Keyboard.NumpadToKey('4'), offset.X > fineMargin && -velocity.X / offset.X < fineVelocityCoeff); // yaw left
                keyboard.SetKeyState(Keyboard.NumpadToKey('6'), offset.X < -fineMargin && velocity.X / -offset.X < fineVelocityCoeff); // yaw right
            }
            oldOffset = offset; // save for next time
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
                status = string.Format("{0:0.0}, {1:0.0}", compass.X, compass.Y);
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
            
            keyboard.SetKeyState(Keyboard.NumpadToKey('9'), compass.X > 0.3); // roll right
            keyboard.SetKeyState(Keyboard.NumpadToKey('7'), compass.X < -0.3); // roll left
            keyboard.SetKeyState(Keyboard.NumpadToKey('5'), compass.Y > 0 && compass.Y < 1.9); // pitch up
            keyboard.SetKeyState(Keyboard.NumpadToKey('8'), compass.Y < 0 && compass.Y > -1.9); // pitch down
            keyboard.SetKeyState(Keyboard.NumpadToKey('4'), compass.X > 0.1); // yaw left
            keyboard.SetKeyState(Keyboard.NumpadToKey('6'), compass.X < -0.1); // yaw right

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
                // todo: better detection of the end of witchspace (sometimes it's way longer 
                // and antialign has trouble seeing the compass to turn away from the star, or 
                // may even be so late that it doesn't select the star for the antialign procedure)
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
                // these commands will initiate docking if we have a computer
                Task.Delay(6000).ContinueWith(t => keyboard.Tap((int)ScanCode.TAB)); // boost
                Task.Delay(10000).ContinueWith(t => keyboard.Tap((int)ScanCode.KEY_X)); // cut throttle
                Task.Delay(12000).ContinueWith(t =>
                {
                    if (!state.HasFlag(PilotState.Cruise))
                        return; // abort docking thing if cruise gets turned off
                    keyboard.Tap(Keyboard.LetterToKey('1')); // nav menu           
                    Thread.Sleep(200); // game needs time to open this menu         
                    keyboard.Tap(Keyboard.LetterToKey('E')); // tab right65765
                    Thread.Sleep(200); // game needs time to realise key was unpressed
                    keyboard.Tap(Keyboard.LetterToKey('E')); // tab right
                    keyboard.Tap((int)ScanCode.SPACEBAR); // select first contact (the station)
                    keyboard.Tap(Keyboard.LetterToKey('S')); // down to the second option (request docking)
                    keyboard.Tap((int)ScanCode.SPACEBAR); // select request docking
                    keyboard.Tap(Keyboard.LetterToKey('1')); // close nav menu
                });
            }
        }
    }
}
