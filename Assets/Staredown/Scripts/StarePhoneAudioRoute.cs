using System.Runtime.InteropServices;

namespace Staredown
{
 // Receiver-only fallback. Native code never takes ownership of Vivox's audio
 // session or changes a headphone / Bluetooth / AirPlay route.
 internal static class StarePhoneAudioRoute
 {
#if UNITY_IOS && !UNITY_EDITOR
  [DllImport("__Internal")] static extern void StareMobileIOSAudioRouteBegin();
  [DllImport("__Internal")] static extern void StareMobileIOSAudioRouteRefresh();
  [DllImport("__Internal")] static extern void StareMobileIOSAudioRouteEnd();
#endif
  public static void Begin()
  {
#if UNITY_IOS && !UNITY_EDITOR
   StareMobileIOSAudioRouteBegin();
#endif
  }
  public static void Refresh()
  {
#if UNITY_IOS && !UNITY_EDITOR
   StareMobileIOSAudioRouteRefresh();
#endif
  }
  public static void End()
  {
#if UNITY_IOS && !UNITY_EDITOR
   StareMobileIOSAudioRouteEnd();
#endif
  }
 }
}
