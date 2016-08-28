using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDAP
{
    class PilotJumper
    {
        private DateTime last_jump_time = DateTime.Today.Date; // time since the "J" key was pressed        
        public Keyboard keyboard;
        public int jumps_remaining = 0;
        public bool bFirstJump = false;
        private uint alignFrames = 0;

        double SecondsSinceLastJump { get { return (DateTime.UtcNow - last_jump_time).TotalSeconds; } }

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

        public void Respond(System.Drawing.PointF compass)
        {
            if (bFirstJump)
            {
                if (Align(compass) && jumps_remaining > 0)
                    Jump();
                return;
            }
            
            if (SecondsSinceLastJump < 20)
                return; // charging friendship drive / countdown

            if (SecondsSinceLastJump < 30)
                return; // definitely in witchspace (10..20s)

            if (SecondsSinceLastJump < 40)
            {
                // maybe in witchspace, maybe facing star
                keyboard.Keyup(Keyboard.NumpadToKey('5'));
                Thread.Sleep(10);
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up for five to ten seconds on arrival to avoid star.
                Thread.Sleep(100);
                return;
            }

            // okay, by this point we are cruising away from the star and are ready to align and jump. We can't start charging to jump until 10 seconds after witchspace ends, but we can start aligning.

            if (Align(compass) && SecondsSinceLastJump > 50 && jumps_remaining > 0)
                Jump();
        }

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
                keyboard.Keydown(Keyboard.NumpadToKey('4')); // yaw left up
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
