using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Staredown;

public static class StareCameraChecks
{
 sealed class CaptureTransport : IStareTransport
 {
  public readonly List<WireMessage> sent=new List<WireMessage>();
  public string Error=>null;
  public void Tick(){} public bool Poll(out StareNetwork.Incoming message){message=default;return false;}
  public void Send(int target,WireMessage message){sent.Add(message);}
  public void Broadcast(WireMessage message){sent.Add(message);}
  public void ClosePeer(int id){}public void Dispose(){}
 }
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception("Camera: "+label);checks++;}
 public static byte[] TestJpeg()
 {
  var texture=new Texture2D(160,160,TextureFormat.RGB24,false);var pixels=new Color[160*160];
  for(int y=0;y<160;y++)for(int x=0;x<160;x++)pixels[y*160+x]=new Color(x/159f,.25f,y/159f);
  texture.SetPixels(pixels);texture.Apply();var bytes=ImageConversion.EncodeToJPG(texture,40);UnityEngine.Object.DestroyImmediate(texture);return bytes;
 }
 static WireMessage Frame(int id,int sequence,byte[] bytes)=>new WireMessage{kind="cameraFrame",id=id,cameraSequence=sequence,cameraFrame=Convert.ToBase64String(bytes)};
 public static string ProtocolChecks()
 {
  checks=0;var jpeg=TestJpeg();int width,height;
  Check(StareCameraFrameValidation.Jpeg(jpeg,out width,out height)&&width==160&&height==160,"real 160px JPEG accepted within binary budget");
  byte[] decoded;Check(StareCameraFrameValidation.Decode(Frame(1,1,jpeg),out decoded)&&decoded.SequenceEqual(jpeg),"JPEG survives bounded JSON/Base64 framing");
  Check(!StareCameraFrameValidation.Decode(new WireMessage{kind="cameraFrame",id=1,cameraSequence=1,cameraFrame="not Base64"},out decoded),"invalid Base64 rejected before decoding image");
  Check(!StareCameraFrameValidation.Decode(Frame(1,1,new byte[5001]),out decoded),"oversize binary rejected");
  Check(!StareCameraFrameValidation.Decode(new WireMessage{kind="cameraFrame",id=1,cameraSequence=1,cameraFrame=new string('A',6669)},out decoded),"oversize encoded data rejected");
  var bomb=(byte[])jpeg.Clone();
  for(int i=0;i<bomb.Length-8;i++)if(bomb[i]==255&&(bomb[i+1]==192||bomb[i+1]==193||bomb[i+1]==194)){bomb[i+5]=4;bomb[i+6]=0;break;}
  Check(!StareCameraFrameValidation.Jpeg(bomb,out width,out height),"large dimension JPEG header rejected before Texture.LoadImage");
  Check(!StareCameraFrameValidation.Jpeg(new byte[]{137,80,78,71,13,10,26,10},out width,out height),"unexpected image format rejected");
  var padded=Frame(1,1,jpeg);padded.name=new string('X',8192);
  Check(!StareCameraFrameValidation.Decode(padded,out decoded),"unexpected JSON fields cannot exceed packet budget");
  var hostObject=new GameObject("Camera protocol host fixture");var stream=hostObject.AddComponent<StareCameraStream>();var net=new CaptureTransport();
  var t=new Tournament(1);t.Add(0,"HOST");t.Add(1,"PHONE");t.Add(2,"BOT",true);t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);
  try
  {
   stream.Handle(Frame(1,1,jpeg),1,t,net,0);Check(stream.AcceptedFrames==0,"frame without host-granted camera profile rejected");
   stream.Handle(new WireMessage{kind="cameraProfile",id=0,camera=true},1,t,net,0);Check(!t.Find(0).phoneCamera&&!t.Find(1).phoneCamera,"profile cannot spoof another contestant");
   stream.Handle(new WireMessage{kind="cameraProfile",id=2,camera=true},2,t,net,0);Check(!t.Find(2).phoneCamera,"bots cannot enable camera streams");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,t,net,0);Check(t.Find(1).phoneCamera&&net.sent.Last().id==1,"host authorizes originating connection profile");
   var copy=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(t.Snapshot()));Check(copy.fighters.First(f=>f.id==1).phoneCamera,"camera profile survives state snapshot");
   stream.Handle(Frame(0,1,jpeg),1,t,net,0);Check(stream.AcceptedFrames==0,"frame spoof rejection preserves host feed");
   stream.Handle(Frame(1,1,jpeg),1,t,net,0);Check(stream.AcceptedFrames==1&&stream.HasLiveFeed(1)&&net.sent.Last().id==1,"validated frame decoded and relayed under real connection id");
   stream.Handle(Frame(1,1,jpeg),1,t,net,0);Check(stream.AcceptedFrames==1,"duplicate frame sequence is ignored");
   stream.Handle(Frame(1,2,jpeg),1,t,net,0);stream.Handle(Frame(1,3,jpeg),1,t,net,0);Check(stream.AcceptedFrames==2,"burst-limited camera traffic cannot grow unbounded");
   t.Input(1,0,0,false,false,0,0,0);stream.Handle(Frame(1,4,jpeg),1,t,net,0);Check(stream.AcceptedFrames==2,"camera traffic remains burst-limited even when the face is absent");
   t.Input(1,0,0,true,true,0,0,0);t.Phase=MatchPhase.Duel;stream.Handle(new WireMessage{kind="cameraProfile",id=0,camera=true},0,t,net,0);Check(!t.Find(0).phoneCamera,"new stream profile cannot be introduced during tournament");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=false},1,t,net,0);Check(!t.Find(1).phoneCamera&&stream.FeedCount==0,"turning camera off removes texture and profile immediately");
   stream.Handle(new WireMessage{kind="cameraProfile",id=0,camera=true},1,t,net,0);Check(!t.Find(0).phoneCamera&&!t.Find(1).phoneCamera,"camera resume cannot override another player's identity");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,t,net,0);Check(t.Find(1).phoneCamera,"previously authorized phone can resume camera after background pause during match");
   stream.Reset();Check(stream.FeedCount==0&&stream.AcceptedFrames==0,"leaving room resets camera state");
   t.Find(1).phoneCamera=false;stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,t,net,0);Check(!t.Find(1).phoneCamera,"reset revokes previous camera capability during match");
   t.Phase=MatchPhase.Lobby;stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,t,net,0);stream.Sync(null,t.Snapshot(),1);
   stream.Handle(Frame(1,5,jpeg),2,null,net,1);Check(stream.AcceptedFrames==0,"client rejects camera frames from any sender other than host");
   stream.Handle(Frame(1,5,jpeg),0,null,net,1);Check(stream.AcceptedFrames==1,"client accepts host-relayed frame for authorized roster member");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=false},0,null,net,1);stream.Sync(null,t.Snapshot(),1);stream.Handle(Frame(1,6,jpeg),0,null,net,1);
   Check(stream.FeedCount==0&&stream.AcceptedFrames==1,"camera off overrides an older in-flight snapshot immediately");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},0,null,net,1);stream.Handle(Frame(1,5,jpeg),0,null,net,1);
   Check(stream.AcceptedFrames==1,"re-enabling camera retains sequence watermark and ignores delayed duplicate frame");
   stream.Handle(Frame(1,6,jpeg),0,null,net,1);Check(stream.AcceptedFrames==2,"resumed camera accepts next fresh sequence");
   stream.Sync(null,new ArenaState(),1);Check(stream.FeedCount==0&&!stream.HasLiveFeed(1),"departure from authoritative roster frees camera texture");
   t.Find(1).phoneCamera=false;t.Phase=MatchPhase.Duel;stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,t,net,0);Check(!t.Find(1).phoneCamera,"departure from roster revokes camera resume capability");t.Phase=MatchPhase.Lobby;
   t.Fighters.RemoveAll(f=>f.bot);t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);
   Check(t.Start(),"VR and phone roster shares authoritative match admission");
   for(int i=0;i<105;i++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);t.Tick(.05f);}
   t.Input(1,1,1,true,true,1,0,0);for(int i=0;i<3;i++)t.Tick(.05f);
   Check(!t.Find(1).alive&&t.Find(0).alive,"phone blink sequence eliminates phone player in shared tournament");
   return checks+" camera protocol checks passed";
  }
  finally{stream.Reset();UnityEngine.Object.DestroyImmediate(hostObject);}
 }

 public static string AvatarChecks()
 {
  checks=0;var root=new GameObject("Camera head render fixture");var view=root.AddComponent<StareVisuals>();var image=new Texture2D(160,160,TextureFormat.RGB24,false);
  try
  {
   // Obsidian is the phone client's preset body. Its arms and torso must remain
   // active while its video head follows the actual animation skeleton.
   var actor=view.CreateAvatar(2,null,root.transform);var body=actor.preset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
   var originallyEnabled=body.ToDictionary(renderer=>renderer,renderer=>renderer.enabled);
   var head=actor.root.gameObject.AddComponent<StareCameraHead>();head.Bind(actor);head.Present(image);
   Check(head.Live&&head.HiddenRenderers>0,"camera head hides separate original facial surfaces");
   Check(body.Length>0&&head.HiddenSkinnedRenderers==0&&body.All(renderer=>renderer.enabled==originallyEnabled[renderer]),"every merged skinned body/arm/leg surface retains its original visibility");
   var animator=actor.preset.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
   var main=body.OrderBy(renderer=>renderer.sharedMesh.vertexCount).Last();
   foreach(string clip in new[]{"Idle","Walk","Dance","Dance1","Dance2","Dance3"})
   {
    if(!animator.HasState(0,Animator.StringToHash(clip)))continue;
    for(int pose=0;pose<4;pose++)
    {
     animator.Play(clip,0,pose/4f);animator.Update(.016f);head.Present(image);
     Check(body.All(renderer=>renderer.enabled==originallyEnabled[renderer]),"all body surfaces remain rendered during "+clip+" pose "+pose);
     // BakeMesh has already applied skin/import scale. Compare native mesh
     // bounds at the same pose instead of applying the FBX transform twice.
     var baseline=new Mesh();head.Present(null);main.BakeMesh(baseline,false);
     var probe=new Mesh();head.Present(image);main.BakeMesh(probe,false);
     Check(probe.vertexCount==baseline.vertexCount&&Vector3.Distance(probe.bounds.center,baseline.bounds.center)<.0001f&&Vector3.Distance(probe.bounds.size,baseline.bounds.size)<.0001f,"camera head leaves animated body geometry unchanged in "+clip+" pose "+pose);
     Check(probe.bounds.size.y>.8f,"body geometry spans torso and legs in "+clip+" pose "+pose);
     UnityEngine.Object.DestroyImmediate(baseline);UnityEngine.Object.DestroyImmediate(probe);
    }
   }
   var bone=actor.preset.GetComponentsInChildren<Transform>(true).First(node=>node.name=="Head"||node.name=="head");
   var before=head.PortraitFocus;bone.rotation=Quaternion.AngleAxis(28,Vector3.up)*bone.rotation;head.Present(image);
   Check(Vector3.Distance(before,head.PortraitFocus)>.02f,"camera face follows animated head bone rotation");
   head.Present(null);Check(!head.Live&&body.All(renderer=>renderer.enabled==originallyEnabled[renderer]),"stale stream restores original avatar head and preserves all body surfaces");
   return checks+" camera avatar checks passed";
  }
  finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(image);}
 }

 public static async Task<string> TransportChecks(bool cloud=false)
 {
  checks=0;var host=new StareRelayNetwork();var clients=new[]{new StareRelayNetwork(),new StareRelayNetwork()};var ids=new[]{-1,-1};var jpeg=TestJpeg();
  try
  {
   if(cloud)
   {
    string code=await host.HostAsync();Check(StareRelayNetwork.ValidCode(code),"cross-platform live numeric room code");
    foreach(var client in clients){var join=client.JoinAsync(code);await Spin(host,clients,()=>join.IsCompleted,35);await join;}
   }
   else {host.TestLocal(true,47779);foreach(var client in clients)client.TestLocal(false,47779);}
   await Spin(host,clients,()=>
   {
    StareNetwork.Incoming p;while(host.Poll(out p)){var m=JsonUtility.FromJson<WireMessage>(p.json);if(m.kind=="connected")host.Send(p.sender,new WireMessage{kind="welcome",id=p.sender});}
    for(int i=0;i<clients.Length;i++)while(clients[i].Poll(out p)){var m=JsonUtility.FromJson<WireMessage>(p.json);if(m.kind=="welcome")ids[i]=m.id;}
    return ids.All(id=>id>0);
   },30);
   Check(ids[0]!=ids[1],"two camera-capable peers receive distinct authenticated ids");
   clients[0].Send(0,new WireMessage{kind="input",id=ids[0],blink=7,valid=true,ready=true});
   for(int sequence=1;sequence<=20;sequence++)clients[0].Send(0,Frame(ids[0],sequence,jpeg));
   Check(clients[0].CameraFramesReplaced==19,"twenty queued face previews coalesce to exactly one latest frame before network tick");
   bool input=false,frame=false;int frameCount=0,burst=1;float nextBurst=Time.realtimeSinceStartup+.2f,inputStarted=Time.realtimeSinceStartup;var seen=new HashSet<int>();
   await Spin(host,clients,()=>
   {
    StareNetwork.Incoming p;while(host.Poll(out p))
    {
     var m=JsonUtility.FromJson<WireMessage>(p.json);input|=m.kind=="input"&&m.blink==7&&p.sender==ids[0];
     if(m.kind=="cameraFrame")
     {
      byte[] bytes;Check(p.sender==ids[0]&&m.id==ids[0]&&m.cameraSequence>=20&&m.cameraSequence%20==0&&StareCameraFrameValidation.Decode(m,out bytes)&&bytes.SequenceEqual(jpeg),"only intact latest frame from each burst reaches authenticated origin");
      Check(seen.Add(m.cameraSequence),"unreliable lane never duplicates a preview sequence");frameCount++;frame=true;
     }
    }
    // A preview is deliberately unreliable. Emit new coalesced bursts at five
    // FPS until a complete frame arrives, while gameplay is sent just once.
    if(!(input&&frame)&&Time.realtimeSinceStartup>=nextBurst)
    {nextBurst=Time.realtimeSinceStartup+.2f;burst++;for(int n=(burst-1)*20+1;n<=burst*20;n++)clients[0].Send(0,Frame(ids[0],n,jpeg));}
    if(!input&&Time.realtimeSinceStartup-inputStarted>3)throw new Exception("Gameplay input delayed while unreliable previews stream. "+host.TransportDiagnostics+" / "+clients[0].TransportDiagnostics);
    return input&&frame;
   },15,"client camera and gameplay",()=>"input="+input+" frame="+frame+" received="+frameCount+" emitted bursts="+burst);
   Check(input&&frame&&frameCount<=burst,"single gameplay input and whole coalesced camera frame arrive without camera retransmission blocking gameplay");
   var t=new Tournament(1);for(int i=0;i<Tournament.Capacity;i++)t.Add(i,"CONTENDER "+i,true);
   host.Broadcast(new WireMessage{kind="state",state=t.Snapshot()});
   int originBase=burst*20+1;
   host.Broadcast(Frame(ids[0],originBase,jpeg));host.Broadcast(Frame(ids[1],12,jpeg));host.Broadcast(Frame(ids[0],originBase+1,jpeg));
   Check(host.CameraFramesReplaced>=2,"host preserves separate origin slots while replacing older frame for each target peer");
   var state=new bool[2];var originA=new bool[2];var originB=new bool[2];int round=0;float nextBroadcast=Time.realtimeSinceStartup+.2f,stateStarted=Time.realtimeSinceStartup;
   await Spin(host,clients,()=>
   {
    StareNetwork.Incoming p;for(int i=0;i<clients.Length;i++)while(clients[i].Poll(out p))
    {
     var m=JsonUtility.FromJson<WireMessage>(p.json);state[i]|=m.kind=="state"&&m.state!=null&&m.state.fighters.Length==Tournament.Capacity;
     if(m.kind=="cameraFrame")
     {
      byte[] bytes;Check(StareCameraFrameValidation.Decode(m,out bytes)&&bytes.SequenceEqual(jpeg),"relayed camera frame is whole and unchanged at peer "+i);
      if(m.id==ids[0]){Check(m.cameraSequence>=originBase+1&&m.cameraSequence%2==0,"only latest camera frame for origin A is relayed");originA[i]=true;}
      else {Check(m.id==ids[1]&&m.cameraSequence>=12,"independent origin B survives coalescing");originB[i]=true;}
     }
    }
    if(Time.realtimeSinceStartup>=nextBroadcast)
    {nextBroadcast=Time.realtimeSinceStartup+.2f;round++;host.Broadcast(Frame(ids[0],originBase+round*2,jpeg));host.Broadcast(Frame(ids[1],12+round,jpeg));host.Broadcast(Frame(ids[0],originBase+1+round*2,jpeg));}
    if(!state.All(v=>v)&&Time.realtimeSinceStartup-stateStarted>3)throw new Exception("Gameplay snapshot delayed while camera previews stream. "+host.TransportDiagnostics);
    return state.All(v=>v)&&originA.All(v=>v)&&originB.All(v=>v);
   },15,"host independent camera feeds",()=>"state="+string.Join(",",state)+" A="+string.Join(",",originA)+" B="+string.Join(",",originB));
   Check(state.All(v=>v)&&originA.All(v=>v)&&originB.All(v=>v),"16-player snapshot and independent latest camera feed from each origin reach both peers");
   clients[0].Send(0,new WireMessage{kind="cameraFrame",id=ids[0],cameraSequence=23,cameraFrame=new string('A',9000)});
   clients[0].Send(0,new WireMessage{kind="input",blink=8,valid=true,ready=true});bool laterInput=false;
   await Spin(host,clients,()=>{StareNetwork.Incoming p;while(host.Poll(out p)){var m=JsonUtility.FromJson<WireMessage>(p.json);laterInput|=m.kind=="input"&&m.blink==8;}return laterInput;},10);
   Check(laterInput,"oversize camera message is dropped without disconnecting gameplay");
   return checks+(cloud?" live Relay":" local UTP")+" camera transport checks passed";
  }
  finally{foreach(var client in clients)client.Dispose();host.Dispose();}
 }
 static async Task Spin(StareRelayNetwork host,StareRelayNetwork[] clients,Func<bool> done,float seconds,string stage="connection",Func<string> detail=null)
 {
  float until=Time.realtimeSinceStartup+seconds;
  while(Time.realtimeSinceStartup<until){host.Tick();foreach(var client in clients)client.Tick();if(done())return;await Task.Delay(15);}
  throw new Exception("Camera transport timed out during "+stage+". "+(detail!=null?detail():"")+" Host: "+host.TransportDiagnostics+" "+host.Error+" Clients: "+string.Join(" / ",clients.Select(c=>c.TransportDiagnostics+" "+c.Error)));
 }
}
