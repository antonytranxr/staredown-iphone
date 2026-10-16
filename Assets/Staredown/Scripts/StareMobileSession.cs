using System;
using System.Linq;
using UnityEngine;

namespace Staredown
{
 // Phone clients share the host-owned tournament and room protocol with Quest.
 // This scene has an ordinary camera; no OVRCameraRig or XR runtime is started.
 public sealed class StareMobileSession : MonoBehaviour
 {
  public const int RenderFrameRate=30,MaxRenderLongEdge=1280,RenderSamples=2;
  public Camera ViewCamera;
  public StareMobileCamera MobileCamera {get;private set;}
  public StareVoice Voice {get;private set;}
  public string VoiceRoomId=>(transport as StareRelayNetwork)?.VoiceKey;
  public string PlayerName=>StarePlayerName.Saved;
  public bool HasPlayerName=>StarePlayerName.HasChosen;
  public bool MusicEnabled=>PlayerPrefs.GetInt("Staredown.MobileMusicEnabled",1)!=0;
  public float MusicVolume=>Mathf.Clamp01(PlayerPrefs.GetFloat("Staredown.MobileMusicVolume",.2f));
  public bool LandscapeMode {get;private set;}
  public ArenaState State {get;private set;}=new ArenaState();
  public int LocalId {get;private set;}=-1;
  public string RoomCode {get;private set;}="";
  public string Status {get;private set;}="Enable your camera, or explore a room first.";
  public bool Connected=>LocalId>=0;
  public bool Busy {get;private set;}
  public bool Hosting=>model!=null;
  public string StartReadinessStatus
  {
   get
   {
    var pending=State.fighters.Where(f=>!f.bot&&(!f.ready||!f.valid)).ToArray();
    if(pending.Length>0)return "Waiting for "+string.Join(", ",pending.Take(2).Select(f=>f.name))+ (pending.Length>2?" +"+(pending.Length-2):"")+" to calibrate";
    return State.fighters.Length<2?"Waiting for another player":"All players ready";
   }
  }
  public bool CanMove=>!Connected||State.phase==(int)MatchPhase.Lobby||State.fighters.Any(f=>f.id==LocalId&&!f.alive);
  public bool CanDance=>Connected&&State.fighters.Any(f=>f.id==LocalId&&!f.disconnected);
  public Vector2 MoveInput,LookInput;
  public StareVisuals Visuals {get;private set;}
  public StareCameraStream CameraHeads {get;private set;}
  Tournament model;IStareTransport transport;int generation,placed=-1,lastPhase=-1;
  bool lastAlive=true,practice,practiceStarted,profileSent;float sendAt,yaw,pitch;Vector3 floor=new Vector3(0,0,-10);
  AudioSource music,effects;AudioClip eliminationSound,victorySound;int effectPhase=-1,aliveCount=-1;
  float nextPerformanceCheck;int appliedThermal=-1;
  void Awake()
  {
   Application.targetFrameRate=RenderFrameRate;Application.runInBackground=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;
   SetLandscapeMode(false);
  }
  public void SetLandscapeMode(bool landscape)
  {
   LandscapeMode=landscape;MoveInput=LookInput=Vector2.zero;
#if STARE_MOBILE && !UNITY_EDITOR
   Screen.autorotateToPortrait=false;Screen.autorotateToPortraitUpsideDown=false;
   Screen.autorotateToLandscapeLeft=true;Screen.autorotateToLandscapeRight=true;
   Screen.orientation=landscape?ScreenOrientation.AutoRotation:ScreenOrientation.Portrait;
#endif
   MobileCamera?.RefreshOrientation();
  }
  public void SetMusicEnabled(bool enabled)
  {PlayerPrefs.SetInt("Staredown.MobileMusicEnabled",enabled?1:0);PlayerPrefs.Save();if(music!=null)music.mute=!enabled;}
  public void SetMusicVolume(float volume)
  {if(float.IsNaN(volume)||float.IsInfinity(volume))return;volume=Mathf.Clamp01(volume);PlayerPrefs.SetFloat("Staredown.MobileMusicVolume",volume);PlayerPrefs.Save();if(music!=null)music.volume=volume;}
  void Start()
  {
   if(ViewCamera==null){var go=new GameObject("Mobile arena camera");ViewCamera=go.AddComponent<Camera>();}
   if(ViewCamera.GetComponent<AudioListener>()==null)ViewCamera.gameObject.AddComponent<AudioListener>();
   ViewCamera.tag="MainCamera";ViewCamera.stereoTargetEye=StereoTargetEyeMask.None;ViewCamera.fieldOfView=65;ViewCamera.nearClipPlane=.05f;ViewCamera.farClipPlane=160;
   ViewCamera.allowHDR=false;ViewCamera.allowMSAA=true;
   ViewCamera.transform.SetPositionAndRotation(floor+Vector3.up*1.6f,Quaternion.identity);
   MobileCamera=gameObject.AddComponent<StareMobileCamera>();MobileCamera.FrameReady+=ForwardCamera;
   CameraHeads=gameObject.AddComponent<StareCameraStream>();
   Visuals=gameObject.AddComponent<StareVisuals>();Visuals.Build();Visuals.SetArenaTheme(0);HideVrMenus();
   Voice=gameObject.AddComponent<StareVoice>();Voice.Initialize(this);
   var hud=gameObject.AddComponent<StareMobileHud>();hud.Initialize(this);
   music=gameObject.AddComponent<AudioSource>();music.clip=Resources.Load<AudioClip>("Audio/ArenaPulse");music.loop=true;music.volume=MusicVolume;music.mute=!MusicEnabled;music.spatialBlend=0;music.Play();
   effects=gameObject.AddComponent<AudioSource>();effects.playOnAwake=false;effects.volume=.5f;
   eliminationSound=Resources.Load<AudioClip>("Audio/Effects/Eliminate");victorySound=Resources.Load<AudioClip>("Audio/Effects/Victory");
   ApplyPhonePerformance();
   Debug.Log("[STARE MOBILE] ready / ordinary camera / shared numeric Relay rooms / camera requires explicit consent");
  }
  void HideVrMenus()
  {
   if(Visuals.menu!=null)Visuals.menu.gameObject.SetActive(false);
   if(Visuals.characterMenu!=null)Visuals.characterMenu.gameObject.SetActive(false);
   Visuals.overview.enabled=false;if(Visuals.hud!=null)Visuals.hud.text="";
  }
  public void Host()=>Connect(true,"");
  public void Join(string code)=>Connect(false,code);
  public void Practice()
  {
   if(!RequirePlayerName())return;
   Leave();practice=true;LocalId=0;model=new Tournament();model.Add(0,PlayerName);model.EnableBotFill();model.SelectAvatar(0,2);Status="Practice: look at the camera and keep both eyes open to calibrate.";
  }
  public void SetPlayerName(string value)
  {
   if(!StarePlayerName.IsValid(value)){Status="Enter a name with at least one letter or number.";return;}
   string saved=StarePlayerName.Save(value);if(!Connected)return;
   if(model!=null)model.SetName(LocalId,saved);else transport?.Send(0,new WireMessage{kind="name",name=saved});
  }
  public void StartMatch()
  {
   if(model==null)return;
   if(MobileCamera==null||!MobileCamera.Running||!MobileCamera.Ready||!MobileCamera.Valid)
   {Status=MobileCamera!=null&&MobileCamera.Running?MobileCamera.Status:"Enable your camera and calibrate before starting.";return;}
   if(!model.Start())Status=StartReadinessStatus;
  }
  async void Connect(bool host,string code)
  {
   if(Busy)return;
   if(!RequirePlayerName())return;
   if(!host&&!StareRelayNetwork.ValidCode(code)){Status="Enter the 4-digit room code.";return;}
   Leave();Busy=true;int request=generation;var relay=new StareRelayNetwork();transport=relay;
   Status=host?"Creating room…":"Joining room…";
   try
   {
    if(host)
    {
     string room=await relay.HostAsync();if(request!=generation||this==null)return;
     while(!relay.Bound&&string.IsNullOrEmpty(relay.Error)){relay.Tick();await System.Threading.Tasks.Task.Delay(20);if(request!=generation||this==null)return;}
     if(!string.IsNullOrEmpty(relay.Error))throw new InvalidOperationException(relay.Error);
     RoomCode=room;LocalId=0;model=new Tournament();model.Add(0,PlayerName);model.EnableBotFill();model.SelectAvatar(0,2);Busy=false;Status="Share the room code. Explore while friends join.";
    }
    else {await relay.JoinAsync(code);if(request!=generation||this==null)return;RoomCode=StareRelayNetwork.NormalizeCode(code);Status="Connected to Relay. Waiting for the host…";}
   }
   catch(Exception e)
   {if(request!=generation||this==null)return;relay.Dispose();transport=null;Busy=false;LocalId=-1;RoomCode="";Status=e is InvalidOperationException||e is ArgumentException?e.Message:"Room connection failed. Check the code and internet, then try again.";Debug.LogWarning("[STARE MOBILE] "+Status);}
  }
  public void Leave()
  {
   generation++;transport?.Dispose();transport=null;model=null;LocalId=-1;Busy=false;RoomCode="";State=new ArenaState();CameraHeads?.Reset();profileSent=false;placed=-1;lastPhase=-1;practice=practiceStarted=false;MoveInput=LookInput=Vector2.zero;floor=new Vector3(0,0,-10);yaw=pitch=0;
   Status="Choose practice, create a room, or join friends.";
  }
  public void Act(string action)
  {
   if(action=="camera")
   {
    if(MobileCamera.Running){SendProfile(false);MobileCamera.StopCamera();}else MobileCamera.StartCamera();return;
   }
   if(action=="calibrate"){MobileCamera.Calibrate();return;}
   if(action=="bot-fill"){if(model!=null&&State.phase==(int)MatchPhase.Lobby)model.SetBotFill(!model.BotFillEnabled);return;}
   if(action=="music"){SetMusicEnabled(!MusicEnabled);return;}
   if(action=="mic"){Voice?.ToggleMic();return;}
   if(action=="voice"){Voice?.ToggleVoice();return;}
   if(action=="leave"){Leave();return;}
   if(action.StartsWith("dance:")){int dance;if(CanDance&&int.TryParse(action.Substring(6),out dance)&&dance>=-1&&dance<=3){if(model!=null)model.Dance(LocalId,dance>=0,Mathf.Max(0,dance));else transport?.Send(0,new WireMessage{kind="dance",dancing=dance>=0,dance=Mathf.Max(0,dance)});}return;}
  }
  bool RequirePlayerName()
  {if(HasPlayerName)return true;Status="Enter your name before entering the arena.";return false;}
  void SendProfile(bool enabled)
  {
   if(!Connected)return;
   var message=new WireMessage{kind="cameraProfile",camera=enabled,id=LocalId};
   if(model!=null)CameraHeads.Handle(message,LocalId,model,transport,LocalId);else transport?.Send(0,message);
   profileSent=enabled;
  }
  void ForwardCamera(byte[] jpeg)
  {if(Connected&&MobileCamera.Running)CameraHeads.Publish(jpeg,LocalId,model,transport);}
  void Pump()
  {
   if(transport==null)return;transport.Tick();StareNetwork.Incoming packet;
   for(int i=0;i<100&&transport.Poll(out packet);i++)
   {
    WireMessage message;try{message=JsonUtility.FromJson<WireMessage>(packet.json);}catch{transport.ClosePeer(packet.sender);continue;}if(message==null)continue;
    if(message.kind=="failed"&&packet.sender<0){string error=transport.Error;Leave();Status="Connection ended: "+error;break;}
    if(CameraHeads.Handle(message,packet.sender,model,transport,LocalId))continue;
    if(model!=null)
    {
     if(message.kind=="connected"){if(model.Add(packet.sender,"PLAYER "+(packet.sender+1))==null){transport.ClosePeer(packet.sender);continue;}transport.Send(packet.sender,new WireMessage{kind="welcome",id=packet.sender});}
     else if(message.kind=="disconnected")model.Remove(packet.sender);
     else if(message.kind=="name")model.SetName(packet.sender,message.name);
     else if(message.kind=="input"&&Finite(message.left)&&Finite(message.right)&&Finite(message.yaw)&&Finite(message.pitch)){model.Input(packet.sender,message.left,message.right,message.valid,message.ready,message.blink,message.yaw,message.pitch,message.demo);model.Presence(packet.sender,message.presence);if(message.hasRoomPose)model.RoomPose(packet.sender,message.roomPosition,message.roomYaw);}
     else if(message.kind=="avatar")model.SelectAvatar(packet.sender,message.avatar,message.avatarUserId);
     else if(message.kind=="skin")model.SelectSkin(packet.sender,message.skin);
     else if(message.kind=="dance")model.Dance(packet.sender,message.dancing,message.dance);
     else if(message.kind=="throw")model.Throw(packet.sender,message.prop,message.origin,message.velocity);
     else if(message.kind=="avatarPose"){byte[] pose;var fighter=model.Find(packet.sender);if(fighter!=null&&fighter.avatar==StareAvatarCatalog.Meta&&model.Clock-fighter.lastAvatarPose>=.08f&&TryPose(message.pose,out pose)){fighter.lastAvatarPose=model.Clock;transport.Broadcast(new WireMessage{kind="avatarPose",id=packet.sender,pose=message.pose});}}
    }
    else
    {
     if(message.kind=="welcome"){LocalId=message.id;Busy=false;transport.Send(0,new WireMessage{kind="avatar",avatar=2});transport.Send(0,new WireMessage{kind="name",name=PlayerName});SendProfile(MobileCamera.Running);Status="Room "+RoomCode+". Explore and wait for the host.";}
     if(message.kind=="state"&&message.state!=null)State=message.state;
     if(message.kind=="disconnected"){string error=transport.Error;Leave();Status="Connection ended: "+error;break;}
    }
   }
  }
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  static bool TryPose(string text,out byte[] pose){pose=null;if(string.IsNullOrEmpty(text)||text.Length>5464)return false;try{pose=Convert.FromBase64String(text);return pose.Length>0&&pose.Length<=4096;}catch{return false;}}
  void Update()
  {
   if(MobileCamera==null||Visuals==null)return;
   if(Time.unscaledTime>=nextPerformanceCheck){nextPerformanceCheck=Time.unscaledTime+3;ApplyPhonePerformance();}
   Pump();PlaceForPhase();float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
   yaw+=LookInput.x;pitch=Mathf.Clamp(pitch-LookInput.y,-65,65);LookInput=Vector2.zero;
   if(CanMove){var movement=Quaternion.Euler(0,yaw,0)*new Vector3(MoveInput.x,0,MoveInput.y)*3*dt;floor=StareArenaFields.Clamp(State,floor+movement);}
   ViewCamera.transform.SetPositionAndRotation(floor+Vector3.up*1.6f,Quaternion.Euler(pitch,yaw,0));
   if(Connected&&MobileCamera.Running!=profileSent)SendProfile(MobileCamera.Running);
   var presence=new StarePresenceFrame{headTracked=MobileCamera.Valid,head=Quaternion.Euler(MobileCamera.Pitch,MobileCamera.Yaw,0)};
   if(model!=null)
   {
    model.Input(LocalId,MobileCamera.Left,MobileCamera.Right,MobileCamera.Valid,MobileCamera.Ready,MobileCamera.BlinkSequence,MobileCamera.Yaw,MobileCamera.Pitch,false);
    model.Presence(LocalId,presence);if(CanMove)model.RoomPose(LocalId,floor,yaw);
    if(practice&&!practiceStarted&&model.Phase==MatchPhase.Lobby&&MobileCamera.Ready&&MobileCamera.Valid&&model.Start())practiceStarted=true;model.Tick(dt);State=model.Snapshot();
   }
   if(transport is StareRelayNetwork relay)relay.SetRoomOpen(State.phase==0&&State.fighters.Count(f=>!f.bot)<Tournament.Capacity);
   if(Time.unscaledTime>=sendAt){sendAt=Time.unscaledTime+.05f;if(model!=null)transport?.Broadcast(new WireMessage{kind="state",state=State});else if(Connected)transport?.Send(0,new WireMessage{kind="input",left=MobileCamera.Left,right=MobileCamera.Right,valid=MobileCamera.Valid,ready=MobileCamera.Ready,blink=MobileCamera.BlinkSequence,yaw=MobileCamera.Yaw,pitch=MobileCamera.Pitch,demo=false,presence=presence,hasRoomPose=CanMove,roomPosition=floor,roomYaw=yaw});}
   PlaceForPhase();Visuals.SetArenaTheme(State.arenaTheme);Visuals.UpdateState(State,LocalId,false);HideVrMenus();CameraHeads.Sync(Visuals,State,LocalId);Feedback();
  }
  void PlaceForPhase()
  {
   var local=State.fighters.FirstOrDefault(f=>f.id==LocalId);if(local==null)return;
   bool phaseChanged=placed!=LocalId||lastPhase!=State.phase;
   if(phaseChanged||local.alive!=lastAlive){floor=ArenaLayout.Seat(State,LocalId);floor.y=0;var direction=ArenaLayout.Facing(State,LocalId);yaw=Quaternion.LookRotation(direction).eulerAngles.y;pitch=0;placed=LocalId;lastPhase=State.phase;lastAlive=local.alive;}
   if(!CanMove){floor=ArenaLayout.Seat(State,LocalId);floor.y=0;}
  }
  void Feedback()
  {
   int alive=State.fighters.Count(f=>f.alive);
   if(aliveCount>=0&&alive<aliveCount&&State.phase!=0&&eliminationSound!=null)effects.PlayOneShot(eliminationSound,.56f);
   if(effectPhase!=State.phase&&State.phase==(int)MatchPhase.Champion&&victorySound!=null)effects.PlayOneShot(victorySound,.3f);
   effectPhase=State.phase;aliveCount=alive;
  }
  public static Vector2Int RenderResolution(int width,int height)
  {
   float scale=Mathf.Min(1,MaxRenderLongEdge/(float)Mathf.Max(1,Mathf.Max(width,height)));
   return new Vector2Int(Mathf.Max(1,Mathf.RoundToInt(width*scale)),Mathf.Max(1,Mathf.RoundToInt(height*scale)));
  }
  void ApplyPhonePerformance()
  {
#if STARE_MOBILE && !UNITY_EDITOR
   int thermal=MobileCamera!=null?MobileCamera.ThermalLevel:0;if(appliedThermal==thermal)return;appliedThermal=thermal;
   Application.targetFrameRate=thermal>=2?24:RenderFrameRate;
   // The built-in forward camera needs both this sample count and allowMSAA.
   // Keep offscreen captures at their lower budgets; only the main view uses MSAA.
   QualitySettings.vSyncCount=0;QualitySettings.antiAliasing=RenderSamples;QualitySettings.softParticles=false;
   QualitySettings.pixelLightCount=1;QualitySettings.shadowDistance=20;QualitySettings.shadowCascades=1;
   QualitySettings.shadows=ShadowQuality.HardOnly;QualitySettings.shadowResolution=ShadowResolution.Low;
   QualitySettings.lodBias=Mathf.Min(QualitySettings.lodBias,.85f);
   // Keep four skin weights: lowering them changes the repaired facial rigs.
   QualitySettings.skinWeights=SkinWeights.FourBones;
   var size=RenderResolution(Screen.width,Screen.height);if(size.x!=Screen.width||size.y!=Screen.height)Screen.SetResolution(size.x,size.y,FullScreenMode.FullScreenWindow);
   Debug.Log("[STARE MOBILE PERFORMANCE] render="+size.x+"x"+size.y+" fps="+Application.targetFrameRate+" msaa="+QualitySettings.antiAliasing+" thermal="+thermal+" / eye capture remains independent");
#endif
  }
  void OnApplicationPause(bool paused){if(paused){SendProfile(false);MobileCamera?.StopCamera();MoveInput=LookInput=Vector2.zero;}}
  void OnDestroy(){generation++;transport?.Dispose();if(MobileCamera!=null)MobileCamera.FrameReady-=ForwardCamera;CameraHeads?.Reset();Screen.sleepTimeout=SleepTimeout.SystemSetting;}
#if UNITY_EDITOR
  public void TestConnect(IStareTransport testTransport,bool host){Leave();transport=testTransport;if(host){model=new Tournament();model.Add(0,PlayerName);model.EnableBotFill();LocalId=0;}else Busy=true;}
#endif
 }
}
