using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDAP
{
    /// <summary>
    /// This class takes in positions and velocities and outputs the acceleration required to get us to position=0.
    /// </summary>
    class Controller
    {
        public double maxAccel = 0;
        public double timestep = PilotJumper.TIMERINTERVAL_MS / 1000;

        private List<Tuple<DateTime, double>> positionHistory = new List<Tuple<DateTime, double>>();

        /// <summary>
        /// Try to approach position=0 at constant velocity (getting slower as we get closer)
        /// </summary>
        /// <param name="x">current position</param>
        /// <param name="v">current velocity</param>
        /// <returns>The desired acceleration</returns>
        public double LinearController(double x, double v)
        {
            var desiredVelocity = -x; // always 1 second away
            return (desiredVelocity - v) / timestep;
        }

        /// <summary>
        /// Attempts to follow the optimal constant-acceleration path
        /// </summary>
        /// <param name="x">current position</param>
        /// <param name="v">current velocity</param>
        /// <returns></returns>
        public double QuadraticController(double x, double v)
        {
            var a = maxAccel;
            // work out the initial acceleration direction
            // 1. find the acceleration direction that will give us a stationary point
            if (a * v >= 0)
                a *= -1;
            var stationarypoint = x - v * v / (2 * a);
            // # 2. If the inflection is already past the target, start by decelerating
            var signx = x >= 0 ? 1 : -1;
            if (stationarypoint * x >= 0)
                a = Math.Abs(a) * -signx;
            else
                a = Math.Abs(a) * signx;

            // solve the constant-acceleration equations to find the turning point that will cause us to eventually stop at x=0
            var rootpart = 1 / (a * Math.Sqrt(2)) * Math.Sqrt(v * v - 2 * a * x);
            var t1 = -v / a + rootpart;
            var t2 = -v / a - rootpart;
            var t0 = Math.Max(t1, t2); // one is probably negative

            if (t0 > timestep)
                return a;

            return a * (t0 / timestep);
        }

        public double QuadraticControllerDamped(double x, double v)
        {
            if (Math.Abs(x) < 2)
                return LinearController(x, v);
            var a = QuadraticController(x, v);
            if (v * x < 0 && a * v > 0)
                a *= 0.5;
            return a;
        }

        /// <summary>
        /// Produce a desired acceleration that will take us close to offset=0 as quickly as possible
        /// <param name="offset">The offset (in pixels) that we want to reduce</param>
        /// <param name="timestamps">The previous timestamps that we have requested an acceleration for</param>
        /// </summary>
        public double ComputeAcceleration(double offset, List<DateTime> ts)
        {
            DateTime now = ts[0];
            positionHistory.Insert(0, new Tuple<DateTime, double>(now, offset));
            if (positionHistory.Count > 3)
                positionHistory.RemoveAt(3);
            
            // if we have the two previous frames of finehistory, use that to estimate velocity
            if (!(positionHistory.Count > 2 &&
                  positionHistory[1].Item1 == ts[1] &&
                  positionHistory[2].Item1 == ts[2]))
            {
                return 0; // not enough frames of history
            }

            var v = QuadFitFinalVelocity(positionHistory[2].Item2, positionHistory[1].Item2, positionHistory[0].Item2, ts[2], ts[1], ts[0]);
            return QuadraticControllerDamped(offset, v);
        }

        /// <summary>
        /// Compute the final velocity from a quadratic fit
        /// </summary>
        /// <returns>dx/dt at t2</returns>
        public static double QuadFitFinalVelocity(double x1, double x2, double x3, DateTime dt1, DateTime dt2, DateTime dt3)
        {
            double t3 = (dt3 - dt3).TotalSeconds;
            double t2 = (dt3 - dt2).TotalSeconds;
            double t1 = (dt3 - dt1).TotalSeconds;

            // http://www.vb-helper.com/howto_find_quadratic_curve.html
            var atop = (x2 - x1) * (t1 - t3) + (x3 - x1) * (t2 - t1);
            var abottom = (t1 - t3) * (t2 * t2 - t1 * t1) + (t2 - t1) * (t3 * t3 - t1 * t1);
            if (abottom == 0.0)
                return 0;

            var btop = (x2 - x1) - atop / abottom * (t2 * t2 - t1 * t1);
            var bbottom = (t2 - t1);
            if (bbottom == 0.0)
                return 0;

            return /*2 * atop / abottom * t3 +*/ btop / bbottom;
        }
    }
}
