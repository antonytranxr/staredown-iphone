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
        public Texture Preview => preview;
        public float Left => blink.Left;
        public float Right => blink.Right;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Valid => Running && blink.Valid;
        public bool Ready => Running && blink.Ready;
        public bool Running { get; private set; }
        public int BlinkSequence => blink.BlinkSequence;
        public string Status => !Running ? status : !Valid ? "FACE CAMERA / LOOK FORWARD IN GOOD LIGHT" :
            !Ready ? "EYES OPEN / CALIBRATING" : "LIVE / EYES TRACKED";
        public event Action<byte[]> FrameReady;

        [Serializable] sealed class Sample
        {
            public long sequence;
            public double timestamp, age;
            public bool valid;
            public float left, right, yaw, pitch;
        }
        readonly StareMobileBlink blink = new StareMobileBlink();
        string status = "TAP ENABLE CAMERA";
        WebCamTexture webcam;
        Texture2D preview;
        Color32[] pixels, thumb;
        int[] argb;
        long sampleSequence, nativeFrameSequence;
        float nextAnalysis, nextFrame;
        Coroutine permission;
        bool testCapture;
        int generation;
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
        AndroidJavaClass detector;
#endif
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
        [DllImport("__Internal")][return: MarshalAs(UnmanagedType.I1)] static extern bool StareMobileIOSSupported();
        [DllImport("__Internal")][return: MarshalAs(UnmanagedType.I1)] static extern bool StareMobileIOSStart();
        [DllImport("__Internal")] static extern void StareMobileIOSStop();
        [DllImport("__Internal")] static extern IntPtr StareMobileIOSPoll();
        [DllImport("__Internal")] static extern long StareMobileIOSFrameSequence();
        [DllImport("__Internal")] static extern IntPtr StareMobileIOSCopyFrame(out int length);
        [DllImport("__Internal")] static extern void StareMobileIOSFree(IntPtr pointer);
#endif
        public void StartCamera()
        {
            if (Running || permission != null) return;
            int token = ++generation;
            permission = StartCoroutine(RequestCamera(token));
        }
        IEnumerator RequestCamera(int token)
        {
            status = "ALLOW FRONT CAMERA TO PLAY";
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
            { status = "CAMERA OFF / ENABLE IN PHONE SETTINGS"; permission = null; yield break; }
#elif UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (generation != token) yield break;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            { status = "CAMERA OFF / ENABLE IN PHONE SETTINGS"; permission = null; yield break; }
#else
            yield return null;
#endif
            if (generation != token) yield break;
            permission = null;
            BeginCamera();
        }
        void BeginCamera()
        {
            Calibrate(); testCapture = false;
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
            if (!StareMobileIOSSupported()) { status = "FACE TRACKING IS NOT SUPPORTED ON THIS PHONE"; return; }
            Running = StareMobileIOSStart();
            status = Running ? "FINDING YOUR FACE" : "CAMERA COULD NOT START";
#elif UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
            try
            {
                detector = new AndroidJavaClass("com.antony.staredown.mobile.StaredownFaceDetector");
                if (!detector.CallStatic<bool>("start")) { StopNative(); status = "FACE TRACKER COULD NOT START"; return; }
                string device = null;
                foreach (var candidate in WebCamTexture.devices) if (candidate.isFrontFacing) { device = candidate.name; break; }
                if (device == null) { status = "NO FRONT CAMERA FOUND"; StopNative(); return; }
                webcam = new WebCamTexture(device, 640, 480, 30);
                webcam.Play(); Running = true; status = "FINDING YOUR FACE";
            }
            catch (Exception error) { Debug.LogWarning("[STARE MOBILE CAMERA] " + error.Message); StopCamera(); status = "CAMERA COULD NOT START / TRY AGAIN"; }
#else
            status = "FACE CAMERA TRACKING REQUIRES A PHONE";
#endif
            nextAnalysis = nextFrame = 0;
            sampleSequence = nativeFrameSequence = 0;
        }
        public void Calibrate()
        {
            blink.Reset(); Yaw = Pitch = 0;
        }
        public void StopCamera()
        {
            generation++;
            if (permission != null) { StopCoroutine(permission); permission = null; }
            Running = testCapture = false;
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
            if (!Running) return;
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
        void AcceptSample(Sample sample)
        {
            if (sample == null || sample.sequence <= sampleSequence) return;
            sampleSequence = sample.sequence;
            bool valid = sample.valid && sample.age <= StareMobileBlink.StaleAfter && sample.age >= 0 &&
                Mathf.Abs(sample.yaw) <= 18 && Mathf.Abs(sample.pitch) <= 25;
            blink.Sample(sample.left, sample.right, valid, sample.timestamp, Time.realtimeSinceStartupAsDouble);
            Yaw = valid ? sample.yaw : 0; Pitch = valid ? sample.pitch : 0;
        }
#if UNITY_ANDROID && STARE_MOBILE && !UNITY_EDITOR
        void UpdateAndroid()
        {
            if (detector == null || webcam == null) return;
            string json = detector.CallStatic<string>("poll");
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
                nextFrame = Time.unscaledTime + StreamInterval;
                NormalizeThumbnail(pixels, webcam.width, webcam.height, webcam.videoRotationAngle, webcam.videoVerticallyMirrored);
                if (Valid) PublishJpeg(EncodeBounded(preview));
            }
        }
#endif
#if UNITY_IOS && STARE_MOBILE && !UNITY_EDITOR
        void UpdateIOS()
        {
            IntPtr pointer = StareMobileIOSPoll();
            if (pointer != IntPtr.Zero)
            {
                try { AcceptSample(JsonUtility.FromJson<Sample>(Marshal.PtrToStringAnsi(pointer))); }
                finally { StareMobileIOSFree(pointer); }
            }
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + StreamInterval;
            long serial = StareMobileIOSFrameSequence();
            if (serial <= nativeFrameSequence) return;
            nativeFrameSequence = serial;
            int length; pointer = StareMobileIOSCopyFrame(out length);
            if (pointer == IntPtr.Zero) return;
            try
            {
                if (length <= 0 || length > MaxFrameBytes) return;
                var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
                EnsurePreview(); if (preview.LoadImage(bytes, false) && Valid) PublishJpeg(bytes);
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
                // Mirror front preview and head feed once, like a selfie camera.
                float u = .5f + ((FrameSize - 1 - x + .5f) / FrameSize - .5f) * side / uprightW;
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
            if (!Running || !Valid || bytes == null || bytes.Length < 4 || bytes.Length > MaxFrameBytes || bytes[0] != 255 || bytes[1] != 216) return;
            FrameReady?.Invoke(bytes);
        }
#if UNITY_EDITOR
        public void BeginTestCapture() { StopCamera(); testCapture = Running = true; status = "EDITOR CAMERA FIXTURE"; sampleSequence = 0; }
        public void SubmitTestSample(float left, float right, bool valid, double timestamp, long sequence, float yaw = 0, float pitch = 0)
        {
            if (testCapture && Running) AcceptSample(new Sample { left = left, right = right, valid = valid, timestamp = timestamp, sequence = sequence, yaw = yaw, pitch = pitch });
        }
        public void SubmitTestFrame(byte[] jpeg) { if (testCapture) { EnsurePreview(); if(preview.LoadImage(jpeg,false)) PublishJpeg(jpeg); } }
#endif
        void OnDisable() { StopCamera(); }
        void OnDestroy() { StopCamera(); }
        void OnApplicationPause(bool paused) { if (paused) { StopCamera(); status = "CAMERA PAUSED / TAP ENABLE CAMERA"; } }
    }
}
