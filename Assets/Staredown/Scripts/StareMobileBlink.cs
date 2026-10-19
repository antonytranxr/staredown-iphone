using System;

namespace Staredown
{
    // Sample timestamps, rather than render frames, determine a blink. A camera
    // result held across many Unity Updates can never create an extra blink.
    public sealed class StareMobileBlink
    {
        public const double StaleAfter = .25;
        public const double CalibrationSeconds = 1.5;
        public const double ClosureSeconds = .030, MaximumCaptureGap = .12;
        public bool Valid { get; private set; }
        public bool Ready => Valid && calibrated;
        public int BlinkSequence { get; private set; }
        public float Left { get; private set; }
        public float Right { get; private set; }
        public int CaptureGaps { get; private set; }
        public float CalibrationProgress => Ready ? 1 : !Valid || double.IsNaN(openStart) ? 0 :
            (float)Math.Max(0, Math.Min(1, (lastCapture-openStart)/CalibrationSeconds));
        bool calibrated, armed;
        int closedSamples;
        double blinkFloor = double.NegativeInfinity;
        double lastCapture = double.NaN, lastReceived = double.NegativeInfinity;
        double openStart = double.NaN, closedStart = double.NaN;

        public void Reset()
        {
            Valid = calibrated = armed = false;
            Left = Right = 0;
            lastCapture = openStart = closedStart = double.NaN;
            lastReceived = double.NegativeInfinity;
            closedSamples = 0; blinkFloor = double.NegativeInfinity;
            // Keep sequence monotonic across calibration and camera restarts.
        }

        // Set at the local Go boundary so queued countdown captures cannot
        // become new losing events when a slow render frame finally drains.
        public void IgnoreBlinksThrough(double captureTimestamp) { blinkFloor = captureTimestamp; }

        public bool Sample(float left, float right, bool valid, double captureTimestamp, double receivedAt)
        {
            if (double.IsNaN(captureTimestamp) || double.IsInfinity(captureTimestamp) ||
                double.IsNaN(receivedAt) || double.IsInfinity(receivedAt)) return false;
            if (!double.IsNaN(lastCapture) && captureTimestamp <= lastCapture) return false;
            bool gap = !double.IsNaN(lastCapture) && captureTimestamp - lastCapture > MaximumCaptureGap;
            lastCapture = captureTimestamp; lastReceived = receivedAt;
            if (!valid || float.IsNaN(left) || float.IsNaN(right) || float.IsInfinity(left) || float.IsInfinity(right))
            { LoseTracking(); return false; }
            if (gap) { CaptureGaps++; LoseTracking(); }
            Valid = true;
            Left = Math.Max(0, Math.Min(1, left)); Right = Math.Max(0, Math.Min(1, right));
            if (Left < .30f && Right < .30f)
            {
                if (double.IsNaN(openStart)) openStart = captureTimestamp;
                if (captureTimestamp - openStart >= CalibrationSeconds) calibrated = true;
                armed = true; closedStart = double.NaN; closedSamples = 0;
                return false;
            }
            openStart = double.NaN;
            if (!calibrated || !armed) { closedStart = double.NaN; closedSamples = 0; return false; }
            if (Math.Max(Left, Right) < .72f) { closedStart = double.NaN; closedSamples = 0; return false; }
            if (double.IsNaN(closedStart)) closedStart = captureTimestamp;
            closedSamples++;
            // At 30 Hz, two closed captures span 33 ms. Requiring 45 ms forced
            // three captures and discarded that real short closure. Never
            // accept a single noisy peak; 60 Hz still needs three captures.
            if (closedSamples < 2 || captureTimestamp - closedStart < ClosureSeconds) return false;
            armed = false; closedStart = double.NaN; closedSamples = 0;
            if (captureTimestamp <= blinkFloor) return false;
            BlinkSequence++;
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
            Left = Right = 0; openStart = closedStart = double.NaN; closedSamples = 0;
        }
    }
}
