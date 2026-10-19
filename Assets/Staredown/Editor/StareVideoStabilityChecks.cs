using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Staredown;

public static class StareVideoStabilityChecks
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class CaptureTransport:IStareTransport
 {
  public readonly List<WireMessage> sent=new List<WireMessage>();public string Error=>null;
  public void Tick(){}public bool Poll(out StareNetwork.Incoming packet){packet=default;return false;}
  public void Send(int id,WireMessage message){sent.Add(message);}public void Broadcast(WireMessage message){sent.Add(message);}public void ClosePeer(int id){}public void Dispose(){}
 }
 static WireMessage Frame(int sequence,byte[] jpeg)=>new WireMessage{kind="cameraFrame",id=1,cameraSequence=sequence,cameraFrame=Convert.ToBase64String(jpeg)};
 static object Feed(StareCameraStream stream,int id){return ((IDictionary)typeof(StareCameraStream).GetField("feeds",Private).GetValue(stream))[id];}
 static void Age(StareCameraStream stream,int id,float age){var feed=Feed(stream,id);feed.GetType().GetField("received").SetValue(feed,Time.unscaledTime-age);}
 public static async Task<string> Run()
 {
  int count=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Stable video: "+message);count++;};
  var root=new GameObject("Stable camera video checks");var preview=new Texture2D(160,160,TextureFormat.RGB24,false);var another=new Texture2D(160,160,TextureFormat.RGB24,false);
  var jpeg=StareCameraChecks.TestJpeg();ImageConversion.LoadImage(preview,jpeg,false);ImageConversion.LoadImage(another,jpeg,false);
  try
  {
   var view=root.AddComponent<StareVisuals>();var avatar=view.CreateAvatar(2,null,root.transform);
   var avatars=(Dictionary<int,CreatureView>)typeof(StareVisuals).GetField("avatars",Private).GetValue(view);avatars.Add(1,avatar);
   var stream=root.AddComponent<StareCameraStream>();var transport=new CaptureTransport();var room=new Tournament();room.Add(0,"HOST");room.Add(1,"PHONE");room.Input(0,0,0,true,true,0,0,0);room.Input(1,0,0,false,false,0,0,0);
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,room,transport,0);stream.Sync(view,room.Snapshot(),0);
   var head=avatar.cameraHead;check(head!=null&&head.Live&&head.Stale&&head.Status=="CONNECTING","explicit consent displays stable connecting head before first image");
   stream.Handle(Frame(1,jpeg),1,room,transport,0);stream.Sync(view,room.Snapshot(),0);
   check(stream.HasLiveFeed(1)&&stream.HasCachedFeed(1)&&head.Fresh&&head.Status=="LIVE","fresh image becomes live without replacing housing");
   check(stream.DecodedFrames==1&&stream.ReusedPreviewFrames==0,"remote video uses the validated decode path");
   var cached=(Texture2D)Feed(stream,1).GetType().GetField("texture").GetValue(Feed(stream,1));
   Age(stream,1,StareCameraFrameValidation.FreshSeconds+.5f);stream.Sync(view,room.Snapshot(),0);
   check(!stream.HasLiveFeed(1)&&stream.HasCachedFeed(1)&&stream.FeedIsStale(1),"freshness expires without deleting cached texture");
   check(head==avatar.cameraHead&&head.Live&&head.Stale&&head.Status=="VIDEO PAUSED","jitter retains housing and clearly marks frozen video");
   check(stream.FeedFramesPerSecond(1)==0&&stream.DiagnosticsSummary.Contains("1 STALE"),"diagnostics cannot report stale video as live FPS");
   var same=(Texture2D)Feed(stream,1).GetType().GetField("texture").GetValue(Feed(stream,1));check(same==cached,"stale transition does not churn GPU texture");
   stream.Handle(Frame(2,jpeg),1,room,transport,0);stream.Sync(view,room.Snapshot(),0);
   check(head==avatar.cameraHead&&head.Fresh&&!head.Stale&&stream.HasLiveFeed(1),"next fresh image resumes the same camera head");
   int accepted=stream.AcceptedFrames;stream.Handle(Frame(1,jpeg),1,room,transport,0);var spoof=Frame(3,jpeg);spoof.id=0;stream.Handle(spoof,1,room,transport,0);
   check(stream.AcceptedFrames==accepted,"stability change preserves stale-sequence and identity rejection");
   stream.Handle(Frame(3,jpeg),1,room,transport,0);check(stream.AcceptedFrames==accepted,"stability change preserves host frame-rate token budget");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=false},1,room,transport,0);
   check(!head.Live&&!stream.HasCachedFeed(1)&&stream.FeedCount==0,"explicit camera off restores real avatar immediately");
   stream.Sync(view,room.Snapshot(),0);check(!head.Live,"camera-off state cannot resurrect the cached head");

   var local=new GameObject("Borrowed camera preview");local.transform.SetParent(root.transform);var client=local.AddComponent<StareCameraStream>();room.Find(1).phoneCamera=true;client.Sync(null,room.Snapshot(),1);client.Publish(jpeg,1,null,transport,preview);
   check(client.ReusedPreviewFrames==1&&client.DecodedFrames==0&&client.HasLiveFeed(1),"local canonical preview avoids a second JPEG decode");
   var borrowed=(Texture2D)Feed(client,1).GetType().GetField("texture").GetValue(Feed(client,1));check(borrowed==preview,"local video borrows the exact canonical texture");
   client.Handle(new WireMessage{kind="cameraProfile",id=1,camera=false},0,null,transport,1);
   await Task.Delay(60);check(preview!=null&&!client.HasCachedFeed(1),"camera-off removes borrowed feed without destroying camera-owned preview");
   client.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},0,null,transport,1);client.Sync(null,room.Snapshot(),1);
   await Task.Delay(190);client.Publish(jpeg,1,null,transport,preview);client.Reset();
   await Task.Delay(60);check(preview!=null&&client.FeedCount==0,"room Reset never destroys camera-owned preview");
   client.Sync(null,room.Snapshot(),1);client.Publish(jpeg,1,null,transport,another);UnityEngine.Object.DestroyImmediate(another);another=null;
   client.Sync(null,room.Snapshot(),1);check(!client.HasCachedFeed(1)&&!client.HasLiveFeed(1),"camera owner releasing a borrowed texture is safe");client.Reset();
   client.Sync(null,room.Snapshot(),1);client.Publish(jpeg,1,null,transport,preview);room.Find(1).disconnected=true;client.Sync(null,room.Snapshot(),1);
   await Task.Delay(60);check(preview!=null&&client.FeedCount==0&&!client.HasCachedFeed(1)&&client.DiagnosticsSummary=="VIDEO OFF","disconnect clears profile/cache immediately without touching borrowed preview");
   check(StareArenaMirror.TargetRefreshRate(true,0,false)==6&&StareArenaMirror.TargetRefreshRate(true,2,false)==3&&StareArenaMirror.TargetRefreshRate(true,0,true)==3,"phone mirror gives detection priority at6Hz /3Hz under pressure");
   check(StareArenaMirror.TargetRefreshRate(false,0,false)==12&&StareArenaMirror.TargetRefreshRate(false,0,true)==8,"Quest mirror remains12Hz with8Hz fallback");
   return count+" video stability / preview ownership / phone mirror checks passed";
  }
  finally{if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(preview!=null)UnityEngine.Object.DestroyImmediate(preview);if(another!=null)UnityEngine.Object.DestroyImmediate(another);}
 }
}
