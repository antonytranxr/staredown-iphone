using System;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using Unity.Services.Vivox;
namespace Staredown
{
 public sealed class StareVoice : MonoBehaviour
 {
  public const int AudibleDistance=18,ConversationalDistance=3;
  StareGame game;StareMobileSession mobile;string channel;bool editorMuted;
  bool initialized,destroyed,hasFocus=true,paused,permissionChecked,micAllowed,phoneAudioRoute;float positionAt,retryAt,permissionAt,healthAt,diagnosticAt;int failures;bool transmissionDirty,playbackDirty=true,rejoinRequested;Task operation;string lastDiagnostic;
  bool Focused=>hasFocus&&!paused;
  bool HasOwner=>game!=null||mobile!=null;
  int LocalId=>game!=null?game.LocalId:mobile!=null?mobile.LocalId:-1;
  string VoiceRoomId=>game!=null?game.VoiceRoomId:mobile!=null?mobile.VoiceRoomId:null;
  Transform Head=>game!=null&&game.rig!=null?game.rig.centerEyeAnchor:mobile!=null&&mobile.ViewCamera!=null?mobile.ViewCamera.transform:null;
  StareVisuals Visuals=>game!=null?game.GetComponent<StareVisuals>():mobile!=null?mobile.Visuals:null;
  string LoginName
  {
   get
   {
    var state=game!=null?game.State:mobile!=null?mobile.State:null;
    if(state!=null)foreach(var fighter in state.fighters)if(fighter.id==LocalId&&!string.IsNullOrWhiteSpace(fighter.name))return fighter.name;
    return "Contender "+(LocalId+1);
   }
  }
  public float LocalMouth {get;private set;}
  public float CaptureEnergy {get;private set;}
  public bool CaptureSpeechDetected {get;private set;}
  public float MicrophoneLevel=>Mathf.Clamp01(CaptureEnergy*4);
  public bool MicMuted {get;private set;}
  public bool MicPermissionGranted=>micAllowed;
  public bool VoiceEnabled {get;private set;}
  public string Status {get;private set;}="ROOM CHAT / JOIN AN ONLINE ROOM";
  public string Channel=>channel;
  public bool Connected=>initialized&&channel!=null&&VivoxService.Instance.ActiveChannels.ContainsKey(channel);
  public int Participants=>Connected?VivoxService.Instance.ActiveChannels[channel].Count:0;
  public bool Transmitting=>Connected&&VivoxService.Instance.TransmittingChannels.Contains(channel);
  public string InputDeviceName
  {
   get
   {
    if(!initialized||VivoxService.Instance.AvailableInputDevices.Count==0)return "unavailable";
    // During hot-plug the SDK can return a wrapper before its native device is
    // populated. DeviceName dereferences that device inside the SDK.
    try{var device=VivoxService.Instance.EffectiveInputDevice;return device==null||string.IsNullOrEmpty(device.DeviceName)?"unavailable":device.DeviceName;}
    catch(NullReferenceException){return "unavailable";}
   }
  }
  public string MicrophoneSummary
  {
   get
   {
    if(!VoiceEnabled)return "VOICE OFF";
    if(!Connected)return Status;
    if(editorMuted)return "MIC PAUSED / EDITOR AUDIO MUTED";
    if(MicMuted)return "MIC MUTED";
    if(!micAllowed)return "MIC PERMISSION NEEDED";
    if(!Focused)return "MIC PAUSED / APP IN BACKGROUND";
    var sdk=VivoxService.Instance;
    if(sdk.IsInputDeviceMuted)return "MIC PAUSED / REPAIRING";
    if(!Transmitting)return "MIC CONNECTING / REPAIRING";
    if(InputDeviceName=="unavailable")return "MIC / NO INPUT DEVICE";
    if(sdk.InputDeviceVolume<=-40)return "MIC LOW GAIN / REPAIRING";
    return CaptureSpeechDetected?"MIC ACTIVE":CaptureEnergy>.00001f?"MIC SIGNAL / SPEAK TO TEST":"MIC READY / SPEAK TO TEST";
   }
  }
  public void Initialize(StareGame owner){StopPhoneAudioRoute();game=owner;mobile=null;LoadPreferences();}
  // Room-wide voice removes distance attenuation while testing receive recovery.
  public void Initialize(StareMobileSession owner){mobile=owner;game=null;LoadPreferences();if(!phoneAudioRoute&&owner!=null){phoneAudioRoute=true;StarePhoneAudioRoute.Begin();}}
  void RefreshPhoneAudioRoute(){if(phoneAudioRoute)StarePhoneAudioRoute.Refresh();}
  void StopPhoneAudioRoute(){if(phoneAudioRoute){phoneAudioRoute=false;StarePhoneAudioRoute.End();}}
  void LoadPreferences(){MicMuted=PlayerPrefs.GetInt("Staredown.MicMuted",0)!=0;VoiceEnabled=PlayerPrefs.GetInt("Staredown.VoiceEnabled",1)!=0;}
  string Desired=>!destroyed&&VoiceEnabled&&HasOwner&&LocalId>=0&&!string.IsNullOrEmpty(VoiceRoomId)?"staredown_room_v2_"+VoiceRoomId:null;
  public void EnableMicrophone()
  {
   MicMuted=false;PlayerPrefs.SetInt("Staredown.MicMuted",0);PlayerPrefs.Save();
   if(!micAllowed){permissionChecked=false;retryAt=0;}ApplyMute();
  }
  public void ToggleMic()
  {
   MicMuted=permissionChecked&&!micAllowed?false:!MicMuted;PlayerPrefs.SetInt("Staredown.MicMuted",MicMuted?1:0);PlayerPrefs.Save();
   if(!MicMuted&&!micAllowed){permissionChecked=false;retryAt=0;}
   ApplyMute();
  }
  public void ToggleVoice(){VoiceEnabled=!VoiceEnabled;PlayerPrefs.SetInt("Staredown.VoiceEnabled",VoiceEnabled?1:0);PlayerPrefs.Save();retryAt=0;ApplyMute();}
  void OnApplicationFocus(bool active){hasFocus=active;if(active)RequestRecovery();ApplyMute();}
  void OnApplicationPause(bool isPaused){paused=isPaused;if(!isPaused)RequestRecovery();ApplyMute();}
  void RequestRecovery()
  {
   if(destroyed)return;retryAt=0;transmissionDirty=true;playbackDirty=true;RefreshPhoneAudioRoute();
   if(permissionChecked)micAllowed=HasMicrophonePermission();
   ApplyMute();
  }
  void InputDeviceChanged(){transmissionDirty=true;ApplyMute();RefreshPhoneAudioRoute();}
  void OutputDeviceChanged(){playbackDirty=true;retryAt=0;RefreshPhoneAudioRoute();}
  public void ReconnectVoice(){if(destroyed)return;rejoinRequested=true;RequestRecovery();Status="RECONNECTING ROOM CHAT...";}
  public string ReceiveSummary
  {
   get
   {
    if(!Connected)return Status;
    int speaking=0,remote=0;var names=new StringBuilder();
    foreach(var p in VivoxService.Instance.ActiveChannels[channel])if(!p.IsSelf)
    {remote++;if(p.SpeechDetected&&!p.IsMuted){speaking++;if(names.Length<70){if(names.Length>0)names.Append(", ");names.Append(p.DisplayName);}}}
    return "VOICE RECEIVE / "+remote+" PEERS / "+speaking+" SPEAKING"+(names.Length>0?"\n"+names:"");
   }
  }
  public string DiagnosticsSummary
  {
   get
   {
    if(!Connected)return Status;
    var sdk=VivoxService.Instance;var text=new StringBuilder("Room-wide chat / "+Participants+" connected\n"+MicrophoneSummary+"\nTransmit: "+(Transmitting?"ON":"OFF")+" / permission: "+(micAllowed?"YES":"NO")+" / focus: "+(Focused?"YES":"NO")+"\nInput: "+InputDeviceName+" / gain "+sdk.InputDeviceVolume+"\nMic signal: "+CaptureEnergy.ToString("F4")+" / speech: "+(CaptureSpeechDetected?"YES":"NO")+"\nOutput: "+(sdk.IsOutputDeviceMuted?"MUTED":"ON")+" / level "+sdk.OutputDeviceVolume+"\nQuiet is normal when you are not speaking.\n");
    int count=0;foreach(var p in sdk.ActiveChannels[channel])
    {if(count++>=8){text.Append("More players connected…");break;}text.Append(p.DisplayName).Append(p.IsSelf?" (you)":"").Append(p.IsMuted?" / MUTED":p.SpeechDetected?" / SPEAKING":" / quiet").Append('\n');}
    return text.ToString();
   }
  }
  void ChannelLeft(string left){if(left==channel)RequestRecovery();}
  static bool HasMicrophonePermission()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
   return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
   return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
   return true;
#endif
  }
  public static float SpeechOpening(double energy,bool speaking)=>speaking&&!double.IsNaN(energy)&&!double.IsInfinity(energy)?Mathf.Clamp01(Mathf.Max(.12f,(float)energy*2.5f)):0;
  void ApplyMute()
  {
   if(!initialized)return;
#if UNITY_EDITOR
   editorMuted=UnityEditor.EditorUtility.audioMasterMute;
#endif
   bool mute=editorMuted||MicMuted||!micAllowed||!Focused||!VoiceEnabled||destroyed||Desired==null;
   if(VivoxService.Instance.IsInputDeviceMuted!=mute){if(mute)VivoxService.Instance.MuteInputDevice();else VivoxService.Instance.UnmuteInputDevice();}
   if(editorMuted||!VoiceEnabled||destroyed||Desired==null)VivoxService.Instance.MuteOutputDevice();else VivoxService.Instance.UnmuteOutputDevice();
  }
  void Update()
  {
   if(!HasOwner)return;
#if UNITY_EDITOR
   if(editorMuted!=UnityEditor.EditorUtility.audioMasterMute){editorMuted=UnityEditor.EditorUtility.audioMasterMute;ApplyMute();}
#endif
   string desired=Desired;
   // Permissions may change in phone/Quest Settings after a denied prompt. Never
   // manufacture a grant on iOS, and never repeatedly prompt in the background.
   if(desired!=null&&permissionChecked&&Focused&&Time.unscaledTime>=permissionAt)
   {permissionAt=Time.unscaledTime+1;bool allowed=HasMicrophonePermission();if(allowed!=micAllowed){micAllowed=allowed;retryAt=0;transmissionDirty=true;ApplyMute();}}
   if(Connected&&Focused&&Time.unscaledTime>=healthAt)
   {
    healthAt=Time.unscaledTime+5;var sdk=VivoxService.Instance;
    if(!sdk.IsLoggedIn||!sdk.TransmittingChannels.Contains(channel))transmissionDirty=true;
    // A capture interruption can leave the SDK muted without a device-change
    // event. Respect explicit mute/focus intent while repairing stale SDK state.
    if(!MicMuted&&micAllowed&&VoiceEnabled&&!editorMuted&&(sdk.IsInputDeviceMuted||sdk.InputDeviceVolume<=-40))
    {transmissionDirty=true;ApplyMute();}
    if(sdk.IsOutputDeviceMuted||sdk.OutputDeviceVolume<=-40)playbackDirty=true;
   }
   LocalMouth=CaptureEnergy=0;CaptureSpeechDetected=false;
   if(Connected&&!MicMuted&&micAllowed&&Focused&&VoiceEnabled&&!editorMuted)
    foreach(var participant in VivoxService.Instance.ActiveChannels[channel])if(participant.IsSelf&&!participant.IsMuted)
    {
     double energy=participant.AudioEnergy;bool finite=!double.IsNaN(energy)&&!double.IsInfinity(energy);CaptureEnergy=finite?Mathf.Clamp01((float)energy):0;
     CaptureSpeechDetected=finite&&participant.SpeechDetected;LocalMouth=SpeechOpening(energy,participant.SpeechDetected);break;
    }
   if((operation==null||operation.IsCompleted)&&Time.unscaledTime>=retryAt&&(desired==null||Focused)&&(desired!=channel||desired!=null&&(!permissionChecked||!Connected||transmissionDirty||playbackDirty||rejoinRequested)))operation=Reconcile();
   if(Connected&&Time.unscaledTime>=positionAt)
   {
    positionAt=Time.unscaledTime+.25f;
    Status=!micAllowed&&!MicMuted?(mobile!=null?"LISTENING ONLY / ENABLE MICROPHONE IN PHONE SETTINGS":"ROOM CHAT / LISTENING / MIC PERMISSION NEEDED"):"ROOM CHAT / "+MicrophoneSummary+" / "+Participants+" CONNECTED";
    if(!MicMuted&&micAllowed&&Focused)Status+="\nMIC ACTIVITY  "+new string('|',Mathf.RoundToInt(MicrophoneLevel*8));
   }
   if(desired==null&&(operation==null||operation.IsCompleted))Status=VoiceEnabled?"ROOM CHAT / JOIN AN ONLINE ROOM":"VOICE OFF";
   if(Visuals!=null)Visuals.SetVoiceLabels(MicMuted||(permissionChecked&&!micAllowed),VoiceEnabled);
   if(Debug.isDebugBuild&&Connected&&Time.unscaledTime>=diagnosticAt)
   {
    diagnosticAt=Time.unscaledTime+2;string current=MicrophoneSummary;
    if(current!=lastDiagnostic)
    {lastDiagnostic=current;Debug.Log("[STARE VOICE MIC] "+current+" / input="+InputDeviceName+" / gain="+VivoxService.Instance.InputDeviceVolume+" / energy="+CaptureEnergy.ToString("F4")+" / transmit="+Transmitting+" / permission="+micAllowed+" / focus="+Focused);}
   }
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
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
   if(!Application.HasUserAuthorization(UserAuthorization.Microphone))
   {var request=Application.RequestUserAuthorization(UserAuthorization.Microphone);while(!request.isDone&&!destroyed)await Task.Delay(50);}
   return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
   return true;
#endif
  }
  async Task Reconcile()
  {
   // Consume only the work known at entry. Focus/device events during an await
   // must survive and trigger a second reconciliation rather than being lost.
   bool repairPlayback=playbackDirty,forceRejoin=rejoinRequested;
   playbackDirty=false;transmissionDirty=false;rejoinRequested=false;
   try
   {
    if(channel!=null&&(channel!=Desired||forceRejoin))
    {ApplyMute();if(VivoxService.Instance.ActiveChannels.ContainsKey(channel))await VivoxService.Instance.LeaveChannelAsync(channel);channel=null;}
    string desired=Desired;if(desired==null)return;
    Status="CONNECTING ROOM CHAT...";
    if(!permissionChecked){micAllowed=MicMuted?false:await MicrophonePermission();permissionChecked=true;}
    if(Desired!=desired)return;
    if(!initialized)
    {
     // Vivox owns PlayAndRecord and voice-processing echo cancellation on iOS.
     // The ARKit camera must not open another microphone or override this category.
     await VivoxService.Instance.InitializeAsync(new VivoxConfigurationOptions{SkipPrepareForVivox=false,IosVoiceProcessingMode=IosVoiceProcessingIOModes.Always});
     initialized=true;VivoxService.Instance.EffectiveOutputDeviceChanged+=OutputDeviceChanged;VivoxService.Instance.AvailableOutputDevicesChanged+=OutputDeviceChanged;VivoxService.Instance.EffectiveInputDeviceChanged+=InputDeviceChanged;VivoxService.Instance.ConnectionRecovered+=RequestRecovery;VivoxService.Instance.ConnectionFailedToRecover+=RequestRecovery;VivoxService.Instance.LoggedIn+=InputDeviceChanged;VivoxService.Instance.LoggedOut+=RequestRecovery;VivoxService.Instance.ChannelLeft+=ChannelLeft;VivoxService.Instance.MuteInputDevice();ApplyMute();
    }
    if(Desired!=desired)return;
    if(!VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LoginAsync(new LoginOptions{DisplayName=LoginName,EnableTTS=false,ParticipantUpdateFrequency=ParticipantPropertyUpdateFrequency.TenPerSecond});
    if(Desired!=desired)return;
    if(!VivoxService.Instance.ActiveChannels.ContainsKey(desired))
     await VivoxService.Instance.JoinGroupChannelAsync(desired,ChatCapability.AudioOnly,new ChannelOptions{MakeActiveChannelUponJoining=true});
    if(Desired!=desired){await VivoxService.Instance.LeaveChannelAsync(desired);return;}
    // A successful join does not guarantee the SDK is still transmitting after focus/device recovery.
    await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single,desired);
    channel=desired;
    VivoxService.Instance.SetInputDeviceVolume(0);
    // SDK voice gain and mute state are separate from Unity's music/SFX mixer.
    // Restore neutral gain after route/focus/join recovery; leave OS volume alone.
    if(repairPlayback){VivoxService.Instance.SetOutputDeviceVolume(0);await VivoxService.Instance.SetChannelVolumeAsync(desired,0);}
    failures=0;retryAt=0;ApplyMute();RefreshPhoneAudioRoute();if(Debug.isDebugBuild)Debug.Log("[STARE VOICE] room-wide channel connected / "+(mobile!=null?"phone":"headset")+" / "+MicrophoneSummary+" / input="+InputDeviceName+" / gain="+VivoxService.Instance.InputDeviceVolume+" / transmit="+Transmitting+" / "+Participants+" participant(s)");
   }
   catch(Exception e){transmissionDirty=true;playbackDirty=true;rejoinRequested|=forceRejoin;failures++;retryAt=Time.unscaledTime+Mathf.Min(15,Mathf.Pow(2,Mathf.Min(failures,4)));Status="VOICE RECONNECTING / CHECK INTERNET / RETRYING AUTOMATICALLY";Debug.LogWarning("[STARE VOICE] "+e.GetType().Name+": "+e.Message);}
   finally {if(destroyed)await Shutdown();}
  }
  async Task Shutdown()
  {
   if(!initialized)return;
   VivoxService.Instance.EffectiveOutputDeviceChanged-=OutputDeviceChanged;VivoxService.Instance.AvailableOutputDevicesChanged-=OutputDeviceChanged;VivoxService.Instance.EffectiveInputDeviceChanged-=InputDeviceChanged;VivoxService.Instance.ConnectionRecovered-=RequestRecovery;VivoxService.Instance.ConnectionFailedToRecover-=RequestRecovery;VivoxService.Instance.LoggedIn-=InputDeviceChanged;VivoxService.Instance.LoggedOut-=RequestRecovery;VivoxService.Instance.ChannelLeft-=ChannelLeft;
   try{VivoxService.Instance.MuteInputDevice();if(channel!=null&&VivoxService.Instance.ActiveChannels.ContainsKey(channel))await VivoxService.Instance.LeaveChannelAsync(channel);channel=null;if(VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LogoutAsync();}catch(Exception){}
  }
  async void OnDestroy(){destroyed=true;StopPhoneAudioRoute();if(initialized){VivoxService.Instance.EffectiveOutputDeviceChanged-=OutputDeviceChanged;VivoxService.Instance.AvailableOutputDevicesChanged-=OutputDeviceChanged;VivoxService.Instance.EffectiveInputDeviceChanged-=InputDeviceChanged;VivoxService.Instance.ConnectionRecovered-=RequestRecovery;VivoxService.Instance.ConnectionFailedToRecover-=RequestRecovery;VivoxService.Instance.LoggedIn-=InputDeviceChanged;VivoxService.Instance.LoggedOut-=RequestRecovery;VivoxService.Instance.ChannelLeft-=ChannelLeft;}ApplyMute();if(operation!=null&&!operation.IsCompleted)return;await Shutdown();}
 }
}
