using OpenCvSharp;
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
            double result = (desiredVelocity - v);
            if (Math.Abs(result) > 10)
                return result > 0 ? 3 : -3;
            return result;            
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

            if (t0 > 1)
                return a;
            return a * t0;
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
        
        public static double Clamp(double val, double absmax)
        {
            if (val.CompareTo(-absmax) < 0) return -absmax;
            else if (val.CompareTo(absmax) > 0) return +absmax;
            else return val;
        }

        static int ControllerIndex = 0;
        public System.IO.StreamWriter file = new System.IO.StreamWriter(string.Format(@"C:\Users\Public\Controller{0}.txt", ControllerIndex++));

        public double offset_fixed;
        /// <summary>
        /// Produce a desired acceleration that will take us close to offset=0 as quickly as possible
        /// <param name="offset">The offset (in pixels) that we want to reduce</param>
        /// <param name="timestamps">The previous timestamps that we have requested an acceleration for</param>
        /// </summary>
        public double ComputeAcceleration(double offset)
        {
            /*
            DateTime now = ts[0];
            positionHistory.Insert(0, new Tuple<DateTime, double>(now, offset));
            if (positionHistory.Count > 10)
                positionHistory.RemoveAt(10);
            
            // if we have the two previous frames of finehistory, use that to estimate velocity
            if (!(positionHistory.Count > 2 &&
                  positionHistory[1].Item1 == ts[1] &&
                  positionHistory[2].Item1 == ts[2]))
            {
                return 0; // not enough frames of history
            }
            */
            double v, a_current;
            KalmanStep(offset, out offset_fixed, out v, out a_current);
            double future_offset = offset_fixed + 5 * v;

            var a_desired = QuadraticController(offset_fixed, v);
            //var a_delta = a_desired - a_current;
            var a_delta = Clamp(-2*v - future_offset/5, 5);
            KalmanInput(a_delta);
            file.WriteLineAsync(string.Format("{0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}", offset, offset_fixed, v, a_delta));
            return a_delta;
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

        private KalmanFilter k = new KalmanFilter(dynamParams: 3, measureParams: 1, controlParams: 1, type: MatType.CV_32F);
        private Mat positionMeasurement = new Mat(new Size(1, 1), MatType.CV_32F);
        private Mat accelerationInput = new Mat(new Size(1, 1), MatType.CV_32F);
        
        /// <summary>
        /// Perform a single timestep of the simulation/measurement to remove most of the noise from position/velocity measurements.
        /// </summary>
        /// <param name="x">the measured (noisy) position</param>
        /// <param name="dt1">the current timestamp</param>
        /// <param name="x_better">a better position estimate, taking into account the last few readings and control inputs</param>
        /// <param name="v">a really smooth but not laggy velocity estimate, taking into account the last few readings and control inputs</param>
        /// <param name="a">best guess for the current acceleration that will be applied if we don't move the mouse</param>
        public void KalmanStep(double x, out double x_better, out double v, out double a)
        {
            // Given the last tick's input, project forward and see where we end up
            k.Predict(accelerationInput);

            // add the current (noisy) measurement of our position to the state
            positionMeasurement.SetIdentity(x);
            k.Correct(positionMeasurement);
             
            // retrieve the best-guess of our current position and velocity, which take into account the last few measurements
            x_better = k.StatePost.At<float>(0, 0);
            v = k.StatePost.At<float>(0, 1);
            a = k.StatePost.At<float>(0, 2);
        }

        /// <summary>
        /// Tell the kalman filter how much acceleration we applied, so it can better predict the state at the next tick
        /// </summary>
        /// <param name="a"></param>
        public void KalmanInput(double a)
        {
            accelerationInput.SetIdentity(a);
        }
        
        /// <summary>
        /// Initialize the kalman filter for this controller so that we can call KalmanStep to get smoothed position/velocity estimates
        /// </summary>
        /// <param name="measurementError">stddev^2 (in px^2) of measurement's gaussian distribution. increase this for slower and smoother response</param>
        /// <param name="controlGain">how much a control movement affects the current acceleration. Basically the mouse sensitivity: (resulting acceleration px/s/s) / (mouse px)</param>
        public void KalmanInit(double measurementError = 2, double accelerationGain = 0.01)
        {
            float timedelta = 1f;

            positionMeasurement.SetIdentity(0); // this tell the filter the position readings
            accelerationInput.SetIdentity(0); // this is how we tell the filter how much acceleration we are applying

            /// this is the internal state of the filter -- position, velocity, and acceleration. 
            /// It holds the current best estimate of the actual state of the system
            ///  -- we can't just rely on the most recent measurement because of noise in the measurements.
            k.StatePre.SetArray(row: 0, col: 0, data: new double[] { 0, 0, 0 });

            // this matrix is used to update our state estimate at each step
            k.TransitionMatrix.SetArray(row: 0, col: 0, data: new float[,] {
                { 1, timedelta, timedelta * timedelta / 2 }, // position = old position + v Δt + a Δt^2 /2
                { 0, 1, timedelta }, // velocity = old velocity + a Δt
                { 0, 0, 0.9f } }); // acceleration = old acceleration * 0.9 (natural decay due to relative mouse)

            // this matrix indicates how much each control parameter affects each state variable
            k.ControlMatrix.SetArray(row: 0, col: 0, data: new double[,] { { 0 }, { 0 }, { accelerationGain } });

            // No idea what these are, messed around with values until they seemed good
            k.MeasurementMatrix.SetIdentity();
            k.ProcessNoiseCov.SetIdentity(0.01); // stddev^2 of Transition error gaussian distribution. increase this to rely more on measurements (faster but noiser response)
            k.MeasurementNoiseCov.SetIdentity(measurementError);
            k.ErrorCovPost.SetIdentity(0.01); // large values (100) make initial transients huge
            k.ErrorCovPre.SetIdentity(0); // large values make initial transients huge
        }
    }
}
