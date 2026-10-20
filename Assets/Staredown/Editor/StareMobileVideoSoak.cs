using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using Staredown;

// Uses the real renderer, JPEG decoder, camera stream and UTP fragmentation paths.
// Eye/camera source is synthetic: this cannot certify native ARKit or iPhone FPS.
public static class StareMobileVideoSoak
{
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 [Serializable] public class Sample {public string scenario;public int iteration,cachedAvatars,activeRoster,feeds,materials,meshes;public long unityAllocated;public float elapsed;}
 [Serializable] public class Result {public string name,status="running",detail;public int framesReceived,reliableReceived,staleDuringPause,resumedAfterPause;public float maxGap,p95FrameMs;}
 [Serializable] public class Report {public string status="running",source="Unity Editor / synthetic video / real local Unity Transport",version;public int seed=20261020,completed;public List<Sample> samples=new List<Sample>();public List<Result> results=new List<Result>();public List<string> runtimeErrors=new List<string>();public string limits="No physical iPhone camera, GPU/thermal, or audio validation";}
 public static bool Running {get;private set;}
 static bool cancel;static Report report;static string file;static double began;static StareMobileSession phone;
 static void Save(){System.IO.File.WriteAllText(file,JsonUtility.ToJson(report,true));}
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Log(string condition,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&report.runtimeErrors.Count<40)report.runtimeErrors.Add(condition);}
 static int Count(object owner,string field)=>((ICollection)owner.GetType().GetField(field,Hidden).GetValue(owner)).Count;
 static void Snapshot(string scenario,int iteration,int roster){report.samples.Add(new Sample{scenario=scenario,iteration=iteration,activeRoster=roster,cachedAvatars=Count(phone.Visuals,"avatars"),feeds=phone.CameraHeads.FeedCount,materials=Count(phone.Visuals,"materials"),meshes=Count(phone.Visuals,"artMeshes"),unityAllocated=Profiler.GetTotalAllocatedMemoryLong(),elapsed=(float)(Time.realtimeSinceStartupAsDouble-began)});Save();}
 static void Present(ArenaState state){typeof(StareMobileSession).GetProperty("State").SetValue(phone,state);typeof(StareMobileSession).GetProperty("LocalId").SetValue(phone,0);phone.Visuals.UpdateState(state,0,false);phone.CameraHeads.Sync(phone.Visuals,state,0);}
 public static void Cancel(){cancel=true;}
 public static async Task<string> Run(string output,int cycles=8,int seed=20261020)
 {
  if(Running)throw new Exception("A mobile soak already runs");if(cycles<1||cycles>20)throw new ArgumentOutOfRangeException("cycles");
  phone=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(phone==null||phone.Visuals==null)throw new Exception("Start the mobile Play scene first");
  Running=true;cancel=false;file=output;began=Time.realtimeSinceStartupAsDouble;report=new Report{version=Application.version,seed=seed};System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));bool enabled=phone.enabled;
  Application.logMessageReceived+=Log;
  try
  {
   phone.Leave();phone.MobileCamera.StopCamera();phone.enabled=false;Save();
   await Scenario("roster-and-video-lifecycle",()=>Churn(cycles));
   foreach(var shape in new[]{new[]{0,0,0},new[]{60,15,1},new[]{150,40,3}})
   {if(cancel)break;int[] config=shape;await Scenario("four-video-streams-delay"+config[0]+"-jitter"+config[1]+"-loss"+config[2],()=>Network(config[0],config[1],config[2],seed));}
   report.status=cancel?"cancelled":report.results.All(x=>x.status=="passed")&&report.runtimeErrors.Count==0?"passed":"failed";Save();
   return report.status+" / "+report.completed+" scenarios / "+file;
  }
  finally{phone.CameraHeads.Reset();phone.Leave();phone.enabled=enabled;Application.logMessageReceived-=Log;Running=false;Save();}
 }
 static async Task Scenario(string name,Func<Task> work)
 {
  var result=new Result{name=name};report.results.Add(result);Save();try{await work();result.status="passed";}catch(Exception e){result.status="failed";result.detail=e.ToString();}finally{phone.CameraHeads.Reset();phone.Visuals.UpdateState(new ArenaState(),-1,false);report.completed++;Save();await Task.Delay(100);}
 }
 static async Task Churn(int cycles)
 {
  var jpeg=StareCameraChecks.TestJpeg();int peak=0;
  for(int cycle=0;cycle<cycles&&!cancel;cycle++)
  {
   var t=new Tournament();t.Add(0,"Local camera");for(int i=0;i<3;i++)t.Add(100+cycle*3+i,"Remote camera "+i);t.EnableBotFill();
   foreach(var f in t.Fighters.Where(x=>!x.bot)){f.avatar=2;f.phoneCamera=true;f.valid=f.ready=true;}
   var state=t.Snapshot();Present(state);await Task.Delay(250);Present(state);
   foreach(var f in state.fighters.Where(x=>!x.bot))phone.CameraHeads.Handle(new WireMessage{kind="cameraFrame",id=f.id,cameraSequence=cycle+1,cameraFrame=Convert.ToBase64String(jpeg)},0,null,null,0);
   phone.CameraHeads.Sync(phone.Visuals,state,0);await Task.Delay(70);peak=Math.Max(peak,Count(phone.Visuals,"avatars"));Snapshot("roster",cycle,state.fighters.Length);
  }
  Present(new ArenaState());phone.CameraHeads.Reset();await Task.Delay(200);Snapshot("after-leave",cycles,0);
  var result=report.results.Last();result.detail="peak cached="+peak+"; after leave="+Count(phone.Visuals,"avatars");
  Require(peak<=Tournament.Capacity,"Departed contestants accumulate: "+result.detail);Require(Count(phone.Visuals,"avatars")==0,"Leaving retains unused contestants");Require(phone.CameraHeads.FeedCount==0,"Camera textures remain after leave");
 }
 static async Task Network(int delay,int jitter,int loss,int seed)
 {
  const ushort port=48991;var root=new GameObject("Video soak network clients");var host=new StareRelayNetwork();var peers=new StareRelayNetwork[3];var streams=new StareCameraStream[3];var ids=new int[3];
  var t=new Tournament();t.Add(0,"Phone host");var jpeg=StareCameraChecks.TestJpeg();int[] sequence=new int[4];double[,] last=new double[3,4];var gaps=new List<float>();var frames=new List<float>();int reliable=0,received=0,stale=0,resumed=0;double start=Time.realtimeSinceStartupAsDouble,nextVideo=0,nextState=0,nextSample=0,prior=start;bool paused=false;var result=report.results.Last();
  try
  {
   host.TestLocal(true,port,delay,jitter,loss,(uint)seed);for(int i=0;i<3;i++){peers[i]=new StareRelayNetwork();peers[i].TestLocal(false,port,delay,jitter,loss,(uint)(seed+i+1));streams[i]=root.AddComponent<StareCameraStream>();}
   while(Time.realtimeSinceStartupAsDouble-start<12&&!cancel)
   {
    double now=Time.realtimeSinceStartupAsDouble,elapsed=now-start;frames.Add((float)((now-prior)*1000));prior=now;
    host.Tick();StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);
     if(m.kind=="connected"){var f=t.Add(packet.sender,"Phone "+packet.sender);if(f!=null){f.avatar=2;f.phoneCamera=true;f.ready=f.valid=true;}host.Send(packet.sender,new WireMessage{kind="welcome",id=packet.sender});}
     else phone.CameraHeads.Handle(m,packet.sender,t,host,0);
    }
    var state=t.Snapshot();state.fighters[0].phoneCamera=true;Present(state);
    for(int i=0;i<3;i++)
    {
     peers[i].Tick();streams[i].Sync(null,state,ids[i]);
     while(peers[i].Poll(out packet))
     {
      var m=JsonUtility.FromJson<WireMessage>(packet.json);if(m.kind=="welcome")ids[i]=m.id;
      if(m.kind=="state")reliable++;
      if(m.kind=="cameraFrame")
      {received++;if(last[i,m.id]>0){double overlap=Math.Max(0,Math.Min(now,start+6.2)-Math.Max(last[i,m.id],start+4));gaps.Add((float)(now-last[i,m.id]-overlap));}last[i,m.id]=now;streams[i].Handle(m,0,null,null,ids[i]);}
     }
     if(ids[i]>0)peers[i].Send(0,new WireMessage{kind="input",valid=true,ready=true});
    }
    paused=elapsed>=4&&elapsed<6.2;
    if(now>=nextVideo&&t.Fighters.Count==4)
    {
     nextVideo=now+1.0/6;
     if(!paused)
     {
      phone.CameraHeads.Publish(jpeg,0,t,host);
      for(int i=0;i<3;i++)if(ids[i]>0)peers[i].Send(0,new WireMessage{kind="cameraFrame",id=ids[i],cameraSequence=++sequence[i],cameraFrame=Convert.ToBase64String(jpeg)});
     }
    }
    if(now>=nextState){nextState=now+.05;host.Broadcast(new WireMessage{kind="state",state=state});}
    if(elapsed>=5.7&&elapsed<6.2&&phone.CameraHeads.FeedIsStale(1)&&phone.CameraHeads.HasCachedFeed(1))stale++;
    if(elapsed>7&&phone.CameraHeads.HasLiveFeed(1))resumed++;
    if(now>=nextSample){nextSample=now+1;Snapshot(result.name,(int)elapsed,state.fighters.Length);}
    await Task.Delay(15);
   }
   result.framesReceived=received;result.reliableReceived=reliable;result.staleDuringPause=stale;result.resumedAfterPause=resumed;result.maxGap=gaps.Count>0?gaps.Max():0;var sorted=frames.OrderBy(x=>x).ToArray();result.p95FrameMs=sorted.Length>0?sorted[(int)((sorted.Length-1)*.95f)]:0;
   result.detail=host.TransportDiagnostics+" / max video gap excluding planned pause="+result.maxGap.ToString("F2")+"s";
   Require(ids.All(x=>x>0),"All three peers must join");Require(received>=200&&reliable>=30,"Video or snapshots stalled: "+result.detail);Require(result.maxGap<1.5f,"Unplanned video freeze exceeds 1.5 seconds: "+result.detail);Require(host.PacketVideoPeers==3&&peers.All(x=>x.PacketVideoPeers==1),"All peers must negotiate reorder-safe video");Require(resumed>0,"Video did not recover after planned camera stall");Require(phone.CameraHeads.FeedCount==4,"Expected four bounded host video textures");
  }
  finally{foreach(var peer in peers)peer?.Dispose();host.Dispose();phone.CameraHeads.Reset();UnityEngine.Object.Destroy(root);}
 }
}
