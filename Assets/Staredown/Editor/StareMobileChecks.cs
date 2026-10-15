using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Staredown;

public static class StareMobileChecks
{
 public static string Basics()
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
 public static async Task<string> CrossPlayRound()
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
