using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDAP
{
    class PilotJumper
    {
        private DateTime last_jump_time; // time since the "J" key was pressed        
        public Keyboard keyboard;
        public int jumps_remaining = 0;

        public void QueueJump()
        {
            if (jumps_remaining < 1)
            {
                last_jump_time = DateTime.UtcNow;
                jumps_remaining = 0;
            }
            jumps_remaining += 1;
        }

        public void Respond(System.Drawing.PointF compass)
        {
            double progress = (DateTime.UtcNow - last_jump_time).TotalSeconds;
            if (progress < 5)
                return; // charging friendship drive

            if (progress < 15)
                return; // witchspace

            if (progress < 20)
            {
                keyboard.Keydown(Keyboard.NumpadToKey('5')); // pitch up for five seconds on arrival to avoid star.
                return;
            }

            // okay, by this point we are cruising away from the star and are ready to align and jump. We can't start charging to jump until 10 seconds after witchspace ends, but we can start aligning.

            if (Align(compass) && progress > 25)
                Jump();
        }

        private bool Align(System.Drawing.PointF compass)
        {
            if (Math.Abs(compass.X) < 0.1 && Math.Abs(compass.Y) < 0.1)
            {
                keyboard.Clear();
                return true;
            }

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

        private void Jump()
        {
            keyboard.Clear();
            keyboard.Tap(Keyboard.LetterToKey('G'));
            last_jump_time = DateTime.UtcNow;
            jumps_remaining -= 1;            
        }
    }
}
