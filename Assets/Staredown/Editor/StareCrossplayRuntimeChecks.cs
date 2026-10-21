using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Staredown;
public static class StareCrossplayRuntimeChecks
{
 const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
 static int count;static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Crossplay runtime: "+why);}
 static void Tick(object o){o.GetType().GetMethod("Update",Private).Invoke(o,null);}
 static Texture2D Read(RenderTexture rt){var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();RenderTexture.active=old;return image;}
 public static async Task<string> Mirror(string folder)
 {
  count=0;var p=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(p==null)throw new Exception("Mobile scene missing");
  var camera=p.ViewCamera;Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;bool enabled=p.enabled;
  try
  {
   p.Practice();p.MobileCamera.BeginTestCapture();p.MobileCamera.SubmitTestFrame(StareCameraChecks.TestJpeg());await Task.Delay(200);p.enabled=false;Tick(p);p.MobileCamera.SubmitTestFrame(StareCameraChecks.TestJpeg());p.CameraHeads.Sync(p.Visuals,p.State,p.LocalId);
   var local=p.Visuals.AvatarFor(p.LocalId);Check(local!=null&&local.root.gameObject.activeInHierarchy&&local.self!=null,"mobile own avatar active with a self viewer");
   Check(p.Visuals.LocalViewer==camera.transform,"mobile camera registered as local viewer");Check(local.cameraHead!=null&&local.cameraHead.Live&&local.cameraHead.Fresh&&local.cameraHead.FirstPersonHidden,"local video head exists and is hidden only from first person");
   Check((camera.cullingMask&(1<<StareCameraHead.FirstPersonLayer))==0,"main camera excludes its opaque camera housing");
   var mirror=p.Visuals.GetComponentInChildren<StareArenaMirror>();var position=mirror.transform.position+mirror.transform.forward*2.8f;position.y=1.6f;camera.transform.position=position;camera.transform.LookAt(mirror.transform.position);local.root.rotation=Quaternion.LookRotation(-camera.transform.forward);local.self.Align();
   mirror.Capture(camera);var feeds=(RenderTexture[])typeof(StareArenaMirror).GetField("feeds",Private).GetValue(mirror);var withSelf=Read(feeds[0]);
   local.root.gameObject.SetActive(false);mirror.Capture(camera);var without=Read(feeds[0]);local.root.gameObject.SetActive(true);
   int different=0;var a=withSelf.GetPixels32();var b=without.GetPixels32();for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>24)different++;
   System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"phone-mirror-local-avatar.png"),withSelf.EncodeToPNG());System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"phone-mirror-without-local-control.png"),without.EncodeToPNG());UnityEngine.Object.DestroyImmediate(withSelf);UnityEngine.Object.DestroyImmediate(without);
   Check(different>1500,"local avatar contributes actual mirror pixels: "+different);var capture=(Camera)typeof(StareArenaMirror).GetField("capture",Private).GetValue(mirror);Check((capture.cullingMask&(1<<StareCameraHead.FirstPersonLayer))!=0,"mirror includes local video head excluded from main view");
   Check(Shader.GetGlobalFloat("_StareExternalView")<.5f,"mirror restores first-person head clipping after capture");return count+" rendered mobile mirror checks passed; "+different+" avatar pixels differ from hidden-avatar control";
  }
  finally{p.enabled=enabled;p.MobileCamera.StopCamera();p.Leave();camera.transform.SetPositionAndRotation(oldPosition,oldRotation);}
 }
 public static async Task<string> PhoneClientToQuestProtocol()
 {
  count=0;var phone=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(phone==null)throw new Exception("Mobile scene missing");
  var host=new StareNetwork();var client=new StareNetwork();var t=new Tournament(1);t.Add(0,"Quest host");int peer=-1,events=0,acks=0;bool injected=false;long seq=1;double capture=Time.realtimeSinceStartupAsDouble;
  try
  {
   host.Host();client.Join("127.0.0.1");phone.TestConnect(client,false);phone.MobileCamera.BeginTestCapture();
   for(int step=0;step<260;step++)
   {
    host.Tick();StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);if(m.kind=="connected"){peer=packet.sender;t.Add(peer,"Phone client");host.Send(peer,new WireMessage{kind="welcome",id=peer,capturedBlinks=true});}
     else if(m.kind=="input")t.Input(packet.sender,m.left,m.right,m.valid,m.ready,m.blink,m.yaw,m.pitch,m.demo,m.capturedBlinks);
     else if(m.kind=="cameraProfile"){var f=t.Find(packet.sender);if(f!=null)f.phoneCamera=m.camera;}
     else if(m.kind=="blinkEvent"){events++;string result=t.AcceptCapturedBlink(packet.sender,m.blinkEvent);host.Send(packet.sender,new WireMessage{kind="blinkAck",blinkEvent=m.blinkEvent,error=result});if(result=="confirmed")acks++;}
    }
    capture+=.05;
    if(!injected)phone.MobileCamera.SubmitTestSample(0,0,true,capture,seq++);
    t.Input(0,0,0,true,true,0,0,0);
    if(t.Phase==MatchPhase.Lobby&&t.Fighters.Count==2&&t.Fighters.All(f=>f.valid&&f.ready)&&t.Find(peer).phoneCamera)t.Start();
    t.Tick(.05f);host.Broadcast(new WireMessage{kind="state",state=t.Snapshot()});
    if(!injected&&phone.State.phase==(int)MatchPhase.Duel)
    {
     await Task.Delay(75);capture+=.05;phone.MobileCamera.SubmitTestSample(1,1,true,capture,seq++);capture+=.04;phone.MobileCamera.SubmitTestSample(1,1,true,capture,seq++);capture+=.04;phone.MobileCamera.SubmitTestSample(0,0,false,capture,seq++);injected=true;
    }
    if(peer>=0&&!t.Find(peer).alive)break;
    await Task.Delay(40);
   }
   Check(peer>0&&injected,"real phone session joined host protocol and reached Go");Check(events>=1&&acks>=1,"host received and confirmed separately delivered captured blink");Check(!t.Find(peer).alive&&t.Find(peer).status.StartsWith("BLINKED"),"phone loses by blink despite invalid latest camera pose");Check(t.Find(0).alive,"Quest protocol opponent remains alive");return count+" actual TCP phone-client/Quest-host-protocol checks passed";
  }
  finally{phone.MobileCamera.StopCamera();phone.Leave();client.Dispose();host.Dispose();}
 }
}
