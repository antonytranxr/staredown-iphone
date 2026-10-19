using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Staredown
{
    // The front camera is opt-in. Face classification remains on the phone;
    // only bounded thumbnail frames are sent to the current room by subscribers.
    public sealed class StareMobileCamera : MonoBehaviour
    {
        public const int FrameSize = 160, MaxFrameBytes = 5000;
        public const float StreamInterval = 1f / 6f;
        // Canonical upright, unmirrored texture. Only the local HUD applies a
        // single horizontal UV flip; other contestants receive the real view.
        public Texture Preview => preview;
        public float Left => blink.Left;
        public float Right => blink.Right;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Valid => Running && !historyLost && blink.Valid;
        public bool Ready => Running && !historyLost && blink.Ready;
        public bool Running { get; private set; }
        public bool CameraEnabledIntent { get; private set; }
        public bool Recovering => CameraEnabledIntent && (paused || restartPending || permission != null);
        public string DiagnosticsSummary { get; private set; } = "Eye capture not started";
        public int BlinkSequence => blink.BlinkSequence;
        public float CalibrationProgress => Running ? blink.CalibrationProgress : 0;
        public int CalibrationPercent => Mathf.RoundToInt(CalibrationProgress*100);
        public string Status => Recovering ? "CAMERA RESUMING / HOLD STILL" : !Running ? status : historyLost ? "TRACKING HISTORY LOST / RECALIBRATE" : !Valid ? TrackingGuidance :
            !Ready ? "KEEP EYES OPEN / "+CalibrationPercent+"%" : "READY / EYES TRACKED";
        string TrackingGuidance => trackingReason == 2 ? "ONLY ONE FACE IN VIEW" :
            trackingReason == 3 ? "SHOW BOTH EYES IN GOOD LIGHT" : trackingReason == 4 ? "FACE THE FRONT CAMERA" :
            trackingReason == 5 ? "CAMERA INTERRUPTED / TRY AGAIN" : trackingReason == 6 ? "CAMERA DELAYED / HOLD STILL" :
            "CAMERA LIVE / KEEP YOUR FACE IN VIEW";
        public event Action<byte[]> FrameReady;
        public event Action<int,double> BlinkCaptured;

        [Serializable] sealed class Sample
        {
            public long sequence;
            public double timestamp, age;
            public bool valid;
            public int reason;
            public float left, right, yaw, pitch;
        }
        [Serializable] sealed class SampleBatch
        {
            public Sample[] samples;
            public long overflow,dropped,nativeSamples,encodedFrames,encodeSkips,captureGaps;
            public float nativeFPS,encodeMs; public int formatFPS;
        }
        readonly StareMobileBlink blink = new StareMobileBlink();
        string status = "TAP ENABLE CAMERA";
        WebCamTexture webcam;
        Texture2D preview;
        Color32[] pixels, thumb;
        int[] argb;
        long sampleSequence, nativeFrameSequence;
        float nextAnalysis, nextFrame, nextThermal, nextOrientation;
        int thermalLevel, trackingReason;
        Coroutine permission;
        bool testCapture;
        int generation;
        bool paused,restartPending,historyLost,contestActive;
        float restartAt,diagnosticsAt,cadenceAt,consumedFPS,videoFPS;
        int consumedWindow,videoWindow; long consumedSamples;
        double lastCaptureTimestamp;
        SampleBatch nativeDiagnostics;
#if UNITY_EDITOR
        bool resumeTestCapture;
#endif
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
        AndroidJavaClass detector;
#endif
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
        [DllImport("__Internal")][return: MarshalAs(UnmanagedType.I1)] static extern bool StareMobileIOSSupported();
        [DllImport("__Internal")][return: MarshalAs(UnmanagedType.I1)] static extern bool StareMobileIOSStart();
        [DllImport("__Internal")] static extern void StareMobileIOSStop();
        [DllImport("__Internal")] static extern void StareMobileIOSRefreshOrientation();
        [DllImport("__Internal")] static extern IntPtr StareMobileIOSDrainSamples();
        [DllImport("__Internal")] static extern void StareMobileIOSResetSamples();
        [DllImport("__Internal")] static extern double StareMobileIOSLatestCaptureTimestamp();
        [DllImport("__Internal")] static extern int StareMobileIOSThermalLevel();
        [DllImport("__Internal")] static extern IntPtr StareMobileIOSCopyFrameAfter(long previousSequence, out long sequence, out int length);
        [DllImport("__Internal")] static extern void StareMobileIOSFree(IntPtr pointer);
#endif
        public int ThermalLevel => thermalLevel;
        // Keep detection at camera cadence; only the decorative video thumbnail
        // slows down under thermal pressure. Real blink timing is unchanged.
        float CurrentStreamInterval => thermalLevel >= 2 ? .25f : StreamInterval;
        public void RefreshOrientation()
        {
            nextOrientation = Time.unscaledTime + .25f;
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            StareMobileIOSRefreshOrientation();
#endif
        }
        public void StartCamera()
        {
            CameraEnabledIntent = true;
            if (paused) { restartPending = true; return; }
            if (Running) { restartPending = false; return; }
            if (permission != null) return;
            restartPending = false;
            int token = ++generation;
            permission = StartCoroutine(RequestCamera(token));
        }
        IEnumerator RequestCamera(int token)
        {
            status = "ALLOW FRONT CAMERA TO PLAY";
            // StartCoroutine may otherwise complete synchronously when the OS
            // permission is already granted, then overwrite permission=null
            // with its returned handle and leave Recovering stuck forever.
            yield return null;
            if (generation != token || !CameraEnabledIntent) yield break;
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                bool finished = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += value => finished = true;
                callbacks.PermissionDenied += value => finished = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
                float timeout = Time.realtimeSinceStartup + 60;
                while (!finished && Time.realtimeSinceStartup < timeout && generation == token) yield return null;
            }
            if (generation != token) yield break;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            { StartFailed("CAMERA OFF / ENABLE IN PHONE SETTINGS"); yield break; }
#elif UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (generation != token) yield break;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            { StartFailed("CAMERA OFF / ENABLE IN PHONE SETTINGS"); yield break; }
#else
            yield return null;
#endif
            if (generation != token) yield break;
            permission = null;
            if (!CameraEnabledIntent) yield break;
            if (paused) { restartPending = true; yield break; }
            restartPending = false; BeginCamera();
        }
        void BeginCamera()
        {
            Calibrate(); testCapture = false;
#if UNITY_EDITOR
            if (resumeTestCapture) { resumeTestCapture = false; testCapture = Running = true; status = "EDITOR CAMERA FIXTURE"; return; }
#endif
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            if (!StareMobileIOSSupported()) { StartFailed("FACE TRACKING IS NOT SUPPORTED ON THIS PHONE"); return; }
            Running = StareMobileIOSStart();
            if (!Running) { StartFailed("CAMERA COULD NOT START"); return; }
            status = "FINDING YOUR FACE";
#elif UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
            try
            {
                detector = new AndroidJavaClass("com.antony.staredown.mobile.StaredownFaceDetector");
                if (!detector.CallStatic<bool>("start")) { StopNative(); StartFailed("FACE TRACKER COULD NOT START"); return; }
                string device = null;
                foreach (var candidate in WebCamTexture.devices) if (candidate.isFrontFacing) { device = candidate.name; break; }
                if (device == null) { StopNative(); StartFailed("NO FRONT CAMERA FOUND"); return; }
                webcam = new WebCamTexture(device, 640, 480, 30);
                webcam.Play(); Running = true; status = "FINDING YOUR FACE";
            }
            catch (Exception error) { Debug.LogWarning("[STARE MOBILE CAMERA] " + error.Message); StopCamera(); status = "CAMERA COULD NOT START / TRY AGAIN"; }
#else
            StartFailed("FACE CAMERA TRACKING REQUIRES A PHONE");
#endif
            nextAnalysis = nextFrame = 0;
            sampleSequence = nativeFrameSequence = 0;
        }
        void StartFailed(string message)
        { CameraEnabledIntent = Running = restartPending = false; permission = null; status = message; }
        public void Calibrate()
        {
            blink.Reset(); historyLost = false; sampleSequence = 0; Yaw = Pitch = 0; trackingReason = 1;
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            if (Running) StareMobileIOSResetSamples();
#endif
        }
        public void StopCamera()
        {
            CameraEnabledIntent = restartPending = false;
#if UNITY_EDITOR
            resumeTestCapture = false;
#endif
            StopCapture(true);
        }
        void StopCapture(bool cancelPermission)
        {
            if (cancelPermission)
            {
                generation++;
                if (permission != null) { StopCoroutine(permission); permission = null; }
            }
            Running = testCapture = false;
            contestActive = false;
            if (webcam != null) { webcam.Stop(); Destroy(webcam); webcam = null; }
            StopNative(); Calibrate();
            if (preview != null) { Destroy(preview); preview = null; }
            pixels = thumb = null; argb = null;
            status = "TAP ENABLE CAMERA";
        }
        void StopNative()
        {
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
            if (detector != null) { try { detector.CallStatic("stop"); } catch (Exception) {} detector.Dispose(); detector = null; }
#elif UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            StareMobileIOSStop();
#endif
        }
        void Update()
        {
            if (restartPending && !paused && permission == null && Time.unscaledTime >= restartAt && CameraEnabledIntent) StartCamera();
            UpdateDiagnostics();
            if (Time.unscaledTime >= nextThermal)
            {
                nextThermal = Time.unscaledTime + 3;
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
                thermalLevel = StareMobileIOSThermalLevel();
#endif
            }
            if (!Running) return;
            if (Time.unscaledTime >= nextOrientation) RefreshOrientation();
            if (!testCapture)
            {
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
                UpdateAndroid();
#elif UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
                UpdateIOS();
#endif
            }
            blink.Tick(Time.realtimeSinceStartupAsDouble);
        }
        public void SetContestActive(bool active)
        {
            if (active && !contestActive)
            {
                double cutoff = lastCaptureTimestamp;
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
                if (Running) cutoff = StareMobileIOSLatestCaptureTimestamp();
#endif
                blink.IgnoreBlinksThrough(cutoff);
            }
            else if (!active && contestActive) blink.IgnoreBlinksThrough(double.NegativeInfinity);
            contestActive = active;
        }
        void AcceptSample(Sample sample, bool replay = false)
        {
            if (sample == null || sample.sequence <= sampleSequence) return;
            sampleSequence = sample.sequence;
            bool ageValid = !double.IsNaN(sample.age) && !double.IsInfinity(sample.age) && sample.age >= 0;
            bool fresh = ageValid && sample.age <= StareMobileBlink.StaleAfter;
            bool facing = Mathf.Abs(sample.yaw) <= 18 && Mathf.Abs(sample.pitch) <= 25;
            bool valid = !historyLost && sample.valid && (replay ? ageValid : fresh) && facing;
            trackingReason = !fresh ? 6 : !facing ? 4 : sample.reason;
            double capturedAt=Time.realtimeSinceStartupAsDouble - (ageValid ? sample.age : 0);
            if(blink.Sample(sample.left, sample.right, valid, sample.timestamp, capturedAt))
                BlinkCaptured?.Invoke(blink.BlinkSequence,capturedAt);
            lastCaptureTimestamp = sample.timestamp; consumedSamples++; consumedWindow++;
            Yaw = valid ? sample.yaw : 0; Pitch = valid ? sample.pitch : 0;
        }
        void AcceptBatch(SampleBatch batch)
        {
            if (batch == null || batch.samples == null || batch.samples.Length > 256) return;
            nativeDiagnostics = batch;
            if (batch.overflow > 0) { blink.Reset(); historyLost = true; }
            foreach (var sample in batch.samples) AcceptSample(sample,true);
            // Replay old closures, but only the newest capture can establish
            // current tracking. A stale batch never makes a paused camera valid.
            blink.Tick(Time.realtimeSinceStartupAsDouble);
        }
        void UpdateDiagnostics()
        {
            float now = Time.unscaledTime;
            if (now-cadenceAt >= 1) { float span=now-cadenceAt; consumedFPS=consumedWindow/span;videoFPS=videoWindow/span;consumedWindow=videoWindow=0;cadenceAt=now; }
            if (now < diagnosticsAt) return; diagnosticsAt = now+.5f;
            var d=nativeDiagnostics;
            DiagnosticsSummary="Eye capture "+(d!=null?d.nativeFPS.ToString("0"):"—")+" Hz / read "+consumedFPS.ToString("0")+" Hz"+
                "\nRequested "+(d!=null?d.formatFPS.ToString():"—")+" Hz / video "+videoFPS.ToString("0.0")+" FPS / blinks "+BlinkSequence+
                "\nSamples "+consumedSamples+" / gaps "+(d!=null?d.captureGaps:blink.CaptureGaps)+" / lost "+(d!=null?d.dropped:0)+
                (d!=null?" / encode "+d.encodeMs.ToString("0.0")+" ms / busy skips "+d.encodeSkips:"");
        }
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
        void UpdateAndroid()
        {
            if (detector == null || webcam == null) return;
            string json = detector.CallStatic<string>("pollAfter", sampleSequence);
            if (!string.IsNullOrEmpty(json))
            {
                var sample = JsonUtility.FromJson<Sample>(json);
                sample.age = Math.Max(0, Time.realtimeSinceStartupAsDouble - sample.timestamp);
                AcceptSample(sample);
            }
            if (!webcam.didUpdateThisFrame || webcam.width < 100 || webcam.height < 100) return;
            bool analyze = Time.unscaledTime >= nextAnalysis && detector.CallStatic<bool>("readyForFrame");
            bool frame = Time.unscaledTime >= nextFrame;
            if (!analyze && !frame) return;
            pixels = webcam.GetPixels32(pixels);
            if (analyze)
            {
                // Some devices ignore the requested camera resolution. Limit
                // JNI copies and detector work while keeping the full face view.
                float scale = Mathf.Min(1, 640f / Mathf.Max(webcam.width, webcam.height));
                int width = Mathf.Max(100, Mathf.RoundToInt(webcam.width * scale));
                int height = Mathf.Max(100, Mathf.RoundToInt(webcam.height * scale));
                if (argb == null || argb.Length != width * height) argb = new int[width * height];
                for (int y = 0; y < height; y++)
                {
                    int rawY = Mathf.Clamp((int)((y + .5f) * webcam.height / height), 0, webcam.height - 1);
                    int sourceY = webcam.videoVerticallyMirrored ? rawY : webcam.height - 1 - rawY;
                    for (int x = 0; x < width; x++)
                    {
                        int sourceX = Mathf.Clamp((int)((x + .5f) * webcam.width / width), 0, webcam.width - 1);
                        var c = pixels[sourceY * webcam.width + sourceX];
                        argb[y * width + x] = (255 << 24) | (c.r << 16) | (c.g << 8) | c.b;
                    }
                }
                detector.CallStatic("analyze", argb, width, height, webcam.videoRotationAngle, Time.realtimeSinceStartupAsDouble);
                nextAnalysis = Time.unscaledTime + 1f / 30f;
            }
            if (frame)
            {
                nextFrame = Time.unscaledTime + CurrentStreamInterval;
                NormalizeThumbnail(pixels, webcam.width, webcam.height, webcam.videoRotationAngle, webcam.videoVerticallyMirrored);
                PublishJpeg(EncodeBounded(preview));
            }
        }
#endif
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
        void UpdateIOS()
        {
            IntPtr pointer = StareMobileIOSDrainSamples();
            if (pointer != IntPtr.Zero)
            {
                try { AcceptBatch(JsonUtility.FromJson<SampleBatch>(Marshal.PtrToStringAnsi(pointer))); }
                finally { StareMobileIOSFree(pointer); }
            }
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + CurrentStreamInterval;
            long serial; int length;
            pointer = StareMobileIOSCopyFrameAfter(nativeFrameSequence, out serial, out length);
            if (pointer == IntPtr.Zero) return;
            nativeFrameSequence = serial;
            try
            {
                if (length <= 0 || length > MaxFrameBytes) return;
                var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
                EnsurePreview(); if (preview.LoadImage(bytes, false)) PublishJpeg(bytes);
            }
            finally { StareMobileIOSFree(pointer); }
        }
#endif
        void EnsurePreview()
        {
            if (preview == null)
                preview = new Texture2D(FrameSize, FrameSize, TextureFormat.RGB24, false) { name = "Live front camera preview", filterMode = FilterMode.Bilinear };
        }
        void NormalizeThumbnail(Color32[] source, int width, int height, int rotation, bool flipped)
        {
            EnsurePreview();
            if (thumb == null) thumb = new Color32[FrameSize * FrameSize];
            bool swapped = rotation == 90 || rotation == 270;
            int uprightW = swapped ? height : width, uprightH = swapped ? width : height;
            float side = Mathf.Min(uprightW, uprightH);
            for (int y = 0; y < FrameSize; y++) for (int x = 0; x < FrameSize; x++)
            {
                // The texture and network frame stay unmirrored. Local selfie
                // presentation is a HUD UV flip, independent of camera pixels.
                float u = .5f + ((x + .5f) / FrameSize - .5f) * side / uprightW;
                float v = .5f + ((y + .5f) / FrameSize - .5f) * side / uprightH;
                float rawU = u, rawV = v;
                if (rotation == 90) { rawU = 1 - v; rawV = u; }
                else if (rotation == 180) { rawU = 1 - u; rawV = 1 - v; }
                else if (rotation == 270) { rawU = v; rawV = 1 - u; }
                if (flipped) rawV = 1 - rawV;
                int sx = Mathf.Clamp((int)(rawU * width), 0, width - 1), sy = Mathf.Clamp((int)(rawV * height), 0, height - 1);
                thumb[y * FrameSize + x] = source[sy * width + sx];
            }
            preview.SetPixels32(thumb); preview.Apply(false, false);
        }
        static byte[] EncodeBounded(Texture2D texture)
        {
            for (int quality = 50; quality >= 10; quality -= 10)
            { var jpeg = texture.EncodeToJPG(quality); if (jpeg != null && jpeg.Length <= MaxFrameBytes) return jpeg; }
            return null;
        }
        void PublishJpeg(byte[] bytes)
        {
            // A camera frame and a usable eye sample are independent. Turning
            // away must keep the live head visible, including the empty view.
            if (!Running || bytes == null || bytes.Length < 4 || bytes.Length > MaxFrameBytes || bytes[0] != 255 || bytes[1] != 216) return;
            FrameReady?.Invoke(bytes);
            videoWindow++;
        }
#if UNITY_EDITOR
        public void BeginTestCapture() { StopCamera(); CameraEnabledIntent = true; testCapture = Running = true; status = "EDITOR CAMERA FIXTURE"; sampleSequence = 0; }
        public void SubmitTestBatchJson(string json) { if(testCapture) AcceptBatch(JsonUtility.FromJson<SampleBatch>(json)); }
        public void SetTestNativeCaptureTimestamp(double timestamp) { lastCaptureTimestamp=timestamp; }
        public void TestApplicationPause(bool value) { OnApplicationPause(value); }
        public void SubmitTestSample(float left, float right, bool valid, double timestamp, long sequence, float yaw = 0, float pitch = 0, int reason = 0, double age = 0)
        {
            if (testCapture && Running) AcceptSample(new Sample { left = left, right = right, valid = valid, timestamp = timestamp, sequence = sequence, yaw = yaw, pitch = pitch, reason = reason, age = age });
        }
        public void SubmitTestFrame(byte[] jpeg) { if (testCapture) { EnsurePreview(); if(preview.LoadImage(jpeg,false)) PublishJpeg(jpeg); } }
        public void SubmitTestPixels(Color32[] source, int width, int height, int rotation, bool flipped)
        { if (testCapture) NormalizeThumbnail(source,width,height,rotation,flipped); }
#endif
        void OnDisable() { StopCamera(); }
        void OnDestroy() { StopCamera(); }
        void OnApplicationPause(bool value)
        {
            paused = value;
            if (value)
            {
#if UNITY_EDITOR
                resumeTestCapture = testCapture;
#endif
                if (CameraEnabledIntent) restartPending = true;
                StopCapture(false); status = "CAMERA PAUSED";
            }
            else if (CameraEnabledIntent) { restartPending = true; restartAt = Time.unscaledTime+.2f; }
        }
    }
}
