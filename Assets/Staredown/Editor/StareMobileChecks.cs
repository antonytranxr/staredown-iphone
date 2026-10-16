using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Staredown;

public static class StareMobileChecks
{
 sealed class NameFixture : IDisposable
 {
  const string Name="Staredown.PlayerName",Confirmed="Staredown.PlayerNameConfirmed";
  readonly bool hadName=PlayerPrefs.HasKey(Name),hadConfirmed=PlayerPrefs.HasKey(Confirmed);
  readonly string oldName=PlayerPrefs.GetString(Name,"");readonly int oldConfirmed=PlayerPrefs.GetInt(Confirmed,0);
  public NameFixture(){StarePlayerName.Save("Phone Fixture");}
  public void Dispose(){if(hadName)PlayerPrefs.SetString(Name,oldName);else PlayerPrefs.DeleteKey(Name);if(hadConfirmed)PlayerPrefs.SetInt(Confirmed,oldConfirmed);else PlayerPrefs.DeleteKey(Confirmed);PlayerPrefs.Save();}
 }
 public static string CameraContinuity()
 {
  int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mobile continuity: "+label);count++;};
  var blink=new StareMobileBlink();
  for(int i=0;i<=32;i++)blink.Sample(0,0,true,i*.05,i*.05);
  check(blink.Valid&&blink.Ready,"initial open-eye calibration completes");
  blink.Sample(0,0,false,1.65,1.65);check(!blink.Valid&&!blink.Ready,"missing face remains invalid for host enforcement");
  blink.Sample(0,0,true,1.7,1.7);check(blink.Valid&&blink.Ready,"face return preserves session calibration without resetting round");
  blink.Sample(1,1,true,1.75,1.75);blink.Sample(0,0,false,1.77,1.77);blink.Sample(1,1,true,1.79,1.79);
  check(blink.BlinkSequence==0,"invalid gap cannot bridge a blink debounce");
  blink.Sample(1,1,true,1.85,1.85);check(blink.BlinkSequence==1,"fresh closed samples after face return register exactly one blink");
  blink.Tick(2.2);check(!blink.Valid&&!blink.Ready,"stalled detector is invalid even if last eyes were valid");
  blink.Sample(0,0,true,2.21,2.21);check(blink.Ready,"stale recovery keeps successful calibration");
  blink.Reset();blink.Sample(0,0,true,2.3,2.3);check(!blink.Ready&&blink.BlinkSequence==1,"explicit reset requires calibration and retains monotonic blink sequence");
  check(StareMobileSession.RenderResolution(2556,1179)==new Vector2Int(1280,590),"high resolution phone renders at bounded aspect-preserving size");
  check(StareMobileSession.RenderResolution(800,600)==new Vector2Int(800,600),"smaller displays are never upscaled");
  check(StareMobileSession.RenderFrameRate==30,"phone renderer uses30fps independent of detector");
  var root=new GameObject("Face camera continuity fixture");var camera=root.AddComponent<StareMobileCamera>();int frames=0;camera.FrameReady+=bytes=>frames++;
  try
  {
   camera.BeginTestCapture();var jpeg=StareCameraChecks.TestJpeg();camera.SubmitTestFrame(jpeg);
   check(!camera.Valid&&camera.Running&&frames==1,"camera streams before face calibration");
   double time=Time.realtimeSinceStartupAsDouble;camera.SubmitTestSample(0,0,true,time,1);camera.SubmitTestSample(0,0,false,time+.05,2);camera.SubmitTestFrame(jpeg);
   check(!camera.Valid&&frames==2&&camera.Preview!=null,"missing face keeps live preview and network JPEG publication");
   camera.StopCamera();camera.SubmitTestFrame(jpeg);check(!camera.Running&&frames==2,"explicitcameraoffstopsallpublication");
  }
  finally{camera.StopCamera();UnityEngine.Object.DestroyImmediate(root);}
  return count+" mobile camera continuity/performance checks passed";
 }
 public static string Basics()
 {
  using(var name=new NameFixture())
  {
  int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mobile: "+label);count++;};
  var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(session==null)throw new Exception("Run the mobile scene first");
  check(session.ViewCamera!=null&&session.ViewCamera.stereoTargetEye==StereoTargetEyeMask.None,"ordinary monoscopic arena camera");
  check(UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>()==null,"phone scene contains no Quest camera rig");
  check(session.ViewCamera.GetComponent<AudioListener>()!=null,"phone audio listener");
  check(!session.MobileCamera.Running,"front camera never starts without consent");
  var camera=session.MobileCamera;camera.BeginTestCapture();double time=Time.realtimeSinceStartupAsDouble;long seq=1;
  for(int i=0;i<=32;i++)camera.SubmitTestSample(0,0,true,time+i*.05,seq++);
  check(camera.Valid&&camera.Ready,"fresh open-eye samples calibrate phone");
  int blinks=camera.BlinkSequence;camera.SubmitTestSample(1,1,true,time+1.65,seq++);camera.SubmitTestSample(1,1,true,time+1.72,seq++);
  check(camera.BlinkSequence==blinks+1,"one camera blink increments once");camera.SubmitTestSample(1,1,true,time+1.80,seq++);
  check(camera.BlinkSequence==blinks+1,"closed-eye hold never repeats blink");
  camera.SubmitTestSample(0,0,false,time+1.85,seq++);check(!camera.Valid&&!camera.Ready&&camera.BlinkSequence==blinks+1,"tracking loss cannot become a losing blink");
  camera.StopCamera();session.Practice();var hud=session.GetComponent<StareMobileHud>();var stick=hud.MovementControl;
  check(stick.Begin(11,Vector2.zero),"movement accepts its first finger");check(!stick.Begin(12,Vector2.zero),"second movement finger is rejected");stick.Move(12,new Vector2(59,0));check(session.MoveInput==Vector2.zero,"wrong finger cannot steer");stick.Move(11,new Vector2(59,0));check(session.MoveInput.x>.9f,"owned finger steers");stick.End(12);check(stick.PointerId==11,"wrong finger cannot release");stick.End(11);check(session.MoveInput==Vector2.zero,"released joystick stops movement");
  var look=hud.LookControl;check(look.Begin(22,Vector2.zero),"independent look finger starts");look.Move(23,new Vector2(100,0));check(session.LookInput==Vector2.zero,"wrong finger cannot turn");look.Move(22,new Vector2(100,0));check(session.LookInput.x>0,"owned swipe turns camera");look.End(22);check(session.LookInput==Vector2.zero,"look release clears deltas");
  var eliminated=new Fighter{id=0,alive=false,pair=0};var state=new ArenaState{phase=(int)MatchPhase.Countdown,remaining=3,fighters=new[]{eliminated},pairs=new[]{new Pairing{a=0,b=1}}};string caption=StareMobileHud.MatchCaption(state,eliminated,"");check(!caption.Contains("Eyes open")&&caption.Contains("You're out"),"spectators never see stare countdown");
  session.Leave();return count+" mobile scene/camera/touch checks passed";
  }
 }
 public static async Task<string> CrossPlayRound()
 {
  using(var name=new NameFixture())
  {
  var phone=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(phone==null)throw new Exception("Mobile scene missing");
  var peer=new StareRelayNetwork();long sequence=1;double capture=Time.realtimeSinceStartupAsDouble;int vr=-1;
  var jpeg=StareCameraChecks.TestJpeg();bool face=false,blink=false;int steps=0;
  Action sample=()=>{capture+=.05;phone.MobileCamera.SubmitTestSample(blink?1:0,blink?1:0,true,capture,sequence++);};
  async Task pumpUntil(Func<bool> done,int limit)
  {
   for(int i=0;i<limit;i++)
   {
    sample();peer.Tick();StareNetwork.Incoming packet;
    while(peer.Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);if(m.kind=="welcome")vr=m.id;
     if(m.kind=="cameraFrame"&&m.id==0)face=true;
    }
    if(vr>=0)peer.Send(0,new WireMessage{kind="input",left=0,right=0,valid=true,ready=true,blink=0,presence=new StarePresenceFrame{headTracked=true,head=Quaternion.identity}});
    if(phone.Connected&&phone.MobileCamera.Valid)phone.MobileCamera.SubmitTestFrame(jpeg);
    if(done())return;steps++;await Task.Delay(50);
   }
   throw new Exception("Cross-play step timed out: "+phone.Status+" / "+peer.Error+" / steps="+steps);
  }
  try
  {
   phone.Leave();phone.MobileCamera.BeginTestCapture();phone.Host();await pumpUntil(()=>phone.Connected&&!string.IsNullOrEmpty(phone.RoomCode),500);
   var join=peer.JoinAsync(phone.RoomCode);await pumpUntil(()=>join.IsCompleted,500);await join;
   await pumpUntil(()=>vr>0&&phone.State.fighters.Count(f=>!f.bot)==2&&phone.State.fighters.All(f=>f.ready&&f.valid),300);
   if(phone.State.fighters.Length!=Tournament.Capacity||!phone.State.fighters.First(f=>f.id==0).phoneCamera)throw new Exception("Phone profile/mixed roster missing");
   phone.StartMatch();await pumpUntil(()=>phone.State.phase==(int)MatchPhase.Duel&&face,200);
   int initial=phone.MobileCamera.BlinkSequence;blink=true;
   await pumpUntil(()=>!phone.State.fighters.First(f=>f.id==0).alive,200);
   if(phone.MobileCamera.BlinkSequence!=initial+1||!phone.CanMove)throw new Exception("Camera blink elimination or spectator movement failed");
   return "Live numeric Relay room: phone host + VR protocol peer +14bots; calibrated camera feed received; phone camera blink caused authoritative elimination; eliminated phone can roam";
  }
 finally{peer.Dispose();phone.Leave();phone.MobileCamera.StopCamera();}
  }
 }
 public static async Task<string> CrossPlayFaceLossRound()
 {
  using(var name=new NameFixture())
  {
  var phone=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(phone==null)throw new Exception("Run the mobile scene first");
  var peer=new StareRelayNetwork();var jpeg=StareCameraChecks.TestJpeg();long sequence=1;double capture=Time.realtimeSinceStartupAsDouble;
  int vr=-1,steps=0,frames=0,framesWhileAbsent=0,lastFrameSequence=0,sequenceBeforeLoss=0,roundBeforeLoss=-1,initialBlinks=0;bool faceVisible=true,losing=false;
  float lossClock=-1,eliminationClock=-1;ArenaState remoteState=null;
  async Task pumpUntil(Func<bool> done,int limit,string label)
  {
   for(int i=0;i<limit;i++)
   {
    capture+=.05;phone.MobileCamera.SubmitTestSample(0,0,faceVisible,capture,sequence++);
    peer.Tick();StareNetwork.Incoming packet;
    while(peer.Poll(out packet))
    {
     var message=JsonUtility.FromJson<WireMessage>(packet.json);if(message==null)continue;
     if(message.kind=="welcome")
     {
      vr=message.id;
      // The host must attribute the name to this connection, regardless of a
      // client-supplied id that points at the phone host.
      peer.Send(0,new WireMessage{kind="name",id=0,name="Quest Friend"});
     }
     if(message.kind=="state"&&message.state!=null)remoteState=message.state;
     if(message.kind=="cameraFrame"&&message.id==0)
     {
      byte[] decoded;if(!StareCameraFrameValidation.Decode(message,out decoded))throw new Exception("Invalid JPEG received by live Quest protocol peer");
      frames++;lastFrameSequence=Mathf.Max(lastFrameSequence,message.cameraSequence);
      var local=phone.State.fighters.FirstOrDefault(f=>f.id==0);
      if(losing&&local!=null&&!local.valid&&phone.State.clock-lossClock>=.3f&&message.cameraSequence>sequenceBeforeLoss)framesWhileAbsent++;
     }
    }
    if(vr>=0)peer.Send(0,new WireMessage{kind="input",left=0,right=0,valid=true,ready=true,blink=0,presence=new StarePresenceFrame{headTracked=true,head=Quaternion.identity}});
    if(phone.Connected&&phone.MobileCamera.Running)phone.MobileCamera.SubmitTestFrame(jpeg);
    if(losing)
    {
     if(phone.State.trackingPaused||phone.State.phase==(int)MatchPhase.Countdown||phone.State.round!=roundBeforeLoss)
      throw new Exception("Missing phone face paused or reset the live tournament");
     var local=phone.State.fighters.FirstOrDefault(f=>f.id==0);
     if(local!=null&&!local.alive&&eliminationClock<0)eliminationClock=phone.State.clock;
    }
    if(done())return;steps++;await Task.Delay(50);
   }
   throw new Exception("Face-loss cross-play timed out at "+label+": "+phone.Status+" / "+peer.Error+" / steps="+steps+" / absentFrames="+framesWhileAbsent);
  }
  try
  {
   phone.Leave();phone.SetPlayerName("Phone Fixture");phone.MobileCamera.BeginTestCapture();initialBlinks=phone.MobileCamera.BlinkSequence;phone.Host();
   await pumpUntil(()=>phone.Connected&&!string.IsNullOrEmpty(phone.RoomCode),500,"host");
   if(!StareRelayNetwork.ValidCode(phone.RoomCode)||phone.RoomCode.Length!=4)throw new Exception("Expected a live four-digit room code");
   var join=peer.JoinAsync(phone.RoomCode);await pumpUntil(()=>join.IsCompleted,500,"Relay join");await join;
   await pumpUntil(()=>vr>0&&phone.State.fighters.Count(f=>!f.bot)==2&&phone.State.fighters.All(f=>f.ready&&f.valid)&&phone.State.fighters.Any(f=>f.id==vr&&f.name=="Quest Friend"),300,"named calibrated roster");
   if(phone.State.fighters.Length!=Tournament.Capacity||phone.State.fighters.First(f=>f.id==0).name!="Phone Fixture"||!phone.State.fighters.First(f=>f.id==0).phoneCamera)
    throw new Exception("Live phone-host saved name, authenticated peer name, or camera profile missing");
   phone.StartMatch();await pumpUntil(()=>phone.State.phase==(int)MatchPhase.Duel&&frames>0,200,"active duel and live video");
   var pair=phone.State.pairs.First(p=>p.a==0||p.b==0);int opponent=pair.a==0?pair.b:pair.a;
   if(!phone.State.fighters.First(f=>f.id==opponent).bot||pair.botA<5||pair.botB<5)throw new Exception("Face-loss fixture requires a bot opponent with at least 5 seconds before blinking");
   lossClock=phone.State.clock;roundBeforeLoss=phone.State.round;sequenceBeforeLoss=lastFrameSequence;losing=true;faceVisible=false;
   await pumpUntil(()=>eliminationClock>=0&&framesWhileAbsent>=2&&remoteState!=null&&remoteState.fighters.Any(f=>f.id==0&&!f.alive),120,"face-loss forfeit and continuing remote video");
   float elapsed=eliminationClock-lossClock;
   if(elapsed<Tournament.PhoneFaceLossGrace-.08f||elapsed>Tournament.PhoneFaceLossGrace+.3f)throw new Exception("Face-loss forfeit timing was "+elapsed.ToString("F3")+" model seconds");
   var eliminated=phone.State.fighters.First(f=>f.id==0);
   if(eliminated.status!="FACE LOST / SPECTATOR"||!phone.CanMove||phone.MobileCamera.Valid||!phone.MobileCamera.Running||phone.MobileCamera.BlinkSequence!=initialBlinks)
    throw new Exception("Absent face did not resolve as a camera-running spectator forfeit");
   if(!remoteState.fighters.Any(f=>f.id==vr&&f.name=="Quest Friend")||!remoteState.fighters.Any(f=>f.id==0&&f.name=="Phone Fixture"))throw new Exception("Saved names did not reach the remote Quest state");
   return "Live four-digit Relay: saved phone-host name and sender-attributed Quest Friend name; phone face absent forfeited in "+elapsed.ToString("F2")+" model seconds with no tracking pause/countdown reset; "+framesWhileAbsent+" fresh JPEGs reached Quest while face absent; camera stayed live and spectator movement available";
  }
  finally
  {
   peer.Dispose();phone.Leave();phone.MobileCamera.StopCamera();
  }
  }
 }
 public static string Render(string path)
 {
  var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();var camera=session.ViewCamera;
  var canvas=session.GetComponentInChildren<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldScale=scaler.uiScaleMode;var oldTexture=camera.targetTexture;var previous=RenderTexture.active;
  var rt=new RenderTexture(1600,900,24);rt.Create();var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
  try
  {
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;canvas.scaleFactor=1;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();System.IO.File.WriteAllBytes(path,image.EncodeToPNG());return "Mobile arena + HUD rendered at1600×900";
  }
  finally{RenderTexture.active=previous;camera.targetTexture=oldTexture;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;scaler.uiScaleMode=oldScale;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);}
 }
}
