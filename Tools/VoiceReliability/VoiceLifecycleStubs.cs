using System;using System.Collections.Generic;using System.Threading.Tasks;using System.Reflection;using Staredown;using UnityEngine;using Unity.Services.Vivox;
namespace UnityEngine {
 public class MonoBehaviour { public T GetComponent<T>() where T:new(){return new T();} }
 public class Transform {public Vector3 position=new Vector3();public Vector3 forward=new Vector3();public Vector3 up=new Vector3();}
 public struct Vector3{}
 public class Camera {public Transform transform=new Transform();}
 public static class Time {public static float unscaledTime;}
 public static class Application {public static bool granted;public static int requests;public static bool HasUserAuthorization(UserAuthorization a){return granted;}public static AsyncOperation RequestUserAuthorization(UserAuthorization a){requests++;return new AsyncOperation();}}
 public enum UserAuthorization {Microphone}public class AsyncOperation {public bool isDone=true;}
 public static class Mathf {public static float Clamp01(float f){return Math.Max(0,Math.Min(f,1));}public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static int Min(int a,int b){return Math.Min(a,b);}public static float Pow(float a,int b){return (float)Math.Pow(a,b);}public static int RoundToInt(float f){return (int)Math.Round(f);}}
 public static class PlayerPrefs {public static Dictionary<string,int> values=new Dictionary<string,int>();public static int GetInt(string k,int d){return values.ContainsKey(k)?values[k]:d;}public static void SetInt(string k,int v){values[k]=v;}public static void Save(){}}
 public static class Debug {public static bool isDebugBuild=true;public static void Log(object a){}public static void LogWarning(object a){Console.WriteLine(a);}}
}
namespace Staredown {
 public static class StarePhoneAudioRoute {public static int begins,refreshes,ends;public static void Begin(){begins++;}public static void Refresh(){refreshes++;}public static void End(){ends++;}}
 public class Fighter {public int id;public string name;}
 public class ArenaState {public List<Fighter> fighters=new List<Fighter>();}
 public class Rig {public Transform centerEyeAnchor=new Transform();}
 public class StareGame:MonoBehaviour {public int LocalId=-1;public string VoiceRoomId;public Rig rig=new Rig();public ArenaState State=new ArenaState();}
 public class StareMobileSession {public int LocalId=-1;public string VoiceRoomId;public Camera ViewCamera=new Camera();public StareVisuals Visuals=new StareVisuals();public ArenaState State=new ArenaState();}
 public class StareVisuals {public void SetVoiceLabels(bool mic,bool voice){}}
}
namespace Unity.Services.Vivox {
 public enum TransmissionMode{Single}public enum ChatCapability{AudioOnly}public enum AudioFadeModel{LinearByDistance}public enum ParticipantPropertyUpdateFrequency{StateChange,TenPerSecond}
 public class VivoxConfigurationOptions {public bool SkipPrepareForVivox;public IosVoiceProcessingIOModes IosVoiceProcessingMode;}
 public class LoginOptions {public string DisplayName;public bool EnableTTS;public ParticipantPropertyUpdateFrequency ParticipantUpdateFrequency;}public class ChannelOptions {public bool MakeActiveChannelUponJoining;}
 public class VivoxInputDevice {public bool missingNativeDevice;public string DeviceName {get{if(missingNativeDevice)throw new NullReferenceException();return "Quest/phone microphone";}}}
 public class Channel3DProperties {public Channel3DProperties(int a,int b,int c,AudioFadeModel d){}}
 public class VivoxParticipant {public string DisplayName="Test peer";public bool IsSelf,IsMuted,SpeechDetected;public double AudioEnergy;}
 public static class VivoxService {public static FakeService Instance=new FakeService();}
 public class FakeService {
  public int OutputDeviceVolume,InputDeviceVolume,channelVolume,outputRepairs,inputRepairs;public bool IsLoggedIn,IsInputDeviceMuted,IsOutputDeviceMuted;public string LoginDisplayName;public int initialized,joined,leaves,positions,transmissions;public VivoxConfigurationOptions config;public ParticipantPropertyUpdateFrequency frequency;
  public VivoxInputDevice EffectiveInputDevice=new VivoxInputDevice();public List<VivoxInputDevice> AvailableInputDevices=new List<VivoxInputDevice>{new VivoxInputDevice()};public TaskCompletionSource<bool> transmissionGate;
  public Dictionary<string,List<VivoxParticipant>> ActiveChannels=new Dictionary<string,List<VivoxParticipant>>();public List<string> TransmittingChannels=new List<string>();
  public event Action EffectiveOutputDeviceChanged,AvailableOutputDevicesChanged,EffectiveInputDeviceChanged,ConnectionRecovered,ConnectionFailedToRecover,LoggedIn,LoggedOut;public event Action<string> ChannelLeft;
  public void MuteInputDevice(){IsInputDeviceMuted=true;}public void UnmuteInputDevice(){IsInputDeviceMuted=false;}public void MuteOutputDevice(){IsOutputDeviceMuted=true;}public void UnmuteOutputDevice(){IsOutputDeviceMuted=false;}
  public Task InitializeAsync(VivoxConfigurationOptions c){initialized++;config=c;return Task.CompletedTask;}
  public Task LoginAsync(LoginOptions l){IsLoggedIn=true;LoginDisplayName=l.DisplayName;frequency=l.ParticipantUpdateFrequency;LoggedIn?.Invoke();return Task.CompletedTask;}
  public void SetInputDeviceVolume(int volume){InputDeviceVolume=volume;inputRepairs++;}
  public void SetOutputDeviceVolume(int volume){OutputDeviceVolume=volume;outputRepairs++;}
  public Task SetChannelVolumeAsync(string c,int volume){channelVolume=volume;return Task.CompletedTask;}
  public void ChangeOutput(){EffectiveOutputDeviceChanged?.Invoke();}
  public void ChangeOutputs(){AvailableOutputDevicesChanged?.Invoke();}
  public Task JoinGroupChannelAsync(string c,ChatCapability x,ChannelOptions z){joined++;ActiveChannels[c]=new List<VivoxParticipant>{new VivoxParticipant{IsSelf=true}};return Task.CompletedTask;}
  public Task JoinPositionalChannelAsync(string c,ChatCapability x,Channel3DProperties y,ChannelOptions z){joined++;ActiveChannels[c]=new List<VivoxParticipant>{new VivoxParticipant{IsSelf=true}};return Task.CompletedTask;}
  public Task LeaveChannelAsync(string c){leaves++;ActiveChannels.Remove(c);TransmittingChannels.Remove(c);ChannelLeft?.Invoke(c);return Task.CompletedTask;}
  public Task SetChannelTransmissionModeAsync(TransmissionMode m,string c){transmissions++;TransmittingChannels.Clear();TransmittingChannels.Add(c);return transmissionGate!=null?transmissionGate.Task:Task.CompletedTask;}
  public void Set3DPosition(Vector3 a,Vector3 b,Vector3 c,Vector3 d,string e){positions++;}
  public Task LogoutAsync(){IsLoggedIn=false;LoggedOut?.Invoke();return Task.CompletedTask;}
  public void LoseTransmission(){TransmittingChannels.Clear();}
  public void Recover(){ConnectionRecovered?.Invoke();}public void ChangeDevice(){EffectiveInputDeviceChanged?.Invoke();}
 }
}
class Tests {
#if UNITY_ANDROID
 const string Platform="Android permission branch";
#else
 const string Platform="iOS permission branch";
#endif
 static int checks;static void Assert(bool value,string title){checks++;if(!value)throw new Exception(title);}
 static void Call(object o,string name,params object[] args){typeof(StareVoice).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
 static async Task Tick(StareVoice v){Call(v,"Update");var task=(Task)typeof(StareVoice).GetField("operation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(v);if(task!=null)await task;Time.unscaledTime+=1.1f;}
 static async Task Run(){
  var phone=new StareMobileSession();var voice=new StareVoice();voice.Initialize(phone);Assert(StarePhoneAudioRoute.begins==1,"Phone installs native route watcher before room/audio startup");await Tick(voice);Assert(Application.requests==0,"No microphone prompt outside a room");
  phone.LocalId=0;phone.VoiceRoomId="same-relay-lobby";phone.State.fighters.Add(new Fighter{id=0,name="Test phone"});await Tick(voice);var sdk=VivoxService.Instance;
  Assert(Application.requests==1,"iOS microphone authorization requested");Assert(voice.Connected,"Denied mic still joins listening-only");Assert(sdk.IsInputDeviceMuted&&!sdk.IsOutputDeviceMuted,"Denied mic remains muted while output stays audible");Assert(voice.Channel=="staredown_room_v2_same-relay-lobby","Phone uses shared headset room identity");Assert(sdk.LoginDisplayName=="Test phone","Vivox displays entered name");Assert(sdk.config!=null&&!sdk.config.SkipPrepareForVivox,"Vivox owns iOS PlayAndRecord");Assert(sdk.config.IosVoiceProcessingMode==IosVoiceProcessingIOModes.Always,"Echo cancellation remains on");Assert(sdk.positions==0,"Room voice does not attenuate by world position");Assert(StarePhoneAudioRoute.refreshes>0&&StarePhoneAudioRoute.begins==1,"Join refreshes route without duplicating watcher");
  await Tick(voice);Assert(voice.Status.Contains("SETTINGS"),"Permission failure is visible");await Tick(voice);Assert(Application.requests==1,"Denied microphone not repeatedly requested");
  Application.granted=true;Call(voice,"OnApplicationPause",true);Call(voice,"OnApplicationFocus",true);Assert(sdk.IsInputDeviceMuted,"Focus gain does not unmute while paused");int routeRefreshes=StarePhoneAudioRoute.refreshes;Call(voice,"OnApplicationPause",false);await Tick(voice);Assert(StarePhoneAudioRoute.refreshes>routeRefreshes,"Resume refreshes iPhone speaker route");Assert(!sdk.IsInputDeviceMuted,"Settings grant and resume restores capture");Assert(Application.requests==1,"Settings grant does not reprompt");
  Assert(sdk.frequency==ParticipantPropertyUpdateFrequency.TenPerSecond,"Login requests live microphone energy updates");
  var self=sdk.ActiveChannels[voice.Channel][0];self.SpeechDetected=true;self.AudioEnergy=.2;await Tick(voice);Assert(voice.LocalMouth>.4f,"Actual self speech drives meter");voice.ToggleMic();await Tick(voice);Assert(sdk.IsInputDeviceMuted&&voice.LocalMouth==0,"Manual mute stops capture/meter");voice.ToggleMic();await Tick(voice);Assert(!sdk.IsInputDeviceMuted,"Manual unmute restores capture");
  Assert(voice.CaptureEnergy>.19f&&voice.MicrophoneLevel>.7f&&voice.Transmitting&&voice.MicrophoneSummary=="MIC ACTIVE","Capture energy and actual transmit channel are exposed");
  self.SpeechDetected=false;self.AudioEnergy=.01;await Tick(voice);Assert(voice.LocalMouth==0&&voice.CaptureEnergy>.009f&&voice.MicrophoneSummary=="MIC SIGNAL / SPEAK TO TEST","Raw capture signal is visible independently of VAD speech threshold");
  self.AudioEnergy=0;await Tick(voice);int silentJoins=sdk.joined;Time.unscaledTime+=6;await Tick(voice);Assert(voice.MicrophoneSummary=="MIC READY / SPEAK TO TEST"&&sdk.joined==silentJoins,"Quiet microphone does not trigger forced rejoin or claim a hardware fault");
  sdk.IsInputDeviceMuted=true;Time.unscaledTime+=6;await Tick(voice);Assert(!sdk.IsInputDeviceMuted,"Unexpected SDK capture mute automatically repaired when intent permits");
  sdk.InputDeviceVolume=-50;Time.unscaledTime+=6;await Tick(voice);Assert(sdk.InputDeviceVolume==0,"Unexpected near-silent SDK input gain automatically restored");
  voice.ToggleMic();sdk.InputDeviceVolume=-50;int inputRepairs=sdk.inputRepairs;Time.unscaledTime+=6;await Tick(voice);Assert(sdk.IsInputDeviceMuted&&sdk.inputRepairs==inputRepairs&&voice.MicrophoneSummary=="MIC MUTED","Health check respects explicit user mute");
  voice.ToggleMic();Time.unscaledTime+=6;await Tick(voice);Assert(!sdk.IsInputDeviceMuted&&sdk.InputDeviceVolume==0,"Capture recovers after user explicitly unmutes");
  Call(voice,"OnApplicationPause",true);sdk.InputDeviceVolume=-50;inputRepairs=sdk.inputRepairs;Time.unscaledTime+=6;await Tick(voice);Assert(sdk.IsInputDeviceMuted&&sdk.inputRepairs==inputRepairs&&voice.MicrophoneSummary.Contains("BACKGROUND"),"Background app stays muted without capture repair");
  Call(voice,"OnApplicationPause",false);await Tick(voice);Assert(!sdk.IsInputDeviceMuted&&sdk.InputDeviceVolume==0,"Resume restores neutral SDK capture gain");
  self.AudioEnergy=double.NaN;self.SpeechDetected=true;await Tick(voice);Assert(voice.CaptureEnergy==0&&voice.MicrophoneLevel==0&&!voice.CaptureSpeechDetected,"Invalid capture energy is safely suppressed");
  sdk.EffectiveInputDevice.missingNativeDevice=true;Assert(voice.InputDeviceName=="unavailable"&&voice.MicrophoneSummary.Contains("NO INPUT DEVICE")&&voice.DiagnosticsSummary.Contains("Input: unavailable"),"Hot-plug SDK device wrapper cannot crash microphone diagnostics");sdk.EffectiveInputDevice.missingNativeDevice=false;
  var available=sdk.AvailableInputDevices;sdk.AvailableInputDevices=new List<VivoxInputDevice>();Assert(voice.MicrophoneSummary.Contains("NO INPUT DEVICE"),"Missing hardware is distinct from a quiet healthy input");sdk.AvailableInputDevices=available;
  int transmissions=sdk.transmissions;sdk.LoseTransmission();Time.unscaledTime+=6;await Tick(voice);Assert(sdk.transmissions>transmissions&&sdk.TransmittingChannels.Contains(voice.Channel),"Lost transmission is recovered");
  transmissions=sdk.transmissions;sdk.ChangeDevice();await Tick(voice);Assert(sdk.transmissions>transmissions,"Device route change reapplies transmission");
  // Output state is independent of capture/transmission: reproduce receive-only failure.
  sdk.IsOutputDeviceMuted=true;sdk.OutputDeviceVolume=-50;Time.unscaledTime+=6;await Tick(voice);Assert(!sdk.IsOutputDeviceMuted&&sdk.OutputDeviceVolume==0,"Health recovery repairs muted/silent voice output without changing microphone");
  int repairs=sdk.outputRepairs;sdk.ChangeOutput();await Tick(voice);Assert(sdk.outputRepairs>repairs,"Effective output change reapplies voice gain");
  repairs=sdk.outputRepairs;sdk.ChangeOutputs();await Tick(voice);Assert(sdk.outputRepairs>repairs,"Available output change repairs playback");
  sdk.transmissionGate=new TaskCompletionSource<bool>();sdk.ChangeDevice();Call(voice,"Update");var inflight=(Task)typeof(StareVoice).GetField("operation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(voice);
  Assert(inflight!=null&&!inflight.IsCompleted,"Recovery fixture pauses inside asynchronous transmission setup");sdk.ChangeDevice();sdk.ChangeOutput();var gate=sdk.transmissionGate;sdk.transmissionGate=null;gate.SetResult(true);await inflight;transmissions=sdk.transmissions;repairs=sdk.outputRepairs;Time.unscaledTime+=1.1f;await Tick(voice);
  Assert(sdk.transmissions>transmissions&&sdk.outputRepairs>repairs,"Device/output changes during await survive and get another recovery pass");
  sdk.transmissionGate=new TaskCompletionSource<bool>();sdk.ChangeDevice();Call(voice,"Update");inflight=(Task)typeof(StareVoice).GetField("operation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(voice);voice.ReconnectVoice();gate=sdk.transmissionGate;sdk.transmissionGate=null;gate.SetResult(true);await inflight;int raceJoins=sdk.joined;Time.unscaledTime+=1.1f;await Tick(voice);
  Assert(sdk.joined==raceJoins+1,"Explicit reconnect requested during await is preserved");
  sdk.ActiveChannels[voice.Channel].Add(new VivoxParticipant{DisplayName="Quest player",SpeechDetected=true});sdk.ActiveChannels[voice.Channel].Add(new VivoxParticipant{DisplayName="Phone player",SpeechDetected=false});sdk.ActiveChannels[voice.Channel].Add(new VivoxParticipant{DisplayName="Muted player",SpeechDetected=true,IsMuted=true});
  Assert(voice.Participants==4&&voice.ReceiveSummary.Contains("3 PEERS / 1 SPEAKING")&&voice.ReceiveSummary.Contains("Quest player")&&!voice.ReceiveSummary.Contains("Muted player"),"Four participant receive roster reports remote activity and excludes muted speech");
  Assert(voice.DiagnosticsSummary.Contains("Phone player / quiet")&&voice.DiagnosticsSummary.Contains("Muted player / MUTED"),"Playback diagnostics distinguish quiet and muted peers");
  int joins=sdk.joined;voice.ReconnectVoice();await Tick(voice);Assert(sdk.joined==joins+1&&voice.Connected&&!sdk.IsOutputDeviceMuted,"User repair leaves and rejoins room and restores playback");
  int oldLeaves=sdk.leaves;phone.VoiceRoomId="different-lobby";await Tick(voice);Assert(sdk.leaves==oldLeaves+1&&!sdk.ActiveChannels.ContainsKey("staredown_room_v2_same-relay-lobby")&&voice.Channel=="staredown_room_v2_different-lobby","Changing room leaves old audio before joining next");
  phone.LocalId=-1;phone.VoiceRoomId=null;await Tick(voice);Assert(!voice.Connected&&sdk.ActiveChannels.Count==0&&sdk.IsInputDeviceMuted,"Leaving room closes capture/channel");
  Call(voice,"OnDestroy");Assert(StarePhoneAudioRoute.ends==1,"Phone destruction tears down route observer");Assert(!sdk.IsLoggedIn,"Destroyed phone fixture logs out before next owner");
  var vr=new StareGame{LocalId=1,VoiceRoomId="quest-room"};var vrVoice=new StareVoice();vrVoice.Initialize(vr);await Tick(vrVoice);Assert(StarePhoneAudioRoute.begins==1,"Headset never installs iOS route observer");Assert(vrVoice.Connected&&!sdk.IsInputDeviceMuted,"Quest owner joins shared room chat with capture enabled");
  Assert(StareVoice.SpeechOpening(double.NaN,true)==0&&StareVoice.SpeechOpening(double.PositiveInfinity,true)==0&&StareVoice.SpeechOpening(.4,false)==0,"Invalid/absent energy cannot animate speech");
  vrVoice.ToggleVoice();Assert(!vrVoice.VoiceEnabled,"Voice preference toggle persists");await Tick(vrVoice);Call(vrVoice,"OnDestroy");Assert(!sdk.IsLoggedIn&&sdk.ActiveChannels.Count==0,"Destroyed Quest fixture leaves/logs out");Assert(StarePhoneAudioRoute.ends==1,"Headset teardown never alters phone audio routing");Console.WriteLine("PASS "+checks+" voice lifecycle checks (mock Vivox / "+Platform+")");
 }
 static void Main(){Run().GetAwaiter().GetResult();}
}

namespace UnityEngine.Android {
 public class PermissionCallbacks {public event Action<string> PermissionGranted,PermissionDenied;public void Complete(string p,bool allowed){if(allowed)PermissionGranted?.Invoke(p);else PermissionDenied?.Invoke(p);}}
 public static class Permission {public const string Microphone="mic";public static bool HasUserAuthorizedPermission(string p){return UnityEngine.Application.granted;}public static void RequestUserPermission(string p,PermissionCallbacks c){UnityEngine.Application.requests++;c.Complete(p,UnityEngine.Application.granted);}}
}
