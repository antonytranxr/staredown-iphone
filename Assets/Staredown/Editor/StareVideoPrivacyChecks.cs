using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Staredown;
using UnityEngine;

public static class StareVideoPrivacyChecks
{
 public static string Run()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Video privacy: "+label);n++;};
  byte[] jpeg=StareCameraChecks.TestJpeg();
  Func<int,int,WireMessage> frame=(id,seq)=>new WireMessage{kind="cameraFrame",id=id,cameraSequence=seq,cameraFrame=Convert.ToBase64String(jpeg)};
  var root=new GameObject("Video sharing privacy checks");var stream=root.AddComponent<StareCameraStream>();
  try
  {
   var room=new Tournament();room.Add(0,"VR");room.Add(1,"Private phone");
   room.Input(0,0,0,true,true,0,0,0);room.Input(1,0,0,true,true,0,0,0,false,true);
   check(room.Start()&&room.Find(1).phoneTrackingRequired&&!room.Find(1).phoneCamera,"camera tracking identity works without video consent");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,room,null,0);
   check(room.Find(1).phoneCamera,"authenticated phone can opt into sharing during a match");
   stream.Handle(frame(1,1),1,room,null,0);check(stream.HasLiveFeed(1),"consenting phone frame is displayed");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=false},1,room,null,0);
   check(!room.Find(1).phoneCamera&&room.Find(1).phoneTrackingRequired&&room.Find(1).valid,"sharing off preserves live eye tracking and phone rules");
   check(stream.FeedCount==0&&!stream.HasCachedFeed(1),"privacy immediately releases remote face texture");
   int accepted=stream.AcceptedFrames;stream.Handle(frame(1,2),1,room,null,0);
   check(stream.AcceptedFrames==accepted,"late frames are rejected after consent is withdrawn");
   for(int i=0;i<25;i++){room.Input(0,0,0,true,true,0,0,0);room.Input(1,0,0,true,true,0,0,0,false,true);room.Tick(.05f);}
   check(room.Find(1).faceMissingFor==0&&!room.TrackingPaused,"video off never becomes missing face tracking");
   stream.Handle(new WireMessage{kind="cameraProfile",id=0,camera=true},1,room,null,0);check(!room.Find(0).phoneCamera,"sharing controls cannot impersonate another contestant");
   stream.Handle(new WireMessage{kind="cameraProfile",id=0,camera=true},0,room,null,0);check(!room.Find(0).phoneCamera,"non-phone cannot introduce a midmatch video stream");
   stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,room,null,0);stream.Handle(frame(1,1),1,room,null,0);
   check(stream.AcceptedFrames==accepted,"sharing resume keeps replay watermark");
   stream.Handle(frame(1,3),1,room,null,0);check(stream.AcceptedFrames==accepted+1,"resumed sharing accepts the next frame");

   var a=StareCameraPackets.Encode(frame(1,20));var b=StareCameraPackets.Encode(frame(2,20));var assembler=new StareCameraPackets.Assembler();WireMessage unused;
   assembler.Add(a.packets[0],0,out unused);assembler.Add(b.packets[0],0,out unused);check(assembler.Pending==2,"independent sources have independent partial frames");
   assembler.ClearOrigin(1);check(assembler.Pending==1,"privacy clears only that source's partial frame");
   foreach(byte[] part in b.packets.Skip(1))assembler.Add(part,.1,out unused);check(assembler.Pending==0&&unused!=null&&unused.id==2,"other players' frames still complete");
   using(var relay=new StareRelayNetwork())
   {
    var peerType=typeof(StareRelayNetwork).GetNestedType("Peer",BindingFlags.NonPublic);var peer=Activator.CreateInstance(peerType,true);
    var queue=typeof(StareRelayNetwork).GetMethod("Queue",BindingFlags.Instance|BindingFlags.NonPublic);
    Action<WireMessage> send=m=>queue.Invoke(relay,new object[]{5,peer,m,new byte[]{1},m.kind=="cameraFrame"?StareCameraPackets.Encode(m):null});
    send(frame(1,1));send(frame(2,1));var partial=(StareCameraPackets.Assembler)peerType.GetField("assembler").GetValue(peer);partial.Add(a.packets[0],0,out unused);
    send(new WireMessage{kind="cameraProfile",id=1,camera=false});
    var frames=(IDictionary)peerType.GetField("cameraFrames").GetValue(peer);var order=(IEnumerable)peerType.GetField("cameraOrder").GetValue(peer);var ids=order.Cast<int>().ToArray();
    check(frames.Count==1&&frames.Contains(2)&&!frames.Contains(1),"privacy removes unsent video while retaining other source");
    check(ids.SequenceEqual(new[]{2})&&partial.Pending==0,"privacy removes stale scheduling slots and partial buffers");
   }
   return n+" video sharing/authentication/eye tracking/queue privacy checks passed";
  }
  finally{stream.Reset();UnityEngine.Object.DestroyImmediate(root);}
 }
}
