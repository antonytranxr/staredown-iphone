using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Staredown;

// Focused regression checks for camera-based contestants. Live video consent is
// independent from face tracking: video keeps streaming while a missing face
// forfeits an active stare, rather than pausing everybody else's contest.
public static class StareRoomPhoneChecks
{
 static int checks;
 static void Check(bool condition,string label)
 {if(!condition)throw new Exception("Phone room: "+label);checks++;}

 static Tournament ReadyRoom(int count,params int[] phones)
 {
  var tournament=new Tournament();
  for(int id=0;id<count;id++)
  {var fighter=tournament.Add(id,"HUMAN "+id);fighter.phoneCamera=phones.Contains(id);tournament.Input(id,0,0,true,true,0,0,0);}
  Check(tournament.Start(),"calibrated humans enter a match");
  return tournament;
 }
 static void Fresh(Tournament tournament,int[] missing=null,int[] stale=null)
 {
  foreach(var fighter in tournament.Fighters)
  {
   if(fighter.bot||stale!=null&&stale.Contains(fighter.id))continue;
   bool valid=missing==null||!missing.Contains(fighter.id);
   tournament.Input(fighter.id,0,0,valid,true,fighter.blinkSequence,0,0);
  }
 }
 static void Step(Tournament tournament,float seconds,int[] missing=null,int[] stale=null)
 {
  // Keep samples fresh against the model's own clock, not Unity's wall clock.
  for(float elapsed=0;elapsed<seconds-.00001f;)
  {float dt=Mathf.Min(.025f,seconds-elapsed);Fresh(tournament,missing,stale);tournament.Tick(dt);elapsed+=dt;}
 }
 static void ReachDuel(Tournament tournament)
 {
  for(int i=0;i<250&&tournament.Phase==MatchPhase.Countdown;i++)
  {Fresh(tournament);tournament.Tick(.025f);}
  Check(tournament.Phase==MatchPhase.Duel,"fresh countdown reaches active stare");
 }

 public static string RulesChecks()
 {
  checks=0;
  var countdown=ReadyRoom(2,0);
  Check(countdown.Find(0).phoneTrackingRequired&&!countdown.Find(1).phoneTrackingRequired,"camera requirement is latched when the round starts");
  float countdownBefore=countdown.Remaining;Step(countdown,.4f,new[]{0});
  Check(countdown.TrackingPaused&&Mathf.Abs(countdown.Remaining-countdownBefore)<.001f,"missing face pauses pre-Go countdown");
  Check(countdown.Fighters.All(fighter=>fighter.alive)&&countdown.Pairs[0].winner<0,"pre-Go face absence does not eliminate anyone");
  ReachDuel(countdown);int originalRound=countdown.Round;float before=countdown.Remaining;
  Step(countdown,Tournament.PhoneFaceLossGrace*.4f,new[]{0});
  Check(!countdown.TrackingPaused&&countdown.Remaining<before&&countdown.Find(0).alive,"brief active phone face absence advances the contest clock");
  Step(countdown,Tournament.PhoneFaceLossGrace*.8f,new[]{0});
  Check(!countdown.Find(0).alive&&countdown.Find(1).alive&&countdown.Pairs[0].winner==1,"missing face beyond grace forfeits the pair");
  Check(countdown.Find(0).status=="FACE LOST / SPECTATOR"&&!countdown.TrackingPaused&&countdown.Round==originalRound&&countdown.Phase==MatchPhase.Intermission,"forfeit advances without countdown reset or global pause");

  var recovery=ReadyRoom(2,0);ReachDuel(recovery);
  Step(recovery,Tournament.PhoneFaceLossGrace*.6f,new[]{0});Step(recovery,.1f);
  Check(recovery.Find(0).alive&&recovery.Find(0).faceMissingFor==0,"face reacquisition clears only this contestant's missing-face timer");
  Step(recovery,Tournament.PhoneFaceLossGrace*.6f,new[]{0});
  Check(recovery.Find(0).alive&&recovery.Phase==MatchPhase.Duel&&!recovery.TrackingPaused,"separate brief interruptions do not accumulate into a forfeit");

  var off=ReadyRoom(2,0);ReachDuel(off);off.Find(0).phoneCamera=false;
  Step(off,Tournament.PhoneFaceLossGrace+.2f);
  Check(off.Find(0).phoneTrackingRequired&&!off.Find(0).alive&&off.Pairs[0].winner==1,"turning video off mid-round cannot evade the latched camera requirement");

  var stale=ReadyRoom(2,0);ReachDuel(stale);
  Step(stale,1+Tournament.PhoneFaceLossGrace+.25f,null,new[]{0});
  Check(!stale.Find(0).alive&&stale.Find(1).alive&&!stale.TrackingPaused,"stale phone input becomes invalid and eventually forfeits");

  var vr=ReadyRoom(2);ReachDuel(vr);float vrBefore=vr.Remaining;
  Step(vr,Tournament.PhoneFaceLossGrace+.2f,new[]{0});
  Check(vr.TrackingPaused&&vr.Fighters.All(fighter=>fighter.alive)&&Mathf.Abs(vr.Remaining-vrBefore)<.001f,"ordinary headset tracking loss retains pause behavior");
  Step(vr,.1f);Check(!vr.TrackingPaused&&vr.Phase==MatchPhase.Duel,"headset tracking recovery resumes the same stare");

  var blinkFirst=ReadyRoom(2,0);ReachDuel(blinkFirst);
  Step(blinkFirst,Tournament.PhoneFaceLossGrace-.05f,new[]{0});
  blinkFirst.Blink(1);Fresh(blinkFirst,new[]{0});blinkFirst.Tick(.1f);
  Check(!blinkFirst.Find(1).alive&&blinkFirst.Find(0).alive&&blinkFirst.Pairs[0].winner==0,"earlier pending blink resolves before a later face-forfeit threshold");
  Check(blinkFirst.Find(1).status=="BLINKED / SPECTATOR","earliest losing event retains its blink reason");

  var simultaneous=ReadyRoom(2,0,1);ReachDuel(simultaneous);int first=simultaneous.Pairs[0].a,second=simultaneous.Pairs[0].b;
  Step(simultaneous,Tournament.PhoneFaceLossGrace+.2f,new[]{0,1});
  Check(!simultaneous.Find(first).alive&&simultaneous.Find(second).alive&&simultaneous.Pairs[0].winner==second,"simultaneous missing faces resolve deterministically");
  Check(simultaneous.Phase==MatchPhase.Intermission&&!simultaneous.TrackingPaused,"simultaneous missing faces never restart or freeze the round");

  var parallel=ReadyRoom(4,0);ReachDuel(parallel);
  var affected=parallel.Pairs.First(pair=>pair.a==0||pair.b==0);var other=parallel.Pairs.First(pair=>pair.a!=0&&pair.b!=0);
  Step(parallel,.25f,new[]{0});float otherBefore=parallel.Remaining;parallel.Blink(other.a);Step(parallel,.15f,new[]{0});
  Check(!parallel.TrackingPaused&&parallel.Remaining<otherBefore,"missing phone face cannot freeze another active pair");
  Check(other.winner==other.b&&!parallel.Find(other.a).alive&&affected.winner<0,"unrelated pair resolves normally while phone face grace runs");
  Step(parallel,Tournament.PhoneFaceLossGrace,new[]{0});
  Check(affected.winner>=0&&affected.winner!=0&&!parallel.Find(0).alive,"affected pair subsequently forfeits independently");

  var closure=ReadyRoom(2,0);ReachDuel(closure);
  closure.Input(0,1,1,true,false,0,0,0);closure.Input(1,0,0,true,true,0,0,0);closure.Tick(.05f);
  closure.Input(0,1,1,true,false,0,0,0);closure.Input(1,0,0,true,true,0,0,0);closure.Tick(.1f);
  Check(!closure.Find(0).alive&&closure.Pairs[0].winner==1,"server still judges phone eyelid closure during local recalibration");
  return checks+" phone room rules checks passed";
 }

 sealed class CaptureTransport : IStareTransport
 {
  public readonly List<WireMessage> Sent=new List<WireMessage>();public string Error=>null;
  public void Tick(){}public bool Poll(out StareNetwork.Incoming message){message=default;return false;}
  public void Send(int target,WireMessage message){Sent.Add(message);}public void Broadcast(WireMessage message){Sent.Add(message);}
  public void ClosePeer(int id){}public void Dispose(){}
 }
 sealed class StreamFixture : IDisposable
 {
  public readonly GameObject Root;public readonly StareCameraStream Stream;public readonly CaptureTransport Transport=new CaptureTransport();
  public StreamFixture(string name){Root=new GameObject(name);Stream=Root.AddComponent<StareCameraStream>();}
  public void Dispose(){Stream.Reset();UnityEngine.Object.DestroyImmediate(Root);}
 }
 static WireMessage Frame(int id,int sequence,byte[] bytes)=>new WireMessage{kind="cameraFrame",id=id,cameraSequence=sequence,cameraFrame=Convert.ToBase64String(bytes)};
 static Tournament CameraRoom()
 {
  var room=new Tournament();room.Add(0,"HOST");room.Add(1,"PHONE");room.Input(0,0,0,true,true,0,0,0);room.Input(1,0,0,false,false,0,0,0);return room;
 }
 static void Authorize(StreamFixture fixture,Tournament room)
 {fixture.Stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=true},1,room,fixture.Transport,0);}

 public static string VideoChecks()
 {
  checks=0;byte[] jpeg=StareCameraChecks.TestJpeg();
  using(var host=new StreamFixture("Phone face-absent host video fixture"))
  {
   var room=CameraRoom();Authorize(host,room);
   Check(room.Find(1).phoneCamera&&!room.Find(1).valid,"phone has video consent while face tracking is invalid");
   host.Stream.Handle(Frame(1,1,jpeg),1,room,host.Transport,0);
   Check(host.Stream.AcceptedFrames==1&&host.Stream.HasLiveFeed(1),"host accepts live frame with absent face");
   Check(host.Transport.Sent.Last().kind=="cameraFrame"&&host.Transport.Sent.Last().id==1,"host relays face-absent frame under authenticated identity");
   host.Stream.Sync(null,room.Snapshot(),0);
   Check(host.Stream.HasLiveFeed(1)&&host.Stream.FeedCount==1,"face-invalid roster sync preserves fresh live texture");
   host.Stream.Handle(Frame(0,2,jpeg),1,room,host.Transport,0);
   Check(host.Stream.AcceptedFrames==1,"face absence does not permit spoofed frame identity");
   host.Stream.Handle(Frame(1,1,jpeg),1,room,host.Transport,0);
   Check(host.Stream.AcceptedFrames==1,"duplicate sequence stays rejected with absent face");
   host.Stream.Handle(Frame(1,2,jpeg),1,room,host.Transport,0);host.Stream.Handle(Frame(1,3,jpeg),1,room,host.Transport,0);
   Check(host.Stream.AcceptedFrames==2,"face-absent stream retains host token bucket limit");
   host.Stream.Handle(Frame(1,1,jpeg),1,room,host.Transport,0);
   Check(host.Stream.AcceptedFrames==2,"older out-of-order sequence cannot replace current absent-face video");
   host.Stream.Handle(Frame(1,4,new byte[]{1,2,3}),1,room,host.Transport,0);
   Check(host.Stream.AcceptedFrames==2,"invalid image payload remains rejected independently of face tracking");
   host.Stream.Handle(new WireMessage{kind="cameraProfile",id=1,camera=false},1,room,host.Transport,0);
   Check(!host.Stream.HasLiveFeed(1)&&host.Stream.FeedCount==0,"explicit camera consent off still removes the texture");
  }
  using(var client=new StreamFixture("Phone face-absent client video fixture"))
  {
   var room=CameraRoom();room.Find(1).phoneCamera=true;client.Stream.Sync(null,room.Snapshot(),0);
   client.Stream.Handle(Frame(1,1,jpeg),0,null,client.Transport,0);
   Check(client.Stream.AcceptedFrames==1&&client.Stream.HasLiveFeed(1),"client accepts host-relayed video for face-invalid phone");
   client.Stream.Sync(null,room.Snapshot(),0);
   Check(client.Stream.HasLiveFeed(1),"continued face-invalid snapshot does not flicker client video off");
   client.Stream.Handle(Frame(1,2,jpeg),1,null,client.Transport,0);
   Check(client.Stream.AcceptedFrames==1,"client rejects non-host frame sender even for authorized phone");
   client.Stream.Handle(Frame(1,1,jpeg),0,null,client.Transport,0);
   Check(client.Stream.AcceptedFrames==1,"client rejects replayed frame sequence");
   room.Find(1).disconnected=true;client.Stream.Sync(null,room.Snapshot(),0);
   client.Stream.Handle(Frame(1,2,jpeg),0,null,client.Transport,0);
   Check(!client.Stream.HasLiveFeed(1)&&client.Stream.AcceptedFrames==1,"disconnected player cannot keep video alive");
  }
  using(var localHost=new StreamFixture("Phone face-absent host publish fixture"))
  {
   var room=CameraRoom();Authorize(localHost,room);localHost.Stream.Publish(jpeg,1,room,localHost.Transport);
   Check(localHost.Stream.AcceptedFrames==1&&localHost.Stream.HasLiveFeed(1),"local host publishes video without valid face tracking");
   Check(localHost.Transport.Sent.Last().kind=="cameraFrame","local host broadcasts its absent-face camera frame");
   localHost.Stream.Publish(jpeg,1,room,localHost.Transport);
   Check(localHost.Stream.AcceptedFrames==1,"local publish frame-rate gate remains enforced");
  }
  using(var localClient=new StreamFixture("Phone face-absent peer publish fixture"))
  {
   var room=CameraRoom();room.Find(1).phoneCamera=true;localClient.Stream.Sync(null,room.Snapshot(),1);localClient.Stream.Publish(jpeg,1,null,localClient.Transport);
   Check(localClient.Stream.AcceptedFrames==1&&localClient.Stream.HasLiveFeed(1),"local client publishes video while face invalid");
   Check(localClient.Transport.Sent.Count==1&&localClient.Transport.Sent[0].id==1,"local client sends own frame to authority");
   localClient.Stream.Sync(null,new ArenaState(),1);
   Check(!localClient.Stream.HasLiveFeed(1)&&localClient.Stream.FeedCount==0,"roster departure frees face-absent camera feed");
  }
  return checks+" face-independent video checks passed";
 }
}
