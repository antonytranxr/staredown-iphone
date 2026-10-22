using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Staredown;
using UnityEngine;
using Unity.Services.Lobbies;

public static class StareRoomRejoinChecks
{
 static void Check(bool value,string message,ref int count)
 {if(!value)throw new Exception("Room rejoin: "+message);count++;}

 // Exercises real UTP connections and the actual tournament admission path.
 // No audio is captured or joined by these transport-only clients.
 public static async Task<string> Run(bool cloud=false)
 {
  int count=0;var host=new StareRelayNetwork();StareRelayNetwork client=null;
  var ids=new HashSet<int>();var model=new Tournament(1);model.Add(0,"HOST");model.EnableBotFill();
  string code=null;const ushort port=47819;
  try
  {
   if(cloud)
   {
    code=await host.HostAsync();host.SetRoomOpen(false);bool publishedClosed=false;
    // Confirm the stale hint in the service itself. A successful join must
    // not merely race ahead of the update that closes the directory entry.
    for(int attempt=0;attempt<6&&!publishedClosed;attempt++)
    {
     var query=LobbyService.Instance.GetLobbyAsync(host.VoiceKey);
     await Spin(host,null,()=>query.IsCompleted,10);var directory=await query;
     publishedClosed=directory.Data.TryGetValue("open",out var open)&&open.Value=="no";
     if(!publishedClosed){float retryAt=Time.realtimeSinceStartup+1.2f;await Spin(host,null,()=>Time.realtimeSinceStartup>=retryAt,3);}
    }
    Check(publishedClosed,"cloud directory is confirmed closed before any returning client joins",ref count);
   }
   else host.TestLocal(true,port);
   for(int cycle=0;cycle<6;cycle++)
   {
    client=new StareRelayNetwork();int id=-1;bool welcome=false,snapshot=false;
    if(cloud)
    {
     // Deliberately publish a stale closed directory hint. Admission belongs
     // to the current host, including returns after a match has started.
     host.SetRoomOpen(false);
     var join=client.JoinAsync(code);await Spin(host,client,()=>join.IsCompleted,30);await join;
    }
    else client.TestLocal(false,port,delayMs:25,jitterMs:10,requireWelcome:true);
    await Spin(host,client,()=>
    {
     while(host.Poll(out var packet))
     {
      var message=JsonUtility.FromJson<WireMessage>(packet.json);
      if(message.kind=="connected")
      {
       id=packet.sender;
       if(model.JoinHuman(id,"RETURNING PLAYER")==null)throw new Exception("Live roster rejected available seat");
       host.Send(id,new WireMessage{kind="welcome",id=id});
       host.Send(id,new WireMessage{kind="state",state=model.Snapshot()});
      }
     }
     while(client.Poll(out var packet))
     {
      var message=JsonUtility.FromJson<WireMessage>(packet.json);
      welcome|=message.kind=="welcome"&&message.id==id;
      snapshot|=message.kind=="state"&&message.state!=null&&Array.Exists(message.state.fighters,f=>f.id==id);
     }
     return id>0&&welcome&&snapshot;
    },15);
    Check(ids.Add(id),"each rejoin receives a fresh transport identity",ref count);
    Check(welcome&&snapshot,"returning client receives admission and current roster",ref count);
    Check(!cloud||client.RoomCode==code&&host.RoomCode==code&&host.VoiceKey==client.VoiceKey,"same room code and unique voice key survive rejoin",ref count);
    Check(model.Fighters.Count==Tournament.Capacity&&model.Fighters.FindAll(f=>f.bot).Count==Tournament.Capacity-2,"default full roster stays at 16 without removing active bots",ref count);
    if(cycle==0)
    {
     model.Input(0,0,0,true,true,0,0,0);model.Input(id,0,0,true,true,0,0,0);
     Check(model.Start(),"first client starts a match before leaving",ref count);
    }
    else
    {
     Check(!model.Find(id).alive&&model.Find(id).lives==0,"mid-match rejoin is a spectator, never resurrected",ref count);
     Check(model.Fighters.FindAll(f=>f.disconnected).Count==0,"returning player reclaims the departed roster slot",ref count);
    }
    client.Dispose();client=null;
    bool departed=false;
    await Spin(host,null,()=>
    {
     while(host.Poll(out var packet))if(JsonUtility.FromJson<WireMessage>(packet.json).kind=="disconnected"&&packet.sender==id){departed=true;model.Remove(id);}
     return departed;
    },5);
    Check(departed,"graceful departure releases host connection without waiting 15 seconds",ref count);
    Check(model.Fighters.Count==Tournament.Capacity&&model.Find(id).disconnected&&model.CanAdmitHuman,"full running match preserves results while reopening the departed human slot",ref count);
   }

   // Replacing a disconnected fighter leaves resolved pair history referring
   // to that former ID. Finish the real tournament to catch missing-ID errors
   // through later rounds, the champion celebration and the next lobby.
   bool champion=false;
   for(int tick=0;tick<20000;tick++)
   {
    model.Input(0,0,0,true,true,0,0,0);model.Tick(.05f);
    champion|=model.Phase==MatchPhase.Champion;
    if(champion&&model.Phase==MatchPhase.Lobby)break;
   }
   Check(champion&&model.Phase==MatchPhase.Lobby,"reclaimed pair history completes the tournament and returns to the lobby",ref count);
   Check(model.Fighters.Count==Tournament.Capacity&&model.Fighters.TrueForAll(f=>!f.disconnected&&f.alive),"next lobby removes departed records and restores the full roster",ref count);

   client=new StareRelayNetwork();
   if(cloud){var join=client.JoinAsync(code);await Spin(host,client,()=>join.IsCompleted,30);await join;}
   else client.TestLocal(false,port,requireWelcome:true);
   bool rejected=false;
   await Spin(host,client,()=>
   {
    while(host.Poll(out var packet))if(JsonUtility.FromJson<WireMessage>(packet.json).kind=="connected")host.RejectPeer(packet.sender,"ROOM FULL / WAIT FOR A PLAYER TO LEAVE");
    while(client.Poll(out var packet))rejected|=JsonUtility.FromJson<WireMessage>(packet.json).kind=="failed";
    return rejected;
   },10);
   Check(rejected&&client.Error=="ROOM FULL / WAIT FOR A PLAYER TO LEAVE","full room returns its actual reason before disconnecting",ref count);
   client.Dispose();client=null;
   Check(host.Bound,"individual join rejection leaves host room online",ref count);
   return count+(cloud?" live Relay":" local UTP")+" repeated leave/rejoin, spectator admission, directory and rejection checks passed";
  }
  finally{client?.Dispose();host.Dispose();}
 }

 public static async Task<string> MissingWelcome()
 {
  var host=new StareRelayNetwork();var client=new StareRelayNetwork();const ushort port=47820;
  try
  {
   host.TestLocal(true,port);client.TestLocal(false,port,requireWelcome:true);
   bool connected=false,failed=false;float began=Time.realtimeSinceStartup;
   await Spin(host,client,()=>
   {
    while(host.Poll(out var packet))connected|=JsonUtility.FromJson<WireMessage>(packet.json).kind=="connected";
    while(client.Poll(out var packet))failed|=JsonUtility.FromJson<WireMessage>(packet.json).kind=="failed";
    return failed;
   },StareRelayNetwork.WelcomeTimeout+6);
   if(!connected||!failed||!client.Error.Contains("HOST DID NOT RESPOND")||Time.realtimeSinceStartup-began<StareRelayNetwork.WelcomeTimeout)throw new Exception("Missing host admission must terminate joining with a clear timeout");
   return "Connected-but-unresponsive host releases the joining screen after "+StareRelayNetwork.WelcomeTimeout+" seconds";
  }
  finally{client.Dispose();host.Dispose();}
 }
 static async Task Spin(StareRelayNetwork host,StareRelayNetwork client,Func<bool> done,float seconds)
 {
  float end=Time.realtimeSinceStartup+seconds;
  while(Time.realtimeSinceStartup<end){host.Tick();client?.Tick();if(done())return;await Task.Delay(15);}
  throw new Exception("Room rejoin timed out: "+host.Error+" / "+client?.Error);
 }
}
