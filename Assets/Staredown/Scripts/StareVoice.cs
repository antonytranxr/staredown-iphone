using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;
namespace Staredown
{
 public sealed class StareVoice : MonoBehaviour
 {
  public const int AudibleDistance=18,ConversationalDistance=3;
  StareGame game;string channel;bool editorMuted;bool initialized,destroyed,focused=true,permissionChecked,micAllowed;float positionAt,retryAt,permissionAt;int failures;bool transmissionDirty;Task operation;
  public float LocalMouth {get;private set;}
  public bool MicMuted {get;private set;}
  public bool VoiceEnabled {get;private set;}
  public string Status {get;private set;}="PROXIMITY CHAT / JOIN AN ONLINE ROOM";
  public string Channel=>channel;
  public bool Connected=>initialized&&channel!=null&&VivoxService.Instance.ActiveChannels.ContainsKey(channel);
  public int Participants=>Connected?VivoxService.Instance.ActiveChannels[channel].Count:0;
  public void Initialize(StareGame owner)
  {game=owner;MicMuted=PlayerPrefs.GetInt("Staredown.MicMuted",0)!=0;VoiceEnabled=PlayerPrefs.GetInt("Staredown.VoiceEnabled",1)!=0;}
  string Desired=>!destroyed&&VoiceEnabled&&game!=null&&game.LocalId>=0&&!string.IsNullOrEmpty(game.VoiceRoomId)?"staredown_"+game.VoiceRoomId:null;
  public void ToggleMic()
  {
   MicMuted=permissionChecked&&!micAllowed?false:!MicMuted;PlayerPrefs.SetInt("Staredown.MicMuted",MicMuted?1:0);PlayerPrefs.Save();
   if(!MicMuted&&!micAllowed){permissionChecked=false;retryAt=0;}
   ApplyMute();
  }
  public void ToggleVoice(){VoiceEnabled=!VoiceEnabled;PlayerPrefs.SetInt("Staredown.VoiceEnabled",VoiceEnabled?1:0);PlayerPrefs.Save();retryAt=0;ApplyMute();}
  void OnApplicationFocus(bool active){focused=active;if(active)RequestRecovery();ApplyMute();}
  void OnApplicationPause(bool paused){focused=!paused;if(!paused)RequestRecovery();ApplyMute();}
  void RequestRecovery()
  {
   if(destroyed)return;retryAt=0;transmissionDirty=true;
#if UNITY_ANDROID && !UNITY_EDITOR
   if(permissionChecked)micAllowed=UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#endif
   ApplyMute();
  }
  void InputDeviceChanged(){transmissionDirty=true;ApplyMute();}
  public static float SpeechOpening(double energy,bool speaking)=>speaking&&!double.IsNaN(energy)&&!double.IsInfinity(energy)?Mathf.Clamp01(Mathf.Max(.12f,(float)energy*2.5f)):0;
  void ApplyMute()
  {
   if(!initialized)return;
#if UNITY_EDITOR
   editorMuted=UnityEditor.EditorUtility.audioMasterMute;
#endif
   bool mute=editorMuted||MicMuted||!micAllowed||!focused||!VoiceEnabled||destroyed||Desired==null;
   if(VivoxService.Instance.IsInputDeviceMuted!=mute){if(mute)VivoxService.Instance.MuteInputDevice();else VivoxService.Instance.UnmuteInputDevice();}
   if(editorMuted||!VoiceEnabled||destroyed||Desired==null)VivoxService.Instance.MuteOutputDevice();else VivoxService.Instance.UnmuteOutputDevice();
  }
  void Update()
  {
   if(game==null)return;
#if UNITY_EDITOR
   if(editorMuted!=UnityEditor.EditorUtility.audioMasterMute){editorMuted=UnityEditor.EditorUtility.audioMasterMute;ApplyMute();}
#endif
   string desired=Desired;
#if UNITY_ANDROID && !UNITY_EDITOR
   // The user may grant permission in Quest Settings after dismissing the first prompt.
   if(desired!=null&&Time.unscaledTime>=permissionAt){permissionAt=Time.unscaledTime+1;bool allowed=UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);if(allowed!=micAllowed){micAllowed=allowed;permissionChecked=true;retryAt=0;transmissionDirty=true;ApplyMute();}}
#endif
   LocalMouth=0;
   if(Connected&&!MicMuted&&micAllowed&&focused&&VoiceEnabled&&!editorMuted)
    foreach(var participant in VivoxService.Instance.ActiveChannels[channel])if(participant.IsSelf&&!participant.IsMuted){LocalMouth=SpeechOpening(participant.AudioEnergy,participant.SpeechDetected);break;}
   if((operation==null||operation.IsCompleted)&&Time.unscaledTime>=retryAt&&(desired!=channel||desired!=null&&(!permissionChecked||!Connected||transmissionDirty)))operation=Reconcile();
   if(Connected&&Time.unscaledTime>=positionAt)
   {
    positionAt=Time.unscaledTime+.1f;var head=game.rig.centerEyeAnchor;
    VivoxService.Instance.Set3DPosition(head.position,head.position,head.forward,head.up,channel);
    Status=!VoiceEnabled?"VOICE OFF":editorMuted?"PROXIMITY CHAT / EDITOR AUDIO MUTED":!focused?"PROXIMITY CHAT / MIC PAUSED":MicMuted?"PROXIMITY CHAT / MIC MUTED":!micAllowed?"PROXIMITY CHAT / LISTENING / MIC PERMISSION NEEDED":VivoxService.Instance.IsInputDeviceMuted?"PROXIMITY CHAT / MIC PAUSED":"PROXIMITY CHAT / MIC LIVE / "+Participants+" CONNECTED";
    if(!MicMuted&&micAllowed&&focused)Status+="\nMIC ACTIVITY  "+new string('|',Mathf.RoundToInt(LocalMouth*8));
   }
   if(desired==null&&(operation==null||operation.IsCompleted))Status=VoiceEnabled?"PROXIMITY CHAT / JOIN AN ONLINE ROOM":"VOICE OFF";
   game.GetComponent<StareVisuals>().SetVoiceLabels(MicMuted||(permissionChecked&&!micAllowed),VoiceEnabled);
  }
  async Task<bool> MicrophonePermission()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
   if(UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))return true;
   var answer=new TaskCompletionSource<bool>();var callbacks=new UnityEngine.Android.PermissionCallbacks();
   callbacks.PermissionGranted+=p=>answer.TrySetResult(true);
   callbacks.PermissionDenied+=p=>answer.TrySetResult(false);

   UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);
   await Task.WhenAny(answer.Task,Task.Delay(30000));return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
   if(!Application.HasUserAuthorization(UserAuthorization.Microphone))
   {var request=Application.RequestUserAuthorization(UserAuthorization.Microphone);while(!request.isDone&&!destroyed)await Task.Delay(50);}
   return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
   return true;
#endif
  }
  async Task Reconcile()
  {
   try
   {
    if(channel!=null&&channel!=Desired)
    {ApplyMute();if(VivoxService.Instance.ActiveChannels.ContainsKey(channel))await VivoxService.Instance.LeaveChannelAsync(channel);channel=null;}
    string desired=Desired;if(desired==null)return;
    Status="CONNECTING PROXIMITY CHAT...";
    if(!permissionChecked){micAllowed=MicMuted?false:await MicrophonePermission();permissionChecked=true;}
    if(Desired!=desired)return;
    if(!initialized){await VivoxService.Instance.InitializeAsync();initialized=true;VivoxService.Instance.EffectiveInputDeviceChanged+=InputDeviceChanged;VivoxService.Instance.ConnectionRecovered+=RequestRecovery;VivoxService.Instance.ConnectionFailedToRecover+=RequestRecovery;VivoxService.Instance.LoggedIn+=InputDeviceChanged;VivoxService.Instance.MuteInputDevice();ApplyMute();}
    if(Desired!=desired)return;
    if(!VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LoginAsync(new LoginOptions{DisplayName="Contender "+(game.LocalId+1),EnableTTS=false});
    if(Desired!=desired)return;
    if(!VivoxService.Instance.ActiveChannels.ContainsKey(desired))
     await VivoxService.Instance.JoinPositionalChannelAsync(desired,ChatCapability.AudioOnly,new Channel3DProperties(AudibleDistance,ConversationalDistance,1,AudioFadeModel.LinearByDistance),new ChannelOptions{MakeActiveChannelUponJoining=true});
    if(Desired!=desired){await VivoxService.Instance.LeaveChannelAsync(desired);return;}
    // A successful join does not guarantee the SDK is still transmitting after focus/device recovery.
    await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single,desired);
    channel=desired;failures=0;retryAt=0;transmissionDirty=false;var head=game.rig.centerEyeAnchor;VivoxService.Instance.Set3DPosition(head.position,head.position,head.forward,head.up,channel);ApplyMute();if(Debug.isDebugBuild)Debug.Log("[STARE VOICE] positional room connected / "+Participants+" participant(s)");
   }
   catch(Exception e){failures++;retryAt=Time.unscaledTime+Mathf.Min(15,Mathf.Pow(2,Mathf.Min(failures,4)));Status="VOICE RECONNECTING / CHECK INTERNET / RETRYING AUTOMATICALLY";Debug.LogWarning("[STARE VOICE] "+e.GetType().Name+": "+e.Message);}
   finally {if(destroyed)await Shutdown();}
  }
  async Task Shutdown()
  {
   if(!initialized)return;
   VivoxService.Instance.EffectiveInputDeviceChanged-=InputDeviceChanged;VivoxService.Instance.ConnectionRecovered-=RequestRecovery;VivoxService.Instance.ConnectionFailedToRecover-=RequestRecovery;VivoxService.Instance.LoggedIn-=InputDeviceChanged;
   try{VivoxService.Instance.MuteInputDevice();if(channel!=null&&VivoxService.Instance.ActiveChannels.ContainsKey(channel))await VivoxService.Instance.LeaveChannelAsync(channel);channel=null;if(VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LogoutAsync();}catch(Exception){}
  }
  async void OnDestroy(){destroyed=true;if(initialized){VivoxService.Instance.EffectiveInputDeviceChanged-=InputDeviceChanged;VivoxService.Instance.ConnectionRecovered-=RequestRecovery;VivoxService.Instance.ConnectionFailedToRecover-=RequestRecovery;VivoxService.Instance.LoggedIn-=InputDeviceChanged;}ApplyMute();if(operation!=null&&!operation.IsCompleted)return;await Shutdown();}
 }
}
