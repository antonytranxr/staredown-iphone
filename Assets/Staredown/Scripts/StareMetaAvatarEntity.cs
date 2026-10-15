#if STARE_MOBILE || (UNITY_IOS && !UNITY_EDITOR)
using System;
using UnityEngine;

namespace Staredown
{
    // Preserve the shared view contract without retaining Avatar2 native calls.
    public sealed class StareMetaAvatarView : MonoBehaviour
    {
        public bool Loaded => false;
        public string Error => "Native Meta avatars are unavailable on phones";
        public string PoseStatus => "";
        public bool IsFirstPerson => false;
        public event Action OnLoaded { add { } remove { } }
        public const int MaxPoseBytes = 4096;
        internal void Initialize(string id, bool isLocal) { }
        public void SetEyes(float left, float right, float yaw, float pitch) { }
        public void SetMouth(float opening) { }
        public void SetLocalView(bool useFirstPerson) { }
        public void SetDanceSource(StareAvatarPresence source) { }
        public byte[] CapturePose() => null;
        public bool TryGetPortraitFocus(out Vector3 worldPosition) { worldPosition = Vector3.zero; return false; }
        public void ApplyPose(byte[] bytes) { }
    }
}
#else
using System;
using System.Collections;
using Oculus.Avatar2;
using UnityEngine;

namespace Staredown
{
    public sealed class StareMetaAvatarView : MonoBehaviour
    {
        public bool Loaded => entity != null && entity.PersonalAvatarLoaded && (local || entity.HasReceivedPose);
        public string Error { get; private set; } = "";
        public string PoseStatus { get; private set; } = "";
        public bool IsFirstPerson => local && firstPerson;
        public event Action OnLoaded;
        public const int MaxPoseBytes = 4096;
        StareMetaAvatarEntity entity;
        StareMetaFaceInput face;
        OvrAvatarEyeTrackingBehaviorOvrPlugin gaze;
        StareMetaInput input;
        StareAvatarPresence danceSource;
#if UNITY_ANDROID && !UNITY_EDITOR
        float nextEyeTrackingCheck;
#endif
        string userId;
        bool local, firstPerson, viewApplied, loadedEventSent, inputAnimated;
        byte[] pendingPose;
        Vector4 eyes;

        internal void Initialize(string id, bool isLocal) { userId = id; local = isLocal; }

        IEnumerator Start()
        {
            if (!StareMetaAvatars.ValidId(userId)) { Error = "Invalid Meta user ID"; yield break; }
            var service = StareMetaAvatars.Instance;
            if (service == null) { Error = "Meta Avatar service unavailable"; yield break; }
            if (!service.Authenticated) service.Connect();
            float until = Time.realtimeSinceStartup + 65;
            while (!service.Authenticated && service.Connecting && Time.realtimeSinceStartup < until) yield return null;
            if (!service.Authenticated || !OvrAvatarManager.initialized) { Error = service.Status; yield break; }
            if (local && userId != service.UserId) { Error = "Local avatar identity does not match signed-in user"; yield break; }

            entity = gameObject.AddComponent<StareMetaAvatarEntity>();
            if (local)
            {
                input = gameObject.AddComponent<StareMetaInput>();
                input.AvatarRoot = transform;
                input.DanceSource = danceSource;
                var game = FindFirstObjectByType<StareGame>();
                input.Rig = game != null ? game.rig : FindFirstObjectByType<OVRCameraRig>();
                face = gameObject.AddComponent<StareMetaFaceInput>();
                face.Eyes = eyes;
                gaze = gameObject.AddComponent<OvrAvatarEyeTrackingBehaviorOvrPlugin>();
            }
            try { entity.Begin(ulong.Parse(userId), local, input, face, gaze); }
            catch (Exception) { Error = "Meta Avatar renderer initialization failed"; }
            if (!string.IsNullOrEmpty(Error)) yield break;
            SetLocalView(firstPerson);
            until = Time.realtimeSinceStartup + 45;
            while (!entity.PersonalAvatarLoaded && string.IsNullOrEmpty(entity.Failure) && Time.realtimeSinceStartup < until) yield return null;
            if (!entity.PersonalAvatarLoaded) Error = string.IsNullOrEmpty(entity.Failure) ? "Meta Avatar loading timed out" : entity.Failure;
        }

        void Update()
        {
            if (face != null) face.Eyes = eyes;
            if (entity == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            // The game's calibration owns the permission request. Start the eye service only after
            // that existing request is granted; do not prompt for extra permissions or microphone use.
            if (local && gaze != null && Time.unscaledTime >= nextEyeTrackingCheck)
            {
                nextEyeTrackingCheck = Time.unscaledTime + 1;
                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(OvrAvatarManager.PERMISSION_EYE_TRACKING))
                {
                    if (!OVRPlugin.eyeTrackingEnabled) OVRPlugin.StartEyeTracking();
                    if (OVRPlugin.eyeTrackingEnabled && entity.IsCreated) entity.SetEyePoseProvider(gaze);
                }
            }
#endif
            if (!string.IsNullOrEmpty(entity.Failure)) Error = entity.Failure;
            if (!local && entity.PersonalAvatarLoaded && entity.HasJoints && pendingPose != null)
            {
                var packet = pendingPose;
                pendingPose = null;
                entity.ReceivePose(packet);
            }
            if (entity.IsCreated && entity.Hidden == Loaded) entity.Hidden = !Loaded;
            if (Loaded && !loadedEventSent)
            {
                loadedEventSent = true;
                Error = "";
                foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
                OnLoaded?.Invoke();
            }
        }

        public void SetEyes(float left, float right, float yaw, float pitch)
        {
            if (!Finite(left) || !Finite(right) || !Finite(yaw) || !Finite(pitch)) return;
            eyes = new Vector4(Mathf.Clamp01(left), Mathf.Clamp01(right), Mathf.Clamp(yaw, -35, 35), Mathf.Clamp(pitch, -25, 25));
            // On remotes, Meta's compressed animation stream carries the facial pose. Enabling a local
            // face animation provider there would overwrite streamed animation. Rules use StareEyes separately.
        }
        public void SetMouth(float opening)
        {if(local&&face!=null&&Finite(opening))face.Mouth=Mathf.Clamp01(opening);}

        public void SetLocalView(bool useFirstPerson)
        {
            bool changed = firstPerson != useFirstPerson;
            firstPerson = useFirstPerson;
            if (entity != null && (!viewApplied || changed))
            {
                entity.SetView(local && firstPerson);
                viewApplied = true;
            }
        }
        public void SetDanceSource(StareAvatarPresence source)
        {
            danceSource=source;if(input==null)return;input.DanceSource=source;
            bool animated=input.AnimatedHands;
            if(animated!=inputAnimated&&entity!=null&&entity.IsCreated)entity.SetInputManager(input,true);
            inputAnimated=animated;
        }

        public byte[] CapturePose()
        {
            if (!local || !Loaded || !entity.HasJoints) return null;
            var bytes = entity.RecordStreamData(OvrAvatarEntity.StreamLOD.High);
            if (bytes != null && bytes.Length > MaxPoseBytes) bytes = entity.RecordStreamData(OvrAvatarEntity.StreamLOD.Medium);
            if (bytes != null && bytes.Length > MaxPoseBytes) bytes = entity.RecordStreamData(OvrAvatarEntity.StreamLOD.Low);
            if (bytes == null || bytes.Length == 0) { PoseStatus = "Meta pose is not ready"; return null; }
            if (bytes.Length > MaxPoseBytes) { PoseStatus = "Meta pose exceeds the network packet limit"; return null; }
            PoseStatus = "";
            return bytes;
        }

        public bool TryGetPortraitFocus(out Vector3 worldPosition)
        {
            worldPosition = default;
            if (entity == null || !entity.IsCreated || !entity.PersonalAvatarLoaded || !entity.HasJoints) return false;
            var head = entity.GetSkeletonTransform(CAPI.ovrAvatar2JointType.Head);
            if (head == null) return false;
            worldPosition = head.position;
            return true;
        }

        public void ApplyPose(byte[] bytes)
        {
            if (local || bytes == null || bytes.Length == 0 || bytes.Length > MaxPoseBytes) return;
            pendingPose = (byte[])bytes.Clone();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class StareMetaAvatarEntity : OvrAvatarEntity
    {
        public bool PersonalAvatarLoaded { get; private set; }
        public bool HasReceivedPose { get; private set; }
        public string Failure { get; private set; } = "";
        protected override bool CreateEntityOnAwake => false;

        internal void Begin(ulong id, bool local, OvrAvatarInputManagerBehavior input, OvrAvatarFacePoseBehavior face, OvrAvatarEyePoseBehavior gaze)
        {
            _userId = id;
            SetIsLocal(local);
            _creationInfo.features = local ? CAPI.ovrAvatar2EntityFeatures.Preset_Default : CAPI.ovrAvatar2EntityFeatures.Preset_Remote;
            _creationInfo.features &= ~(CAPI.ovrAvatar2EntityFeatures.UseDefaultModel | CAPI.ovrAvatar2EntityFeatures.UseDefaultFaceAnimations | CAPI.ovrAvatar2EntityFeatures.HandScaling);
            if (!local) _creationInfo.features &= ~CAPI.ovrAvatar2EntityFeatures.UseDefaultAnimHierarchy;
            _creationInfo.renderFilters.quality = CAPI.ovrAvatar2EntityQuality.Light;
            _creationInfo.renderFilters.viewFlags = CAPI.ovrAvatar2EntityViewFlags.All;
            _creationInfo.renderFilters.manifestationFlags = CAPI.ovrAvatar2EntityManifestationFlags.Full;
            _criticalJointTypes = new[] { CAPI.ovrAvatar2JointType.Head };
            SetInitialManifestatation(CAPI.ovrAvatar2EntityManifestationFlags.Full);
            SetInputManager(local ? input : null);
            SetFacePoseProvider(local ? face : null);
            SetEyePoseProvider(local ? gaze : null);
            SetLipSync(null);
            CreateEntity();
            if (!IsCreated) { Failure = "Meta Avatar entity could not be created"; return; }
            Hidden = true;
            SetView(false);
            if (!LoadUserWithFilters(_creationInfo.renderFilters)) Failure = "Meta Avatar request failed: check app access and connection";
        }

        public void SetView(bool firstPerson)
        {
            if (IsCreated) SetActiveView(firstPerson ? CAPI.ovrAvatar2EntityViewFlags.FirstPerson : CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
        }

        internal void ReceivePose(byte[] bytes)
        {
            if (IsLocal || !PersonalAvatarLoaded || !HasJoints) return;
            if (ApplyStreamData(bytes)) HasReceivedPose = true;
        }

        protected override void OnUserAvatarLoaded() { base.OnUserAvatarLoaded(); PersonalAvatarLoaded = true; Failure = ""; }
        protected override void OnLoadFailed(CAPI.ovrAvatar2LoadRequestInfo info)
        {
            base.OnLoadFailed(info);
            Failure = "Meta Avatar could not load: check Avatars access and connection";
        }
    }

    // Runtime-only input components avoid importing the SDK's sample scenes and their unrelated UI/locomotion.
    public sealed class StareMetaInput : OvrAvatarInputManager
    {
        public OVRCameraRig Rig;
        public Transform AvatarRoot;
        public StareAvatarPresence DanceSource;
        public bool AnimatedHands=>DanceSource!=null&&DanceSource.UsesDanceHands;
        public override OvrAvatarHandTrackingPoseProviderBase HandTrackingProvider=>AnimatedHands?null:base.HandTrackingProvider;

        protected override void OnTrackingInitialized()
        {
            _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new Tracking(this));
            _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(new Controls(this));
        }

        protected override void OnDestroyCalled()
        {
            _inputTrackingProvider?.Dispose();
            _inputControlProvider?.Dispose();
            _inputTrackingProvider = null;
            _inputControlProvider = null;
            base.OnDestroyCalled();
        }

        sealed class Tracking : OvrAvatarInputTrackingDelegate
        {
            readonly StareMetaInput owner;
            public Tracking(StareMetaInput owner) { this.owner = owner; }
            public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState state)
            {
                state = default;
                if (owner == null || owner.Rig == null || owner.AvatarRoot == null) return false;
                var rig = owner.Rig;
                state.headsetActive = true;
                state.headset = Relative(rig.centerEyeAnchor);
                state.leftController = Relative(rig.leftHandAnchor);
                state.rightController = Relative(rig.rightHandAnchor);
                bool hands = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
                state.leftControllerActive = !hands && OVRInput.GetControllerOrientationTracked(OVRInput.Controller.LTouch);
                state.rightControllerActive = !hands && OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch);
                state.leftControllerVisible = state.leftControllerActive;
                state.rightControllerVisible = state.rightControllerActive;
                Pose dance;
                if(owner.AnimatedHands)
                {
                    if(owner.DanceSource.TryDanceHand(true,out dance)){state.leftController=Relative(dance,rig.centerEyeAnchor.position);state.leftControllerActive=true;state.leftControllerVisible=true;}
                    if(owner.DanceSource.TryDanceHand(false,out dance)){state.rightController=Relative(dance,rig.centerEyeAnchor.position);state.rightControllerActive=true;state.rightControllerVisible=true;}
                }
                return true;
            }

            CAPI.ovrAvatar2Transform Relative(Transform source) => new CAPI.ovrAvatar2Transform(
                owner.AvatarRoot.InverseTransformPoint(source.position),
                Quaternion.Inverse(owner.AvatarRoot.rotation) * source.rotation, Vector3.one);
            CAPI.ovrAvatar2Transform Relative(Pose pose,Vector3 head) => new CAPI.ovrAvatar2Transform(
                owner.AvatarRoot.InverseTransformPoint(head+pose.position),Quaternion.Inverse(owner.AvatarRoot.rotation)*pose.rotation,Vector3.one);
        }

        sealed class Controls : OvrAvatarInputControlDelegate
        {
            readonly StareMetaInput owner;
            public Controls(StareMetaInput value){owner=value;}
            public override bool GetInputControlState(out OvrAvatarInputControlState state)
            {
                state = default;
                if (!OvrAvatarManager.initialized) return false;
                state.type = OvrAvatarManager.Instance.ControllerType;
                if(owner.AnimatedHands)return true;
                state.leftControllerState.indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
                state.leftControllerState.handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch);
                state.rightControllerState.indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
                state.rightControllerState.handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
                return true;
            }
        }
    }

    public sealed class StareMetaFaceInput : OvrAvatarFacePoseBehavior
    {
        public Vector4 Eyes;
        public float Mouth;
        Provider provider;
        public override OvrAvatarFacePoseProviderBase FacePoseProvider => provider ?? (provider = new Provider(this));
        void OnDestroy() { provider?.Dispose(); provider = null; }

        sealed class Provider : OvrAvatarFacePoseProviderBase
        {
            readonly StareMetaFaceInput owner;
            public Provider(StareMetaFaceInput owner) { this.owner = owner; }
            protected override bool GetFacePose(OvrAvatarFacePose pose)
            {
                if (owner == null) return false;
                Array.Clear(pose.expressionWeights, 0, pose.expressionWeights.Length);
                for (int i = 0; i < pose.expressionConfidence.Length; i++) pose.expressionConfidence[i] = 1;
                var eyes = owner.Eyes;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.JawDrop] = Mathf.Clamp01(owner.Mouth);
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesClosedL] = eyes.x;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesClosedR] = eyes.y;
                float left = Mathf.Clamp01(-eyes.z / 35), right = Mathf.Clamp01(eyes.z / 35);
                float down = Mathf.Clamp01(eyes.w / 25), up = Mathf.Clamp01(-eyes.w / 25);
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookLeftL] = left;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookLeftR] = left;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookRightL] = right;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookRightR] = right;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookDownL] = down;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookDownR] = down;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookUpL] = up;
                pose.expressionWeights[(int)CAPI.ovrAvatar2FaceExpression.EyesLookUpR] = up;
                return true;
            }
        }
    }
}

#endif
