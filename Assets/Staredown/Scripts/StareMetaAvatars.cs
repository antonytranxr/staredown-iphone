#if STARE_MOBILE || (UNITY_IOS && !UNITY_EDITOR)
using System;
using System.Globalization;
using UnityEngine;

namespace Staredown
{
    // Quest keeps its native Meta service. Phones render the existing preset
    // fallback; this shipped AvatarSDK contains no iOS native avatar libraries.
    public sealed class StareMetaAvatars : MonoBehaviour
    {
        public static StareMetaAvatars Instance { get; private set; }
        public bool Ready => false;
        public string UserId => "";
        public string Status => "META AVATARS ARE DISPLAYED AS GAME CHARACTERS ON PHONES";
        public bool Connecting => false;
        internal bool Authenticated => false;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        public static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 20 &&
            ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0;
        public void Connect() { }
        public GameObject CreateAvatar(Transform parent, string userId, bool local = false) => null;
    }
}
#else
using System;
using System.Globalization;
using System.Threading.Tasks;
using Oculus.Avatar2;
using Oculus.Platform;
using UnityEngine;
using Application = UnityEngine.Application;

namespace Staredown
{
    /// <summary>Owns local Platform authentication. Only the numeric user ID belongs on the game network.</summary>
    public sealed class StareMetaAvatars : MonoBehaviour
    {
        public static StareMetaAvatars Instance { get; private set; }
        public bool Ready { get; private set; }
        public string UserId { get; private set; } = "";
        public string Status { get; private set; } = "META APP ID NOT CONFIGURED";
        public bool Connecting { get; private set; }
        internal bool Authenticated { get; private set; }
        int generation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            RefreshAvailability();
        }

        void OnDestroy()
        {
            generation++;
            if (Instance == this) Instance = null;
        }

        void RefreshAvailability()
        {
            var config = Resources.Load<StareMetaAvatarConfig>("StareMetaAvatarConfig");
            Status = config == null || !ValidId(config.MetaAppId)
                ? "META APP ID NOT CONFIGURED"
                : Application.platform != RuntimePlatform.Android
                    ? "META AVATAR: TEST ON QUEST HEADSET"
                    : "CONNECT YOUR META AVATAR";
        }

        public static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 20 && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0;

        public void Connect()
        {
            if (Connecting || Ready) return;
            var config = Resources.Load<StareMetaAvatarConfig>("StareMetaAvatarConfig");
            if (config == null || !ValidId(config.MetaAppId)) { Status = "META APP ID NOT CONFIGURED"; return; }
            // SDK 40.0.1 contains Android and Windows native plugins, but no macOS runtime.
            // This integration authenticates the Quest app ID on the headset, never a made-up editor identity.
            if (Application.platform != RuntimePlatform.Android) { Status = "META AVATAR: TEST ON QUEST HEADSET"; return; }
            if (config.ManagerPrefab == null) { Status = "META AVATAR RENDER ASSETS NOT PREPARED"; return; }
            _ = ConnectAsync(config, ++generation);
        }

        async Task ConnectAsync(StareMetaAvatarConfig config, int attempt)
        {
            Connecting = true;
            Ready = false;
            Authenticated = false;
            UserId = "";
            string failure = "META PLATFORM INITIALIZATION FAILED";
            try
            {
                Status = "CONNECTING TO META";
                Core.LogMessages = false;
                if (!Core.IsInitialized())
                {
                    var init = await Wait(Core.AsyncInitialize(config.MetaAppId));
                    if (init == null || init.IsError) throw new InvalidOperationException();
                }
                if (!Current(attempt)) return;

                failure = "APP ACCESS DENIED: CHECK META TESTER ENTITLEMENT";
                Status = "CHECKING APP ACCESS";
                var entitlement = await Wait(Entitlements.IsUserEntitledToApplication());
                if (entitlement == null || entitlement.IsError) throw new InvalidOperationException();
                if (!Current(attempt)) return;

                failure = "META SIGN-IN FAILED: CHECK USER ID / PROFILE ACCESS";
                var user = await Wait(Users.GetLoggedInUser());
                if (user == null || user.IsError || user.Data == null || user.Data.ID == 0) throw new InvalidOperationException();
                if (!Current(attempt)) return;

                failure = "META AVATAR ACCESS TOKEN UNAVAILABLE";
                var token = await Wait(Users.GetAccessToken());
                if (token == null || token.IsError || string.IsNullOrWhiteSpace(token.Data)) throw new InvalidOperationException();
                if (!Current(attempt)) return;

                failure = "META AVATAR RENDERER COULD NOT START";
                if (!OvrAvatarManager.initialized)
                {
                    if (OvrAvatarManager.Instance != null) throw new InvalidOperationException();
                    var manager = Instantiate(config.ManagerPrefab);
                    manager.name = "Staredown Meta Avatar Runtime";
                    manager.SetActive(true);
                }
                if (!OvrAvatarManager.initialized) throw new InvalidOperationException();
                OvrAvatarManager.Instance.automaticallyRequestPermissions = false;
                OvrAvatarEntitlement.SetAccessToken(token.Data);
                // Never store the token on a component, in PlayerPrefs, or in a network message.
                token = null;
                Authenticated = true;
                UserId = user.Data.ID.ToString(CultureInfo.InvariantCulture);

                failure = "META AVATAR UNAVAILABLE: CHECK AVATARS DATA USE ACCESS";
                Status = "CHECKING YOUR META AVATAR";
                var avatarTask = OvrAvatarManager.Instance.UserHasAvatarAsync(user.Data.ID);
                var result = await WithTimeout(avatarTask);
                if (!Current(attempt)) return;
                if (result == OvrAvatarManager.HasAvatarRequestResultCode.HasNoAvatar)
                {
                    Status = "CREATE YOUR META AVATAR IN HEADSET SETTINGS";
                    return;
                }
                if (result != OvrAvatarManager.HasAvatarRequestResultCode.HasAvatar) throw new InvalidOperationException();
                Ready = true;
                Status = "META AVATAR CONNECTED";
            }
            catch (TimeoutException)
            {
                if (Current(attempt)) { Ready = false; Status = "META CONNECTION TIMED OUT: TRY AGAIN"; }
            }
            catch (Exception)
            {
                // Platform error bodies can contain credentials; do not forward them to logs or the network.
                if (Current(attempt)) { Ready = false; Status = failure; }
            }
            finally { if (Current(attempt)) Connecting = false; }
        }

        bool Current(int attempt) => this != null && generation == attempt;

        static async Task<T> WithTimeout<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(20000)) != task) throw new TimeoutException();
            return await task;
        }

        static Task<Message<T>> Wait<T>(Request<T> request)
        {
            if (request == null) throw new InvalidOperationException();
            var completion = new TaskCompletionSource<Message<T>>();
            request.OnComplete(message => completion.TrySetResult(message));
            return WithTimeout(completion.Task);
        }

        static Task<Message> Wait(Request request)
        {
            if (request == null) throw new InvalidOperationException();
            var completion = new TaskCompletionSource<Message>();
            request.OnComplete(message => completion.TrySetResult(message));
            return WithTimeout(completion.Task);
        }

        public GameObject CreateAvatar(Transform parent, string userId, bool local = false)
        {
            var root = new GameObject(local ? "Your Meta Avatar" : "Player Meta Avatar");
            root.transform.SetParent(parent, false);
            if (parent != null) root.layer = parent.gameObject.layer;
            root.AddComponent<StareMetaAvatarView>().Initialize(userId, local);
            return root;
        }
    }
}

#endif
