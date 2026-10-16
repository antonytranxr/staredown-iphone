using System;

namespace Staredown
{
    // Sample timestamps, rather than render frames, determine a blink. A camera
    // result held across many Unity Updates can never create an extra blink.
    public sealed class StareMobileBlink
    {
        public const double StaleAfter = .25;
        public bool Valid { get; private set; }
        public bool Ready => Valid && calibrated;
        public int BlinkSequence { get; private set; }
        public float Left { get; private set; }
        public float Right { get; private set; }
        bool calibrated, armed;
        double lastCapture = double.NaN, lastReceived = double.NegativeInfinity;
        double openStart = double.NaN, closedStart = double.NaN;

        public void Reset()
        {
            Valid = calibrated = armed = false;
            Left = Right = 0;
            lastCapture = openStart = closedStart = double.NaN;
            lastReceived = double.NegativeInfinity;
            // Keep sequence monotonic across calibration and camera restarts.
        }

        public bool Sample(float left, float right, bool valid, double captureTimestamp, double receivedAt)
        {
            if (double.IsNaN(captureTimestamp) || double.IsInfinity(captureTimestamp) ||
                double.IsNaN(receivedAt) || double.IsInfinity(receivedAt)) return false;
            if (!double.IsNaN(lastCapture) && captureTimestamp <= lastCapture) return false;
            bool gap = !double.IsNaN(lastCapture) && captureTimestamp - lastCapture > StaleAfter;
            lastCapture = captureTimestamp; lastReceived = receivedAt;
            if (!valid || float.IsNaN(left) || float.IsNaN(right) || float.IsInfinity(left) || float.IsInfinity(right))
            { LoseTracking(); return false; }
            if (gap) LoseTracking();
            Valid = true;
            Left = Math.Max(0, Math.Min(1, left)); Right = Math.Max(0, Math.Min(1, right));
            if (Left < .30f && Right < .30f)
            {
                if (double.IsNaN(openStart)) openStart = captureTimestamp;
                if (captureTimestamp - openStart >= 1.5) calibrated = true;
                armed = true; closedStart = double.NaN;
                return false;
            }
            openStart = double.NaN;
            if (!calibrated || !armed) { closedStart = double.NaN; return false; }
            if (Math.Max(Left, Right) < .72f) { closedStart = double.NaN; return false; }
            if (double.IsNaN(closedStart)) closedStart = captureTimestamp;
            if (captureTimestamp - closedStart < .045) return false;
            armed = false; closedStart = double.NaN; BlinkSequence++;
            return true;
        }

        public void Tick(double now)
        {
            if (now - lastReceived > StaleAfter) LoseTracking();
        }
        void LoseTracking()
        {
            // Calibration belongs to this explicit camera session. A brief
            // missing face must not restart the round or grant a fresh 1.5 s
            // calibration window on return. Only fresh captures can complete
            // a blink; the host separately forfeits a sustained missing face.
            Valid = false;
            Left = Right = 0; openStart = closedStart = double.NaN;
        }
    }
}
