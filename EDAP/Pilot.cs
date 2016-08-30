using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private DateTime last_jump_time = DateTime.UtcNow.AddHours(-1); // time since the jump key was pressed        
        public Keyboard keyboard;
        public int jumps_remaining = 0;
        private uint alignFrames = 0;
        
        public enum PilotState
        {
            None = 0,
            firstjump = 1 << 0,
            swoopStart = 1 << 1,            
            swoopEnd = 1 << 2,
            cruiseStart = 1 << 3,
        }

        public PilotState state;

        double SecondsSinceLastJump { get { return (DateTime.UtcNow - last_jump_time).TotalSeconds; } }

        /// <summary>
        /// Add another jump to the queue. If this is the first jump, don't wait for all the cooldowns before jumping.
        /// </summary>
        public void QueueJump()
        {
            if (jumps_remaining < 1 && SecondsSinceLastJump > 50)
            {
                last_jump_time = DateTime.UtcNow;
                jumps_remaining = 0;
                state = PilotState.None;
                state |= PilotState.firstjump;
            }
            jumps_remaining += 1;
        }
        
        private void Jump()
        {
            keyboard.Clear();
            keyboard.Tap(Keyboard.LetterToKey('G')); // jump            
            last_jump_time = DateTime.UtcNow;
            jumps_remaining -= 1;
            state = PilotState.None;
        }

        /// <summary>
        /// Handle an input frame by setting which keys are pressed.
        /// </summary>
        /// <param name="compass"></param>
        public void Respond(System.Drawing.PointF compass)
        {
            // perform the first alignment/jump immediately
            if (state.HasFlag(PilotState.firstjump))
            {
                if (Align(compass) && jumps_remaining > 0)
                    Jump();
                return;
            }
            
            if (SecondsSinceLastJump < 30)
                return; // charging friendship drive (15s) / countdown (5s) / witchspace (~14-16s)

            if (SecondsSinceLastJump < 45)
            {
                Swoop();
                return;
            }

            // okay, by this point we are cruising away from the star and are ready to align and jump. We can't start 
            // charging to jump until 10 seconds after witchspace ends, but we can start aligning.

            if (Align(compass) && jumps_remaining > 0)
                Jump();
            else if (jumps_remaining < 1)
                Cruise();
        }

        /// <summary>
        /// Press whichever keys will make us point more towards the target.
        /// </summary>
        /// <param name="compass">The normalized vector pointing from the centre of the compass to the blue dot</param>
        /// <returns>true if we are pointing at the target</returns>
        private bool Align(System.Drawing.PointF compass)
        {
            if (Math.Abs(compass.X) < 0.1 && Math.Abs(compass.Y) < 0.1)
            {
                keyboard.Clear();
                alignFrames += 1;
                return alignFrames > 10;
            }

            alignFrames = 0;

            if (compass.X < -0.3)
                keyboard.Keydown(Keyboard.NumpadToKey('7')); // roll left
            else
                keyboard.Keyup(Keyboard.NumpadToKey('7'));
            if (compass.X > 0.3)
                keyboard.Keydown(Keyboard.NumpadToKey('9')); // roll right
            else
                keyboard.Keyup(Keyboard.NumpadToKey('9'));

            if (compass.Y < -0.1)
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up
            else
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
            if (compass.Y > 0.1)
                keyboard.Keydown(Keyboard.NumpadToKey('8')); // pitch down
            else
                keyboard.Keyup(Keyboard.NumpadToKey('8'));

            if (compass.X < -0.1)
                keyboard.Keydown(Keyboard.NumpadToKey('4')); // yaw left
            else
                keyboard.Keyup(Keyboard.NumpadToKey('4'));
            if (compass.X > 0.1)
                keyboard.Keydown(Keyboard.NumpadToKey('6')); // yaw right
            else
                keyboard.Keyup(Keyboard.NumpadToKey('6'));
            
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
                if (!state.HasFlag(PilotState.swoopStart))
                {
                    keyboard.Tap(Keyboard.LetterToKey('P')); // set throttle to 50%
                    state |= PilotState.swoopStart;
                }

                // maybe in witchspace, maybe facing star
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
                Thread.Sleep(10);
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up for ~10 seconds on arrival to avoid star.
                Thread.Sleep(100);
                return;
            }

            if (SecondsSinceLastJump < 45)
            {
                if (!state.HasFlag(PilotState.swoopEnd))
                {
                    keyboard.Tap(Keyboard.LetterToKey('F')); // full throttle
                    state |= PilotState.swoopEnd;
                }    

                // cruise away from the star for a few seconds to make it less likely that we hit it after alignment
                keyboard.Clear();
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
            if (SecondsSinceLastJump > 60 && !state.HasFlag(PilotState.cruiseStart))
            {
                keyboard.Tap(Keyboard.LetterToKey('F')); // full throttle
                keyboard.Tap(Keyboard.LetterToKey('Q')); // drop 25% throttle
                state |= PilotState.cruiseStart;
            }
        }
    }
}
