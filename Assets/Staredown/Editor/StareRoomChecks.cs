using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Staredown;
using UnityEngine;
using UnityEditor;
public static class StareRoomChecks
{
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
 public static string InputChecks()
 {
  checks=0;
  Check(StareRelayNetwork.ValidCode(" 0123 "),"numeric code trims and preserves leading zero");
  Check(!StareRelayNetwork.ValidCode("ABC1")&&!StareRelayNetwork.ValidCode("123")&&!StareRelayNetwork.ValidCode("12345")&&!StareRelayNetwork.ValidCode("12.3")&&!StareRelayNetwork.ValidCode("１２３４"),"reject letters, wrong length and non-ASCII digits");
  var stable=new StareHandRayStabilizer();var aim=new Ray(Vector3.one,Vector3.forward);var closure=new Ray(Vector3.one+Vector3.right*.08f,Quaternion.Euler(0,12,0)*Vector3.forward);
  var before=stable.Sample(aim,false,0,0,.02f);var during=stable.Sample(closure,false,.4f,.02f,.02f);
  Check(Vector3.Distance(before.origin,during.origin)<.001f&&Vector3.Angle(before.direction,during.direction)<.01f,"pre-pinch ray holds position and direction");
  Check(Vector3.Angle(stable.Sample(closure,true,1,.04f,.02f).direction,aim.direction)<.01f,"closed pinch holds target");
  Check(Vector3.Angle(stable.Sample(closure,false,0,.08f,.02f).direction,aim.direction)<.01f,"release hysteresis");
  Check(Vector3.Angle(stable.Sample(closure,false,0,.2f,.02f).direction,aim.direction)>1,"released hand resumes aiming");
  stable.Reset();Check(Vector3.Angle(stable.Sample(closure,false,0,.3f,.02f).direction,closure.direction)<.01f,"tracking recovery resets aim");
  var t=new Tournament(1);t.Add(0,"HOST");t.Add(1,"DEMO");t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0,true);
  Check(t.Find(1).demo&&!t.Find(0).demo&&t.Start(),"mixed real-eye and demo match admission");
  for(int n=0;n<110;n++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0,true);t.Tick(.05f);}
  Check(t.Phase==MatchPhase.Duel,"mixed demo countdown reaches duel");
  t.Input(1,1,1,true,true,1,0,0,true);for(int n=0;n<4;n++)t.Tick(.05f);
  Check(!t.Find(1).alive&&t.Find(0).alive,"demo button blink loses authoritative duel");
  var gate=new BlinkGate();for(int n=0;n<90;n++)gate.Sample(0,0,true,.02f);int blinks=0;
  for(int n=0;n<30;n++)if(gate.Sample(1,1,true,.02f))blinks++;
  Check(blinks==1,"held button records one blink");
  return checks+" room/input checks passed";
 }
 public static async Task<string> TransportChecks(bool cloud)
 {
  checks=0;var host=new StareRelayNetwork();var client=new StareRelayNetwork();
  try
  {
   if(cloud){string code=await host.HostAsync();Check(StareRelayNetwork.ValidCode(code),"live numeric room code");var join=client.JoinAsync(code);await Spin(host,client,()=>join.IsCompleted,30);await join;Check(!string.IsNullOrEmpty(host.VoiceKey)&&host.VoiceKey==client.VoiceKey,"host and guest share the same unique voice room");}
   else {host.TestLocal(true,47778);client.TestLocal(false,47778);}
   int id=-1;bool connected=false;
   await Spin(host,client,()=>{while(host.Poll(out var p))if(JsonUtility.FromJson<WireMessage>(p.json).kind=="connected")id=p.sender;while(client.Poll(out var q))if(JsonUtility.FromJson<WireMessage>(q.json).kind=="connected")connected=true;return id>0&&connected;},30);
   Check(id>0&&connected,"host/client connect");
   var t=new Tournament(1);for(int n=0;n<Tournament.Capacity;n++)t.Add(n,"PLAYER "+n,true);
   string large=new string('p',4000);host.Send(id,new WireMessage{kind="avatarPose",pose=large});host.Broadcast(new WireMessage{kind="state",state=t.Snapshot()});
   bool pose=false,state=false;await Spin(host,client,()=>{while(client.Poll(out var p)){var m=JsonUtility.FromJson<WireMessage>(p.json);pose|=m.kind=="avatarPose"&&m.pose==large;state|=m.kind=="state"&&m.state.fighters.Length==Tournament.Capacity;}return pose&&state;},10);
   Check(pose&&state,"fragmented pose and 16-player snapshot delivery");
   client.Send(0,new WireMessage{kind="input",blink=3,demo=true,valid=true,ready=true,hasRoomPose=true,roomPosition=new Vector3(3,0,-2),roomYaw=125});bool input=false;
   await Spin(host,client,()=>{while(host.Poll(out var p)){var m=JsonUtility.FromJson<WireMessage>(p.json);input|=m.kind=="input"&&p.sender==id&&m.demo&&m.blink==3&&m.hasRoomPose&&m.roomPosition==new Vector3(3,0,-2)&&Mathf.Abs(m.roomYaw-125)<.01f;}return input;},10);
   Check(input,"guest demo blink reaches authenticated connection id");
   string originalCode=host.RoomCode;
   t.Phase=MatchPhase.Champion;t.Winner=0;t.ReturnToLobby();
   t.Find(id).hasRoomPose=true;t.Find(id).roomPosition=new Vector3(3,0,-2);t.Find(id).roomYaw=125;
   host.Broadcast(new WireMessage{kind="state",state=t.Snapshot()});bool social=false;
   await Spin(host,client,()=>{while(client.Poll(out var p)){var m=JsonUtility.FromJson<WireMessage>(p.json);social|=m.kind=="state"&&m.state.phase==0&&m.state.fighters.Length==Tournament.Capacity&&m.state.fighters[id].hasRoomPose&&m.state.fighters[id].roomPosition==new Vector3(3,0,-2);}return social;},10);
   Check(social&&host.RoomCode==originalCode,"same connected room delivers social positions after match reset");
   client.Dispose();bool disconnected=false;await Spin(host,null,()=>{while(host.Poll(out var p))disconnected|=JsonUtility.FromJson<WireMessage>(p.json).kind=="disconnected";return disconnected;},20);
   Check(disconnected,"guest departure reaches host");
   return checks+(cloud?" live Relay":" local UTP")+" transport checks passed";
  }
  finally{client.Dispose();host.Dispose();}
 }
 public static string SocialRulesChecks()
 {
  checks=0;var t=new Tournament(1);t.Add(0,"HOST");t.Add(1,"GUEST");
  Check(t.RoomPose(1,new Vector3(3,0,-2),125),"guest can roam in lobby");
  var snapshot=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(t.Snapshot()));
  Check(ArenaLayout.Seat(snapshot,1)==new Vector3(3,0,-2)&&Vector3.Angle(ArenaLayout.Facing(snapshot,1),Quaternion.Euler(0,125,0)*Vector3.forward)<.01f,"position and heading survive snapshot");
  Check(!t.RoomPose(1,new Vector3(float.NaN,0,0),0)&&!t.RoomPose(1,new Vector3(40,0,0),0)&&!t.RoomPose(1,Vector3.up,0)&&!t.RoomPose(99,Vector3.zero,0)&&!t.RoomPose(1,Vector3.zero,float.PositiveInfinity),"reject invalid and out-of-arena poses");
  t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0,true);Check(t.Start(),"ready room starts match");
  Check(!t.RoomPose(1,Vector3.zero,0),"gameplay pads cannot be moved by lobby pose messages");
  for(int i=0;i<110;i++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0,true);t.Tick(.05f);}
  t.Input(1,1,1,true,true,1,0,0,true);
  for(int i=0;i<90;i++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,1,0,0,true);t.Tick(.05f);}
  Check(t.Phase==MatchPhase.Champion&&t.Winner==0,"winner celebration occurs before returning");
  for(int i=0;i<125;i++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,1,0,0,true);t.Tick(.05f);}
  Check(t.Phase==MatchPhase.Lobby&&t.Winner==0&&t.Fighters.Count==2&&t.Fighters.TrueForAll(f=>f.alive&&f.pair==-1&&!f.hasRoomPose)&&t.Pairs.Count==0&&!t.FinalRound,"winner and loser return together with roster retained and pads reset");
  Check(t.RoomPose(1,new Vector3(-3,0,2),35)&&t.Start(),"former spectator can roam then play a rematch");
  t.Remove(1);for(int i=0;i<400;i++){t.Input(0,0,0,true,true,0,0,0);t.Tick(.05f);}
  Check(t.Phase==MatchPhase.Lobby&&t.Find(1)==null&&t.Fighters.Count==1,"disconnected contestants removed before next match");
  Check(t.Add(2,"NEW GUEST")!=null,"new guests can join after completion");
  var full=new Tournament(1);for(int i=0;i<Tournament.Capacity;i++)full.Add(i,"BOT "+i,true);full.Start();bool crowned=false;
  for(int i=0;i<12000;i++){full.Tick(.05f);crowned|=full.Phase==MatchPhase.Champion;if(crowned&&full.Phase==MatchPhase.Lobby)break;}
  Check(crowned&&full.Phase==MatchPhase.Lobby&&full.Fighters.Count==Tournament.Capacity&&full.Fighters.TrueForAll(f=>f.alive&&f.pair==-1),"full sixteen-player elimination returns all contestants to social arena");
  var all=full.Snapshot();foreach(var f in all.fighters)Check(Mathf.Abs(ArenaLayout.Seat(all,f.id).y)<.001f&&ArenaLayout.Seat(all,f.id).magnitude<=8.01f,"contestant "+f.id+" returns to arena floor");
  return checks+" social lobby rules checks passed";
 }
 static async Task Spin(StareRelayNetwork a,StareRelayNetwork b,Func<bool> complete,float timeout)
 {
  float end=Time.realtimeSinceStartup+timeout;
  while(Time.realtimeSinceStartup<end){a.Tick();b?.Tick();if(complete())return;await Task.Delay(15);}
  throw new Exception("Transport test timed out: "+a.Error+" / "+b?.Error);
 }
}
