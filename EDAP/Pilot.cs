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
        private DateTime last_jump_time = DateTime.UtcNow.AddHours(-1); // time since the "J" key was pressed        
        public Keyboard keyboard;
        public int jumps_remaining = 0;
        public bool bFirstJump = false;
        private uint alignFrames = 0;

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
                bFirstJump = true;
            }
            jumps_remaining += 1;
        }
        
        private void Jump()
        {
            keyboard.Clear();
            keyboard.Tap(Keyboard.LetterToKey('G'));
            last_jump_time = DateTime.UtcNow;
            jumps_remaining -= 1;
            bFirstJump = false;
        }

        /// <summary>
        /// Handle an input frame by setting which keys are pressed.
        /// </summary>
        /// <param name="compass"></param>
        public void Respond(System.Drawing.PointF compass)
        {
            if (bFirstJump)
            {
                if (Align(compass) && jumps_remaining > 0)
                    Jump();
                return;
            }
            
            if (SecondsSinceLastJump < 30)
                return; // charging friendship drive (15s) / countdown (5s) / witchspace (~14-16s)

            if (SecondsSinceLastJump < 45)
            {
                // maybe in witchspace, maybe facing star
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
                Thread.Sleep(10);
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up for ~10 seconds on arrival to avoid star.
                Thread.Sleep(100);
                return;
            }
            
            if (SecondsSinceLastJump < 50)
            {
                // cruise away from the star for a few seconds to make it less likely that we hit it after alignment
                keyboard.Clear();
                return;
            }

            //cruise away from the star for at least ten seconds to make it less likely for us to hit it
            if (SecondsSinceLastJump < 50)
            {
                return;
            }

            // okay, by this point we are cruising away from the star and are ready to align and jump. We can't start charging to jump until 10 seconds after witchspace ends, but we can start aligning.

            if (Align(compass) && jumps_remaining > 0)
                Jump();
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
    }
}
