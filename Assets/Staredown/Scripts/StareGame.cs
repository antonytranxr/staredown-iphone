using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Staredown
{
 public sealed class StareGame : MonoBehaviour
 {
  public StareEyes eyes; public OVRCameraRig rig;
  public ArenaState State { get; private set; } = new ArenaState();
  StareVisuals view; Tournament model; IStareTransport net; int localId=-1; float sendAt; bool autoStart, broadcast,matchMenuOpen;
  string address="", notice="Choose how to play";
  int connectionGeneration;bool roomBusy;string roomCode;
  public bool RoomBusy=>roomBusy;public string RoomCode=>roomCode; Vector3 lastSeat=new Vector3(999,0,0);
  public string VoiceRoomId=>(net as StareRelayNetwork)?.VoiceKey;
  OVRHand[] hands; OVRHand leftMenuHand; bool[] pinched,pinchUsedForUi; Vector3[] previous,velocity; Camera[] rigCameras;
  StareNavigation navigation;float menuPlaceAt;
  public int LocalId=>localId;
  public bool InLobby=>State.phase==(int)MatchPhase.Lobby;
  bool lobbyMenuOpen=true,lastLocalAlive=true;int placedPlayerId=-1,lastPhase=(int)MatchPhase.Lobby;
  public bool IsSpectator=>!InLobby&&State.fighters.Any(f=>f.id==localId&&!f.alive);
  public bool CanMove=>InLobby||IsSpectator;
  public Vector3 LocalFloor=>new Vector3(rig.centerEyeAnchor.position.x,requestedFloor.y,rig.centerEyeAnchor.position.z);
  public string LobbySummary=>roomBusy?notice:localId<0?notice:State.fighters.Count(f=>!f.bot)+" humans + "+State.fighters.Count(f=>f.bot)+" bots / 16\n"+(State.fighters.Any(f=>!f.ready||!f.valid)?"Calibrate in Settings before starting.":model!=null?"You are the host. Ready to start.":"Ready. Waiting for the host.");
  public string InputStatus {get;private set;}="WAITING FOR INPUT";
  public void RecallMenu(){if(view==null)return;if(InLobby){lobbyMenuOpen=true;view.PlaceLobbyMenus(rig.centerEyeAnchor);}else{view.UpdateMatchMenu(false,State,localId,rig.centerEyeAnchor);matchMenuOpen=true;}}
  void OnApplicationFocus(bool focused){if(focused)menuPlaceAt=Time.unscaledTime+.5f;}
  StareProgression progression;
  StareMusic music;StarePresence presence;StarePinchGate[] pinchGates;StareHandRayStabilizer[] handStabilizers;
  readonly bool[] controllersTracked=new bool[2];
  readonly StarePhysicalGrab[] physicalGrabs={new StarePhysicalGrab(),new StarePhysicalGrab()};
  StareUIPointer uiPointer;StareSpectatorTools spectatorTools;StareVoice voice;StareCameraStream cameraStream;
  public bool MatchMenuOpen=>InterfaceOpen;
  public bool SlingshotAvailable=>spectatorTools!=null&&spectatorTools.CanThrow;
  public bool IsHandInteractionBusy=>spectatorTools!=null&&spectatorTools.Drawing;
  public void RotateSlingshotAim(Quaternion rotation){spectatorTools?.RotateWithRig(rotation);}
  public bool CanDance=>localId>=0&&State.fighters.Any(f=>f.id==localId&&!f.disconnected);
  public bool Dancing=>State.fighters.Any(f=>f.id==localId&&f.dancing);
  public bool RayHitsUi(Ray ray)
  {
   foreach(var button in StareButton.Active){RaycastHit hit;var collider=button!=null?button.GetComponent<Collider>():null;if(button!=null&&button.Interactable&&collider!=null&&collider.enabled&&collider.Raycast(ray,out hit,12))return true;}return false;
  }
  Vector3 requestedFloor,requestedFacing;bool firstTrackedPlacement;OVRManager trackingManager;float nextOriginRetry;
#if UNITY_EDITOR
  bool showDesktopShortcuts; GUIStyle shortcutStyle;
#endif
  int selectedAvatar; bool choosingAvatar,waitingForMeta; float nextAvatarPose;
  string avatarNotice="SELECT A CHARACTER, THEN CONTINUE";
  readonly Dictionary<int,byte[]> pendingAvatarPoses=new Dictionary<int,byte[]>();
  public int SelectedAvatar => selectedAvatar;
  int preferredArenaTheme;
  public int ArenaTheme=>localId>=0?Mathf.Clamp(State.arenaTheme,0,2):preferredArenaTheme;
  public bool CanChooseArena=>InLobby&&!roomBusy&&(localId<0||model!=null);
  void Awake()
  {
   Application.runInBackground=true;
   // Seats specify the arena floor, not the height of the player's eyes.
   trackingManager=rig!=null?rig.GetComponent<OVRManager>():FindFirstObjectByType<OVRManager>();
   if(trackingManager!=null)trackingManager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
  }
  void Start()
  {
   int saved=PlayerPrefs.GetInt("Staredown.Avatar",0);selectedAvatar=StareAvatarCatalog.IsPreset(saved)?saved:0;
   preferredArenaTheme=Mathf.Clamp(PlayerPrefs.GetInt("Staredown.ArenaTheme",0),0,2);
   if(StareMetaAvatars.Instance==null)gameObject.AddComponent<StareMetaAvatars>();
   view=gameObject.AddComponent<StareVisuals>(); view.Build(); hands=rig.GetComponentsInChildren<OVRHand>(true);
   view.SetArenaTheme(preferredArenaTheme);view.SetLocalViewer(rig.centerEyeAnchor);
   leftMenuHand=rig.leftHandAnchor.GetComponentInChildren<OVRHand>(true);
   pinched=new bool[hands.Length];pinchUsedForUi=new bool[hands.Length]; previous=new Vector3[hands.Length]; velocity=new Vector3[hands.Length];
   for(int i=0;i<hands.Length;i++)
   {
    previous[i]=hands[i].transform.position;
    hands[i].gameObject.AddComponent<StareTrackedHands>();
    // OVRHand supplies tracking-space poses. Unparented pointer transforms
    // otherwise stay at the scene origin when the player moves to the lobby.
    var pointer=hands[i].PointerPose;
    if(pointer!=null&&pointer.parent==null)pointer.SetParent(rig.trackingSpace,false);
   }
   gameObject.AddComponent<StareFeedback>();
   gameObject.AddComponent<StareDistractions>();
   gameObject.AddComponent<StareCompetitionPresentation>().Initialize(this,view);
   gameObject.AddComponent<StareAnnouncer>();
   music=gameObject.AddComponent<StareMusic>();
   pinchGates=new StarePinchGate[hands.Length];handStabilizers=new StareHandRayStabilizer[hands.Length];for(int i=0;i<hands.Length;i++){pinchGates[i]=new StarePinchGate();handStabilizers[i]=new StareHandRayStabilizer();}
   uiPointer=gameObject.AddComponent<StareUIPointer>();
   rigCameras=rig.GetComponentsInChildren<Camera>(true);foreach(var camera in rigCameras)camera.cullingMask&=~(1<<28); SeatLobby();
   navigation=gameObject.AddComponent<StareNavigation>();navigation.Initialize(this,view);
   spectatorTools=gameObject.AddComponent<StareSpectatorTools>();spectatorTools.Initialize(this,view);
   voice=gameObject.AddComponent<StareVoice>();voice.Initialize(this);
   cameraStream=gameObject.AddComponent<StareCameraStream>();
   progression=gameObject.AddComponent<StareProgression>();progression.Initialize(this);
   menuPlaceAt=Time.unscaledTime+.75f;
   gameObject.AddComponent<StareHeadsetDiagnostics>().Initialize(rig,eyes,view);
  }
  void OnDestroy(){connectionGeneration++;net?.Dispose();}
  public void Act(string action)
  {
   if(action=="name-panel"){if(InLobby)view.ShowNamePanel(true,StarePlayerName.Saved);return;}
   if(action=="name-back"){view.ShowNamePanel(false,StarePlayerName.Saved);return;}
   if(action.StartsWith("name-key:")){if(InLobby)view.EditPlayerName(action.Substring(9));return;}
   if(action=="name-save")
   {
    if(!InLobby)return;
    string name=StarePlayerName.Save(view.NameDraft);
    if(model!=null)model.SetName(localId,name);else if(localId>=0)net?.Send(0,new WireMessage{kind="name",name=name});
    view.SetPlayerNameLabel(name);view.ShowNamePanel(false,name);return;
   }
   if(action=="intro-done"){view.ShowOpeningInstructions(false);RecallMenu();return;}
   if(action=="how-to-play"){if(InLobby){view.ShowOpeningInstructions(true);RecallMenu();}return;}
   if(action=="skins"){if(InLobby){view.ShowSkins(true,progression);RecallMenu();}return;}
   if(action=="skins-back"){view.ShowSkins(false,progression);return;}
   if(action.StartsWith("skin:")){int skin;if(InLobby&&int.TryParse(action.Substring(5),out skin)&&progression.Select(skin)){if(model!=null)model.SelectSkin(localId,skin);else if(localId>=0)net?.Send(0,new WireMessage{kind="skin",skin=skin});}view.UpdateSkinChoices(progression);return;}
   if(action=="fps-toggle"){GetComponent<StareHeadsetDiagnostics>()?.Toggle();return;}
   if(action.StartsWith("arena:"))
   {
    int choice;
    if(!CanChooseArena||!int.TryParse(action.Substring(6),out choice)||choice<0||choice>2)return;
    preferredArenaTheme=choice;PlayerPrefs.SetInt("Staredown.ArenaTheme",choice);PlayerPrefs.Save();
    if(model!=null){model.SelectArena(choice);State=model.Snapshot();}
    view.SetArenaTheme(choice);return;
   }
   if(action.StartsWith("nav:"))
   {
    if(action=="nav:menu")RecallMenu();
    if(action=="nav:forward")navigation.Move(new Vector2(0,.5f));
    if(action=="nav:back")navigation.Move(new Vector2(0,-.5f));
    if(action=="nav:left")navigation.Turn(-30);
    if(action=="nav:right")navigation.Turn(30);
    return;
   }
   if(action=="toss"||action=="throw-help"){spectatorTools.ShowHelp();return;}
   if(action=="dance"){if(CanDance)view.ShowDanceChoices(rig.centerEyeAnchor,!view.DanceChoicesVisible);return;}
   if(action=="dance-back"){view.ShowDanceChoices(rig.centerEyeAnchor,false);return;}
   if(action.StartsWith("dance:")){int choice;if(CanDance&&int.TryParse(action.Substring(6),out choice)&&choice>=-1&&choice<StareDanceCatalog.Count){if(model!=null)model.Dance(localId,choice>=0,Mathf.Max(0,choice));else net?.Send(0,new WireMessage{kind="dance",dancing=choice>=0,dance=Mathf.Max(0,choice)});view.ShowDanceChoices(rig.centerEyeAnchor,false);}return;}
   if(action=="bot-fill"){if(model!=null&&InLobby&&net is StareRelayNetwork){model.SetBotFill(!model.BotFillEnabled);PlayerPrefs.SetInt("Staredown.BotFill",model.BotFillEnabled?1:0);PlayerPrefs.Save();}return;}
   if(action=="mic"){if(!voice.VoiceEnabled){voice.ToggleVoice();voice.EnableMicrophone();}else if(!voice.MicPermissionGranted)voice.EnableMicrophone();else voice.ToggleMic();return;}
   if(action=="voice-reconnect"){voice.ReconnectVoice();return;}
   if(action=="voice"){voice.ToggleVoice();return;}
   if(action=="music"){music.Toggle();view.SetMusicLabel(music.Muted,music.Volume);return;}
   if(action=="music-down"||action=="music-up"){music.SetVolume(music.Volume+(action=="music-up"?.1f:-.1f));view.SetMusicLabel(music.Muted,music.Volume);return;}
   if(action=="match-menu"){if(InLobby){if(lobbyMenuOpen&&localId>=0){lobbyMenuOpen=false;choosingAvatar=false;}else RecallMenu();}else matchMenuOpen=!matchMenuOpen;view.HideAuxiliaryPanels();return;}
   if(action=="resume"){matchMenuOpen=false;view.HideAuxiliaryPanels();return;}
   if(action=="explore"){if(InLobby&&localId>=0){lobbyMenuOpen=false;choosingAvatar=false;}return;}
   if(action=="settings"){view.ShowSettingsPanel(true);return;}
   if(action=="settings-back"){view.ShowSettingsPanel(false);return;}
   if(action=="join-panel"){view.ShowJoinPanel(true);return;}
   if(action=="join-back"){view.ShowJoinPanel(false);return;}
   if(action=="characters"){if(State.phase==0)choosingAvatar=true;return;}
   if(action=="character-done"){choosingAvatar=false;RecallMenu();return;}
   if(action.StartsWith("avatar-page:")){if(State.phase==0){int page;if(int.TryParse(action.Substring(12),out page))view.ChangeCharacterPage(page);}return;}
   if(action.StartsWith("avatar:"))
   {
    if(State.phase!=0){avatarNotice="CHARACTERS ARE LOCKED DURING THE TOURNAMENT";return;}
    int choice;if(!int.TryParse(action.Substring(7),out choice)||(!StareAvatarCatalog.IsPreset(choice)&&choice!=StareAvatarCatalog.Meta))return;
    if(choice==StareAvatarCatalog.Meta&&!StareMetaAvatars.Instance.Ready)
    {waitingForMeta=true;StareMetaAvatars.Instance.Connect();avatarNotice=StareMetaAvatars.Instance.Status;return;}
    SelectAvatar(choice);return;
   }
   if(action.StartsWith("key:")){var key=action.Substring(4); address=key=="DEL"?(address.Length>0?address.Substring(0,address.Length-1):""):key=="CLEAR"?"":address.Length<StareRelayNetwork.CodeLength&&key.Length==1&&key[0]>='0'&&key[0]<='9'?address+key:address;return;}
   if(action=="blink"){eyes.RequestDemoBlink();return;}
   if(action=="input-mode")
   {
    if(!InLobby){notice="INPUT MODE IS LOCKED DURING A TOURNAMENT";return;}
    if(eyes.CanChooseEyeMode)eyes.UseSimulation(!eyes.simulated);return;
   }
   if(action=="calibrate"){eyes.Calibrate();return;}
   if(action=="start"){if(model!=null)model.Start();return;}
   if(action=="leave"){LeaveRoom(false);return;}
   if(action=="host"||action=="join"){if(!roomBusy)ConnectRoom(action=="host",address);return;}
   if(action=="demo")
   {
    Act("leave");choosingAvatar=false;eyes.Calibrate();model=new Tournament();model.SelectArena(preferredArenaTheme);localId=0;model.Add(0,StarePlayerName.Saved);SendAvatarSelection();
    for(int i=1;i<Tournament.Capacity;i++)model.Add(i,StareAvatarCatalog.Names[StareAvatarCatalog.PresetId(i)]+" "+(i+1),true);
    autoStart=true;notice=eyes.simulated?"PRACTICE / BUTTON DEMO":"PRACTICE / REAL QUEST PRO BLINKS";return;
   }
  }
  void LeaveRoom(bool preserveLobbyMenu)
  {
   bool preserve=preserveLobbyMenu&&InLobby;
   view.HideAuxiliaryPanels();cameraStream?.Reset();connectionGeneration++;roomBusy=false;roomCode=null;
   view.ShowJoinPanel(false);view.ShowSettingsPanel(false);matchMenuOpen=false;lobbyMenuOpen=true;
   placedPlayerId=-1;lastPhase=0;net?.Dispose();net=null;model=null;localId=-1;autoStart=false;
   pendingAvatarPoses.Clear();State=new ArenaState();
   if(preserve){lastSeat=new Vector3(999,0,0);menuPlaceAt=0;}else SeatLobby();
   notice="Choose how to play";
  }
  async void ConnectRoom(bool host,string code)
  {
   if(!host&&!StareRelayNetwork.ValidCode(code)){notice="ENTER THE 4-DIGIT ROOM CODE";view.SetJoinStatus(notice);return;}
   LeaveRoom(true);choosingAvatar=false;eyes.Calibrate();roomBusy=true;
   bool failed=false;int generation=connectionGeneration;var relay=new StareRelayNetwork();net=relay;
   notice=host?"CREATING ONLINE ROOM...":"JOINING ROOM "+StareRelayNetwork.NormalizeCode(code)+"...";
   try
   {
    if(host)
    {
     string created=await relay.HostAsync();if(generation!=connectionGeneration||this==null)return;
     while(!relay.Bound&&string.IsNullOrEmpty(relay.Error)){relay.Tick();await System.Threading.Tasks.Task.Delay(20);if(generation!=connectionGeneration||this==null)return;}
     if(!string.IsNullOrEmpty(relay.Error))throw new Exception(relay.Error);
     model=new Tournament();model.SelectArena(preferredArenaTheme);localId=0;model.Add(0,StarePlayerName.Saved);model.SetBotFill(PlayerPrefs.GetInt("Staredown.BotFill",1)!=0);SendAvatarSelection();roomCode=created;
     notice="Share the code. Explore while friends join.";if(Debug.isDebugBuild)Debug.Log("[STARE ROOM] created code="+roomCode);
    }
    else {await relay.JoinAsync(code);if(generation!=connectionGeneration||this==null)return;roomCode=relay.RoomCode;}
   }
   catch(Exception e)
   {
    if(generation!=connectionGeneration||this==null)return;
    failed=true;relay.Dispose();net=null;localId=-1;model=null;
    notice=RoomError(e);Debug.LogWarning("[STARE ROOM] "+notice);
   }
   finally {if(generation==connectionGeneration&&this!=null&&(host||failed))roomBusy=false;}
  }
  static string RoomError(Exception e)=>StareRelayNetwork.ConnectionError(e);
  void SelectAvatar(int choice)
  {
   selectedAvatar=choice;waitingForMeta=false;PlayerPrefs.SetInt("Staredown.Avatar",choice);PlayerPrefs.Save();
   avatarNotice=StareAvatarCatalog.Names[choice]+" SELECTED";SendAvatarSelection();
  }
  void SendAvatarSelection()
  {
   if(progression!=null){if(model!=null)model.SelectSkin(localId,progression.Equipped);else net?.Send(0,new WireMessage{kind="skin",skin=progression.Equipped});}
   string user=selectedAvatar==StareAvatarCatalog.Meta?StareMetaAvatars.Instance.UserId:null;
   if(!StareAvatarCatalog.IsValid(selectedAvatar,user))return;
   if(model!=null)model.SelectAvatar(localId,selectedAvatar,user);
   else if(localId>=0)net?.Send(0,new WireMessage{kind="avatar",avatar=selectedAvatar,avatarUserId=user});
  }
  static string LocalAddress(){try{return System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()).First(x=>x.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!System.Net.IPAddress.IsLoopback(x)).ToString();}catch{return "CHECK WI-FI IP";}}
  void Pump()
  {
   if(net==null)return;net.Tick();
   for(int i=0;i<100&&net.Poll(out var packet);i++)
   {
    WireMessage m;try{m=JsonUtility.FromJson<WireMessage>(packet.json);}catch{net.ClosePeer(packet.sender);continue;}if(m==null)continue;
    if(cameraStream!=null&&cameraStream.Handle(m,packet.sender,model,net,localId))continue;
    if(m.kind=="failed"&&packet.sender<0) {string error=net.Error;Act("leave");notice="CONNECTION ENDED / "+error;break;}
    if(model!=null)
    {
     if(m.kind=="connected") {if(model.JoinHuman(packet.sender,"PLAYER "+(packet.sender+1))==null){if(net is StareRelayNetwork relay)relay.RejectPeer(packet.sender,"ROOM FULL / WAIT FOR A PLAYER TO LEAVE");else net.ClosePeer(packet.sender);continue;}net.Send(packet.sender,new WireMessage{kind="welcome",id=packet.sender,capturedBlinks=true,state=model.Snapshot()});}
     else if(m.kind=="blinkEvent")net.Send(packet.sender,new WireMessage{kind="blinkAck",blinkEvent=m.blinkEvent,error=model.AcceptCapturedBlink(packet.sender,m.blinkEvent)});
     else if(m.kind=="disconnected")model.Remove(packet.sender);
     else if(m.kind=="dance")model.Dance(packet.sender,m.dancing,m.dance);
     else if(m.kind=="skin")model.SelectSkin(packet.sender,m.skin);
     else if(m.kind=="name")model.SetName(packet.sender,m.name);
     else if(m.kind=="avatar")model.SelectAvatar(packet.sender,m.avatar,m.avatarUserId);
     else if(m.kind=="avatarPose")
     {
      var f=model.Find(packet.sender);byte[] pose;
      if(f!=null&&f.avatar==StareAvatarCatalog.Meta&&model.Clock-f.lastAvatarPose>=.08f&&TryPose(m.pose,out pose))
      {f.lastAvatarPose=model.Clock;pendingAvatarPoses[packet.sender]=pose;net.Broadcast(new WireMessage{kind="avatarPose",id=packet.sender,pose=m.pose});}
     }
     else if(m.kind=="input"&&Finite(m.left)&&Finite(m.right)&&Finite(m.yaw)&&Finite(m.pitch))
     {model.Input(packet.sender,m.left,m.right,m.valid,m.ready,m.blink,m.yaw,m.pitch,m.demo,m.capturedBlinks);model.Presence(packet.sender,m.presence);if(m.hasRoomPose)model.RoomPose(packet.sender,m.roomPosition,m.roomYaw);}
     else if(m.kind=="throw")model.Throw(packet.sender,m.prop,m.origin,m.velocity);
    }
    else
    {
     if(m.kind=="welcome"){roomBusy=false;localId=m.id;if(m.state!=null)State=m.state;net?.Send(0,new WireMessage{kind="name",name=StarePlayerName.Saved});if(Debug.isDebugBuild)Debug.Log("[STARE ROOM] joined code="+roomCode+" player="+localId);SendAvatarSelection();notice=State.phase!=(int)MatchPhase.Lobby?"ROOM "+roomCode+" / SPECTATING / PLAY NEXT MATCH":"ROOM "+roomCode+" / CONNECTED / WAIT FOR HOST";}
     if(m.kind=="state"&&m.state!=null)State=m.state;
     if(m.kind=="avatarPose"&&m.id!=localId&&State.fighters.Any(f=>f.id==m.id&&f.avatar==StareAvatarCatalog.Meta))
     {byte[] pose;if(TryPose(m.pose,out pose))pendingAvatarPoses[m.id]=pose;}
     if(m.kind=="disconnected"||m.kind=="failed"){string error=net.Error;Act("leave");notice="CONNECTION ENDED / "+error;break;}
    }
   }
  }
  static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
  static bool TryPose(string data,out byte[] bytes)
  {
   bytes=null;if(string.IsNullOrEmpty(data)||data.Length>5464)return false;
   try{bytes=Convert.FromBase64String(data);return bytes.Length>0&&bytes.Length<=4096;}catch(FormatException){return false;}
  }
  void Update()
  {
   if(trackingManager!=null&&trackingManager.trackingOriginType==OVRManager.TrackingOrigin.EyeLevel&&Time.unscaledTime>=nextOriginRetry)
   {nextOriginRetry=Time.unscaledTime+1;trackingManager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;firstTrackedPlacement=false;}
   if(!firstTrackedPlacement&&(trackingManager==null||trackingManager.trackingOriginType!=OVRManager.TrackingOrigin.EyeLevel)&&OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter))
   {
    if(!InLobby)Place(requestedFloor,requestedFacing);menuPlaceAt=Time.unscaledTime+.5f;firstTrackedPlacement=true;
    if(Application.isMobilePlatform&&OVRManager.display!=null)
    {
     var rates=OVRManager.display.displayFrequenciesAvailable;
     if(rates!=null&&Array.Exists(rates,x=>Mathf.Abs(x-72)<.1f))OVRManager.display.displayFrequency=72;
    }
   }
   if(menuPlaceAt>0&&Time.unscaledTime>=menuPlaceAt){if(InLobby)RecallMenu();menuPlaceAt=0;}
   Pump();SyncPlacement();float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
   if(presence==null){presence=gameObject.AddComponent<StarePresence>();presence.Initialize(this,voice);}var bodyFrame=presence.Capture();
   if(model!=null)
   {
    model.Input(localId,eyes.Left,eyes.Right,eyes.Valid,eyes.Ready,eyes.BlinkSequence,eyes.Yaw,eyes.Pitch,eyes.simulated);
    model.Presence(localId,bodyFrame);
    if(CanMove&&placedPlayerId==localId)model.RoomPose(localId,LocalFloor,rig.centerEyeAnchor.eulerAngles.y);
    if(autoStart&&eyes.Ready&&model.Start())autoStart=false;
    model.Tick(dt);State=model.Snapshot();
   }
   SyncPlacement();
   if(net is StareRelayNetwork room)room.SetRoomOpen(model!=null&&model.CanAdmitHuman);
   if(Time.unscaledTime>=sendAt){sendAt=Time.unscaledTime+.05f;
    if(model!=null)net?.Broadcast(new WireMessage{kind="state",state=State});
    else if(localId>=0)net?.Send(0,new WireMessage{kind="input",left=eyes.Left,right=eyes.Right,valid=eyes.Valid,ready=eyes.Ready,blink=eyes.BlinkSequence,yaw=eyes.Yaw,pitch=eyes.Pitch,demo=eyes.simulated,presence=bodyFrame,hasRoomPose=CanMove&&placedPlayerId==localId,roomPosition=LocalFloor,roomYaw=rig.centerEyeAnchor.eulerAngles.y});
   }
   bool lobby=State.phase==(int)MatchPhase.Lobby;
   if(waitingForMeta){avatarNotice=StareMetaAvatars.Instance.Status;if(StareMetaAvatars.Instance.Ready&&lobby)SelectAvatar(StareAvatarCatalog.Meta);}
   view.SetMusicLabel(music!=null&&music.Muted,music!=null?music.Volume:1);view.SetFpsLabel(GetComponent<StareHeadsetDiagnostics>()?.Visible??false);
   view.SetPlayerNameLabel(StarePlayerName.Saved);
   view.SetArenaTheme(ArenaTheme);
   progression.Observe(State,localId,VoiceRoomId);view.UpdateSkinChoices(progression);
   view.UpdateArenaSelection(ArenaTheme,CanChooseArena,localId>=0&&model==null);view.UpdateDanceControls(CanDance,Dancing,State.fighters.FirstOrDefault(f=>f.id==localId)?.dance??0);
   view.UpdateState(State,localId,broadcast);
   cameraStream?.Sync(view,State,localId);
   view.UpdateMatchMenu(!lobby&&matchMenuOpen,State,localId,rig.centerEyeAnchor);
   bool menuController=StareControllerAim.TryRead(true,rig.trackingSpace,out var menuControllerRay,out _,out _,out _,out _);
   bool rightMenuTracked=StareControllerAim.TryRead(false,rig.trackingSpace,out var rightMenuRay,out _,out _,out _,out _);
   bool menuRightController=rightMenuTracked&&(!menuController||spectatorTools.CanThrow);
   var menuAnchor=menuRightController?rig.rightHandAnchor:rig.leftHandAnchor;
   var menuPoint=menuController||menuRightController?StareControllerAim.GripPosition(!menuRightController,rig.trackingSpace,menuRightController?rightMenuRay.origin:menuControllerRay.origin):StareHandAim.TryRead(true,rig.trackingSpace,out var leftMenuRay,out _)?leftMenuRay.origin:menuAnchor.position;
   view.UpdateWristMenu(StareShortcutMenu.Visible(InterfaceOpen,spectatorTools.Drawing,spectatorTools.CanThrow,WristMenuVisible,menuController,rightMenuTracked,view.DanceFromWrist)&&!broadcast,CanDance,Dancing,spectatorTools.CanThrow,menuPoint,rig.centerEyeAnchor);
   view.PositionDanceChoices(rig.centerEyeAnchor);
   view.UpdateLobbyAvailability(model!=null,localId>=0||roomBusy,State);
   view.UpdateRoomControls(roomBusy,localId>=0,roomCode,eyes.simulated,address,model!=null,State);
   bool handsOnly=!menuController&&!rightMenuTracked&&(hands.Any(h=>h.IsTracked)||StareHandAim.TryRead(true,rig.trackingSpace,out _,out _)||StareHandAim.TryRead(false,rig.trackingSpace,out _,out _));
   view.UpdateInputInstructions(eyes.Capability,eyes.simulated,handsOnly);
   view.UpdateDemoBlink(eyes.simulated&&!lobby&&!InterfaceOpen&&localId>=0&&(State.phase==(int)MatchPhase.Countdown||State.phase==(int)MatchPhase.Duel)&&State.fighters.Any(f=>f.id==localId&&f.alive&&f.pair>=0&&f.pair<State.pairs.Length&&State.pairs[f.pair].winner<0),rig.centerEyeAnchor);
   if(selectedAvatar==StareAvatarCatalog.Meta&&localId>=0)
   {
    var personal=view.MetaViewFor(localId);
    avatarNotice=personal==null||!personal.Loaded?"LOADING META AVATAR / FROST PREVIEW": "META AVATAR CONNECTED";
    if(personal!=null&&!string.IsNullOrEmpty(personal.Error))avatarNotice="META AVATAR UNAVAILABLE / USING FROST";
   }
   view.menu.gameObject.SetActive(lobby&&(localId<0||lobbyMenuOpen)&&!choosingAvatar);view.UpdateCharacterSelection(lobby&&choosingAvatar,selectedAvatar,avatarNotice);
   string micStatus=voice.MicrophoneSummary+"  "+new string('|',Mathf.RoundToInt(voice.MicrophoneLevel*10));
   view.SetMicrophoneStatus(micStatus);view.lobbyStatus.text=LobbySummary;view.SetSettingsStatus(eyes.Status.Split('\n')[0]+"\n"+micStatus+"\n"+voice.ReceiveSummary.Split('\n')[0]);view.ipText.text=string.IsNullOrEmpty(address)?"_ _ _ _":address;
   foreach(var id in pendingAvatarPoses.Keys.ToArray())
   {var avatar=view.MetaViewFor(id);if(avatar!=null){avatar.ApplyPose(pendingAvatarPoses[id]);pendingAvatarPoses.Remove(id);}else if(!State.fighters.Any(f=>f.id==id))pendingAvatarPoses.Remove(id);}
   if(Time.unscaledTime>=nextAvatarPose&&selectedAvatar==StareAvatarCatalog.Meta&&localId>=0)
   {
    nextAvatarPose=Time.unscaledTime+.1f;var avatar=view.MetaViewFor(localId);var pose=avatar?.CapturePose();
    if(pose!=null&&pose.Length>0&&pose.Length<=4096)
    {var message=new WireMessage{kind="avatarPose",id=localId,pose=Convert.ToBase64String(pose)};if(model!=null)net?.Broadcast(message);else net?.Send(0,message);}
   }
   Controls();
  }
  void SyncPlacement()
  {
   if(localId>=0&&State.fighters.Any(f=>f.id==localId))
   {
    bool entered=placedPlayerId!=localId||lastPhase!=State.phase;
    if(InLobby&&entered)
    {
     bool preserveMenu=placedPlayerId!=localId&&lobbyMenuOpen;
     PlaceLobbyEntry(ArenaLayout.Seat(State,localId),ArenaLayout.Facing(State,localId),preserveMenu);lastSeat=new Vector3(999,0,0);
     choosingAvatar=false;lobbyMenuOpen=placedPlayerId!=localId;matchMenuOpen=false;view.ShowJoinPanel(false);view.ShowSettingsPanel(false);menuPlaceAt=0;
     if(lobbyMenuOpen&&!preserveMenu)view.PlaceLobbyMenus(rig.centerEyeAnchor);
     if(lastPhase==(int)MatchPhase.Champion)notice="Match complete. You're back in the arena.";
    }
    else if(!InLobby)
    {
     var f=State.fighters.First(x=>x.id==localId);var seat=ArenaLayout.Seat(State,localId);
     if(f.alive?Vector3.Distance(seat,lastSeat)>.1f:lastLocalAlive||placedPlayerId!=localId){Place(seat,ArenaLayout.Facing(State,localId));lastSeat=seat;}
    }
    lastLocalAlive=State.fighters.First(x=>x.id==localId).alive;
    placedPlayerId=localId;lastPhase=State.phase;
   }
  }
  void PlaceLobbyEntry(Vector3 pos,Vector3 facing,bool preserveMenu)
  {
   if(!preserveMenu){Place(pos,facing);return;}
   // Move the already-open menu by the same rigid transform as the player.
   // Re-anchoring to the latest head pose here shifts the user's click target.
   var panels=new[]{view.menu,view.characterMenu};
   var positions=new Vector3[panels.Length];var rotations=new Quaternion[panels.Length];
   for(int i=0;i<panels.Length;i++)if(panels[i]!=null){positions[i]=rig.transform.InverseTransformPoint(panels[i].position);rotations[i]=Quaternion.Inverse(rig.transform.rotation)*panels[i].rotation;}
   Place(pos,facing);
   for(int i=0;i<panels.Length;i++)if(panels[i]!=null)panels[i].SetPositionAndRotation(rig.transform.TransformPoint(positions[i]),rig.transform.rotation*rotations[i]);
   Physics.SyncTransforms();
  }
  void Place(Vector3 pos,Vector3 facing)
  {
   requestedFloor=pos;requestedFacing=facing;
   var localEye=rig.transform.InverseTransformPoint(rig.centerEyeAnchor.position);
   var localForward=rig.transform.InverseTransformDirection(rig.centerEyeAnchor.forward);
   rig.transform.rotation=StareRigPlacement.Rotation(facing,localForward);
   rig.transform.position=StareRigPlacement.Position(pos,rig.transform.rotation,localEye);
  }
  void SeatLobby(){lastSeat=new Vector3(999,0,0);Place(new Vector3(0,0,-11),Vector3.forward);menuPlaceAt=Time.unscaledTime+.2f;}
  bool InterfaceOpen => view!=null&&(view.DanceChoicesVisible||view.SkinsVisible)|| (InLobby?(localId<0||lobbyMenuOpen||choosingAvatar):matchMenuOpen);
  bool WristMenuVisible => !broadcast&&!InterfaceOpen&&!spectatorTools.CanThrow&&!spectatorTools.Drawing&&(StareHandAim.TryRead(true,rig.trackingSpace,out _,out _)||(leftMenuHand!=null&&leftMenuHand.isActiveAndEnabled&&leftMenuHand.IsTracked&&leftMenuHand.HandConfidence==OVRHand.TrackingConfidence.High));
  void Controls()
  {
#if UNITY_EDITOR
   if(StareDesktopInput.KeyDown(KeyCode.F1))showDesktopShortcuts=!showDesktopShortcuts;
#endif
   if(StareDesktopInput.KeyDown(KeyCode.P))Act("demo");if(StareDesktopInput.KeyDown(KeyCode.H))Act("host");if(StareDesktopInput.KeyDown(KeyCode.J))Act("join");
   if(StareDesktopInput.KeyDown(KeyCode.Return))Act("start");if(StareDesktopInput.KeyDown(KeyCode.Escape))Act("leave");
   if(StareDesktopInput.KeyDown(KeyCode.M)||OVRInput.GetDown(OVRInput.Button.Start,OVRInput.Controller.LTouch))Act("match-menu");
   if(StareDesktopInput.KeyDown(KeyCode.C))Act("characters");
   if(State.phase==0&&choosingAvatar)for(int i=0;i<8;i++)if(StareDesktopInput.KeyDown(KeyCode.Alpha1+i))Act("avatar:"+StareAvatarCatalog.PresetId(i));
   if(StareDesktopInput.KeyDown(KeyCode.V)){broadcast=!broadcast;view.overview.enabled=broadcast;foreach(var c in rigCameras)c.enabled=!broadcast;foreach(var h in hands)h.gameObject.SetActive(!broadcast);}
   uiPointer.BeginFrame(!spectatorTools.Drawing,!broadcast&&!navigation.TeleportAiming&&!spectatorTools.Drawing);spectatorTools.BeginInputFrame();controllersTracked[0]=controllersTracked[1]=false;
   bool anyRay=false;int controllerCount=0,handCount=0;
   Camera cam=broadcast?view.overview:rig.centerEyeAnchor.GetComponent<Camera>();
   if(!Application.isMobilePlatform&&cam!=null&&StareDesktopInput.TryMousePosition(out var mouse)&&cam.pixelRect.Contains(mouse))
   {
    var target=uiPointer.Aim(cam.ScreenPointToRay(mouse));
    if(StareDesktopInput.MousePressed)PressUi(target);
   }
   for(int side=0;side<2;side++)
   {
    if(broadcast||!StareControllerAim.TryRead(side==0,rig.trackingSpace,out var controllerRay,out var pressed,out var held,out _,out _))continue;
    anyRay=true;controllerCount++;controllersTracked[side]=true;var target=uiPointer.Aim(controllerRay,true,pressed?220:20+side);
    if(pressed&&!spectatorTools.Drawing&&!navigation.TeleportAiming)PressUi(target,true);
    spectatorTools.SampleInput(side,held,true,StareControllerAim.GripPosition(side==0,rig.trackingSpace,controllerRay.origin),controllerRay,!InterfaceOpen&&(spectatorTools.Drawing||target==null)&&!navigation.TeleportAiming,true);
   }
   if(StareDesktopInput.KeyDown(KeyCode.Space)&&!InterfaceOpen)spectatorTools.TossFromHead();
   for(int i=0;i<hands.Length;i++)
   {
    var h=hands[i];Vector3 p=h.transform.position;
    bool isLeft=StareTrackedHands.IsLeft(h);
    bool nativeAim=StareHandAim.TryRead(isLeft,rig.trackingSpace,out var handRay,out var nativePinch);
    bool ovrAim=h.isActiveAndEnabled&&h.IsTracked&&h.IsPointerPoseValid&&!h.IsSystemGestureInProgress&&h.PointerPose!=null;
    bool physicalTracked;bool physicalDown=physicalGrabs[isLeft?0:1].Read(isLeft,rig.trackingSpace,h,out physicalTracked,out p);
    if(!physicalTracked)p=h.transform.position;
    var physicalRay=nativeAim?handRay:ovrAim?new Ray(h.PointerPose.position,h.PointerPose.forward):new Ray(p,Vector3.zero);
    float dt=Mathf.Max(.005f,Time.unscaledDeltaTime);velocity[i]=Vector3.Lerp(velocity[i],(p-previous[i])/dt,.5f);previous[i]=p;
    bool tracked=!broadcast&&(nativeAim||ovrAim);
    bool down=tracked&&(nativeAim?nativePinch:h.GetFingerIsPinching(OVRHand.HandFinger.Index));
    StareButton target=null;
    if(tracked)
    {
     anyRay=true;handCount++;
     if(!nativeAim)handRay=new Ray(h.PointerPose.position,h.PointerPose.forward);
     handRay=handStabilizers[i].Sample(handRay,down,nativeAim?StareHandAim.PinchStrength(isLeft):h.GetFingerPinchStrength(OVRHand.HandFinger.Index),Time.unscaledTime,Time.unscaledDeltaTime);
     target=uiPointer.Aim(handRay,true,down?310:40+i);
    }
    if(!tracked)handStabilizers[i].Reset();
    bool beginPinch=pinchGates[i].Sample(tracked,down);
    if(beginPinch)
    {
     pinchUsedForUi[i]=InterfaceOpen||target!=null;
     if(!spectatorTools.Drawing&&!navigation.TeleportAiming)PressUi(target);
    }
    if(!down&&pinched[i])
    {
     pinchUsedForUi[i]=false;
    }
    pinched[i]=down&&(pinched[i]||beginPinch);
    if(!controllersTracked[isLeft?0:1])spectatorTools.SampleInput(isLeft?0:1,physicalDown,physicalTracked&&!h.IsSystemGestureInProgress&&!StareHandAim.SystemGesture(isLeft),p,physicalRay,!InterfaceOpen&&(spectatorTools.CanThrow||!pinchUsedForUi[i]&&target==null)&&!navigation.TeleportAiming);
   }
   spectatorTools.TickInput();
   InputStatus="CONTROLLERS "+controllerCount+" / HANDS "+handCount+(spectatorTools.CanThrow?" / "+spectatorTools.InputDebug:anyRay?"":" / WAITING FOR HANDS OR CONTROLLERS");
   uiPointer.EndFrame();
  }
  bool PressUi(StareButton target,bool controller=false){if(!uiPointer.Press(target,controller))return false;if(Debug.isDebugBuild)Debug.Log("[STARE INPUT] selected="+target.action);if(target.action=="dance"){if(CanDance)view.ShowDanceChoices(rig.centerEyeAnchor,!view.DanceChoicesVisible||!view.DanceAttachedTo(target.transform),target.transform);}else Act(target.action);return true;}
  public void SendToss(Vector3 origin,Vector3 speed,int prop){if(model!=null)model.Throw(localId,prop,origin,speed);else net?.Send(0,new WireMessage{kind="throw",origin=origin,velocity=speed,prop=prop});}
#if UNITY_EDITOR
  void OnGUI()
  {
   if(!showDesktopShortcuts)return;
   if(shortcutStyle==null)shortcutStyle=new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,12,7,7),normal={textColor=new Color(.75f,.83f,.89f)}};
   const string shortcuts="C Characters   1–8 Select   P Practice   H Host   J Join   Enter Start   B Blink   T Tracking loss   Space Throw   M Menu   V Camera   Esc Lobby   F1 Hide";
   float width=Mathf.Min(1090,Screen.width-32);float height=shortcutStyle.CalcHeight(new GUIContent(shortcuts),width);
   var rect=new Rect((Screen.width-width)*.5f,Screen.height-height-14,width,height);
   Color saved=GUI.color;GUI.color=new Color(.025f,.045f,.065f,.90f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=saved;
   GUI.Label(rect,shortcuts,shortcutStyle);
  }
#endif
 }
}
