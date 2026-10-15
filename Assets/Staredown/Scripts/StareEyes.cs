using UnityEngine;

namespace Staredown
{
    public enum StareEyeCapability { Checking, EyeTracking, ButtonOnly }
    public static class StareEyeDevice
    {
        public static StareEyeCapability Resolve(bool desktop,bool initialized,OVRPlugin.SystemHeadset headset,bool eyeSupported,bool faceSupported)
        {
            if(desktop)return StareEyeCapability.ButtonOnly;
            if(!initialized)return StareEyeCapability.Checking;
            // Hardware identity survives disabled services and denied permissions.
            if(headset==OVRPlugin.SystemHeadset.Meta_Quest_Pro||headset==OVRPlugin.SystemHeadset.Meta_Link_Quest_Pro)return StareEyeCapability.EyeTracking;
            if(eyeSupported&&faceSupported)return StareEyeCapability.EyeTracking;
            return headset==OVRPlugin.SystemHeadset.None?StareEyeCapability.Checking:StareEyeCapability.ButtonOnly;
        }
    }
    public sealed class StareEyes : MonoBehaviour
    {
        public bool simulated; public bool Valid { get; private set; }
        public float Left { get; private set; } public float Right { get; private set; }
        public float Yaw { get; private set; } public float Pitch { get; private set; }
        public float Mouth { get; private set; } public bool MouthTracked { get; private set; }
        public int BlinkSequence { get; private set; }
        public bool Ready => Valid && gate.Calibrated;
        public StareEyeCapability Capability {get;private set;}=StareEyeCapability.Checking;
        public bool CanChooseEyeMode=>Capability==StareEyeCapability.EyeTracking;
        public string Status => Capability==StareEyeCapability.Checking?"DETECTING HEADSET...":simulated ? (!Ready ? "BUTTON DEMO / RELEASE TO GET READY" : "BUTTON DEMO / A, X OR BLINK") : !requested ? "ENABLE EYE TRACKING" : !FacePermission ? "ALLOW FACE AND EYE TRACKING IN HEADSET" : !Valid ? "ENABLE FACE / EYE TRACKING IN QUEST SETTINGS" : !Ready ? "LOOK FORWARD / OPEN EYES 2s" : "EYES CALIBRATED";
        readonly BlinkGate gate = new BlinkGate(); OVRFaceExpressions face; bool requested; float retryAt, demoBlinkUntil,nextDeviceCheck;
        public void RequestDemoBlink(){if(simulated)demoBlinkUntil=Time.unscaledTime+.16f;}
        public static bool DesktopSimulation => Application.isEditor || !Application.isMobilePlatform;
        bool FacePermission => OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.FaceTracking);
        public void UseSimulation(bool value) { simulated = value||Capability==StareEyeCapability.ButtonOnly; Valid = false; Left = Right = 0; Calibrate(); }
        public void Calibrate()
        {
            gate.Reset(); demoBlinkUntil=0; if (Capability==StareEyeCapability.Checking)return;
            if (simulated) { if(face!=null)face.enabled=false; return; }
            requested = true;
            OVRPermissionsRequester.Request(new[] { OVRPermissionsRequester.Permission.FaceTracking, OVRPermissionsRequester.Permission.EyeTracking });
            if (face == null) face = gameObject.AddComponent<OVRFaceExpressions>();
            else face.enabled = true;
        }
        void Awake() { simulated = DesktopSimulation; }
        void Start() { DetectDevice(); }
        void DetectDevice()
        {
            if(Capability!=StareEyeCapability.Checking||Time.unscaledTime<nextDeviceCheck)return;nextDeviceCheck=Time.unscaledTime+.5f;
            if(!DesktopSimulation&&!OVRPlugin.initialized)return;
            var capability=DesktopSimulation?StareEyeCapability.ButtonOnly:StareEyeDevice.Resolve(false,OVRPlugin.initialized,OVRPlugin.GetSystemHeadsetType(),OVRPlugin.eyeTrackingSupported,OVRPlugin.faceTrackingSupported||OVRPlugin.faceTracking2Supported);
            ApplyCapability(capability);
        }
        void ApplyCapability(StareEyeCapability value)
        {
            if(value==StareEyeCapability.Checking||value==Capability)return;
            Capability=value;simulated=value==StareEyeCapability.ButtonOnly;Valid=false;Left=Right=0;Calibrate();
            Debug.Log("[STARE INPUT MODE] detected="+Capability+" mode="+(simulated?"button demo":"real eyes"));
        }
        void Update()
        {
            DetectDevice();if(Capability==StareEyeCapability.Checking){Valid=false;Left=Right=Mouth=0;MouthTracked=false;return;}
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            Mouth=0;MouthTracked=false;
            if (simulated)
            {
                Valid = !StareDesktopInput.KeyHeld(KeyCode.T);
                Left = Right = StareDesktopInput.KeyHeld(KeyCode.B) || StareControllerAim.DemoBlinkHeld() || Time.unscaledTime<demoBlinkUntil ? 1 : 0;
                Yaw = Mathf.Sin(Time.unscaledTime * .65f) * 5; Pitch = 0;
            }
            else
            {
                if (requested && FacePermission && face != null && !face.enabled && Time.unscaledTime >= retryAt)
                { retryAt = Time.unscaledTime + 2; face.enabled = true; }
                Left = Right = Yaw = Pitch = 0;
                Valid = face != null && face.ValidExpressions && face.TryGetFaceTrackingDataSource(out var source) && source == OVRFaceExpressions.FaceTrackingDataSource.Visual;
                if (Valid)
                {
                    bool hasLeft = face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.EyesClosedL, out float l);
                    bool hasRight = face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.EyesClosedR, out float r);
                    Valid = hasLeft && hasRight;
                    float down = 0;
                    if (face.EyeFollowingBlendshapesValid)
                    {
                        face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.EyesLookDownL, out float dl);
                        face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.EyesLookDownR, out float dr);
                        down = Mathf.Min(dl, dr);
                        face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.EyesLookLeftL, out float gl);
                        face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.EyesLookRightR, out float gr);
                        Yaw = (gr - gl) * 35; Pitch = down * 25;
                    }
                    Left = Mathf.Clamp01(l + down); Right = Mathf.Clamp01(r + down);
                    float jaw;MouthTracked=face.TryGetFaceExpressionWeight(OVRFaceExpressions.FaceExpression.JawDrop,out jaw);Mouth=MouthTracked?Mathf.Clamp01(jaw):0;
                }
            }
            if (gate.Sample(Left, Right, Valid, dt)) BlinkSequence++;
        }
    }
}
