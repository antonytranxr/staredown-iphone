using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Staredown
{
    public static class StareAppVersion
    {
        static string installedBuild;

        public static string Label => "Version " + Application.version + " / Build " + BuildNumber;

        public static string BuildNumber
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS
                    ? UnityEditor.PlayerSettings.iOS.buildNumber
                    : UnityEditor.PlayerSettings.Android.bundleVersionCode.ToString(CultureInfo.InvariantCulture);
#else
                if(installedBuild==null)
                {
                    installedBuild=ReadInstalledBuild();
                    Debug.Log("[Staredown] Installed version "+Application.version+" / Build "+installedBuild);
                }
                return installedBuild;
#endif
            }
        }

        static string ReadInstalledBuild()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var info = manager.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0))
                    return info.Call<long>("getLongVersionCode").ToString(CultureInfo.InvariantCulture);
#elif UNITY_IOS && !UNITY_EDITOR
                return Marshal.PtrToStringAnsi(StareInstalledBuildNumber()) ?? "Unavailable";
#endif
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Staredown] Could not read installed build number: " + error.GetType().Name);
            }
            return "Unavailable";
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern IntPtr StareInstalledBuildNumber();
#endif
    }
}
