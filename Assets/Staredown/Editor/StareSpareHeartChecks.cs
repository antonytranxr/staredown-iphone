using System;
using System.Linq;
using UnityEngine;
using Staredown;

public static class StareSpareHeartChecks
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Spare heart/spawn: "+message);checks++;}
    static void Step(Tournament room,float duration)
    {
        for(float elapsed=0;elapsed<duration;elapsed+=.025f)
        {
            foreach(var f in room.Fighters.Where(f=>!f.bot&&!f.disconnected))room.Input(f.id,0,0,true,true,f.blinkSequence,0,0);
            room.Tick(.025f);
        }
    }
    static void Duel(Tournament room)
    {for(int i=0;i<500&&room.Phase!=MatchPhase.Duel;i++)Step(room,.025f);Check(room.Phase==MatchPhase.Duel,"duel starts");}
    static Tournament Room(int count)
    {
        var room=new Tournament();for(int i=0;i<count;i++){room.Add(i,"Player "+i);room.Input(i,0,0,true,true,0,0,0);}
        Check(room.Start(),"room starts with "+count);return room;
    }
    static void ClearSpawns(ArenaState state)
    {
        foreach(var a in state.fighters)
        foreach(var b in state.fighters.Where(b=>b.id>a.id))
        {
            var delta=ArenaLayout.Seat(state,a.id)-ArenaLayout.Seat(state,b.id);delta.y=0;
            Check(delta.sqrMagnitude>=3.99f,"separate positions for "+a.id+" and "+b.id);
        }
    }
    public static string Rules()
    {
        checks=0;
        // A missing field in older host snapshots must retain index-based placement.
        var legacy=JsonUtility.FromJson<ArenaState>("{\"phase\":0,\"fighters\":[{\"id\":0,\"alive\":true},{\"id\":1,\"alive\":true}],\"pairs\":[]}");
        Check(new Fighter().arenaSlot==0&&legacy.fighters.All(f=>f.arenaSlot==0),"missing spawn slots use the unset sentinel");
        Check(Vector3.Distance(ArenaLayout.Seat(legacy,0),ArenaLayout.Seat(legacy,1))>1,"old-host players keep distinct fallback seats");
        Check(StareScoreboard.Summary(legacy)=="NEXT LOSS = OUT","old-host rules retain accurate screen instructions");
        foreach(int count in new[]{2,3,4,8,9,16})
        {
            var room=Room(count);Check(room.LivesMode&&room.StartingLives==2&&room.Fighters.All(f=>f.lives==2),"one spare heart at every room size");
            Duel(room);int id=room.Pairs[0].a;room.Blink(id);Step(room,.15f);
            Check(room.Find(id).alive&&room.Find(id).lives==1,"first loss spends heart without eliminating");
            Check(StareScoreboard.Status(room.Snapshot(),room.Find(id))=="HEART USED","screen does not call the protected loser a winner");
            Check(StareMobileHud.MatchCaption(room.Snapshot(),room.Find(id),"").Contains("one more chance"),"mobile explains the second chance");
            foreach(var pair in room.Pairs.Where(p=>p.winner<0))room.Blink(pair.a);Step(room,.15f);
            Check(room.Phase==MatchPhase.Intermission&&room.Fighters.All(f=>f.alive),"all first-round losers remain in");
            Duel(room);
            for(int skipped=0;skipped<16&&!room.Pairs.Any(p=>p.a==id||p.b==id);skipped++)
            {foreach(var p in room.Pairs.Where(p=>p.winner<0))room.Blink(p.a);Step(room,.15f);Duel(room);}
            var second=room.Pairs.First(p=>p.a==id||p.b==id);room.Blink(id);Step(room,.15f);
            Check(!room.Find(id).alive&&room.Find(id).lives==0&&second.winner>=0,"second loss eliminates");
            for(int rounds=0;rounds<64&&room.Phase!=MatchPhase.Champion;rounds++)
            {
                if(room.Phase==MatchPhase.Countdown)Duel(room);
                if(room.Phase==MatchPhase.Duel){foreach(var p in room.Pairs.Where(p=>p.winner<0))room.Blink(p.a);Step(room,.15f);}
                if(room.Phase==MatchPhase.Intermission)Step(room,4.05f);
            }
            Check(room.Phase==MatchPhase.Champion&&room.Fighters.Count(f=>f.alive)==1,"full spare-heart tournament converges");
            room.ReturnToLobby();Check(room.Fighters.All(f=>f.alive&&f.lives==2),"next match restores one spare heart");ClearSpawns(room.Snapshot());
        }
        var filled=new Tournament();filled.Add(0,"HOST");filled.EnableBotFill();ClearSpawns(filled.Snapshot());
        for(int id=1;id<=8;id++)
        {
            var before=filled.Fighters.ToDictionary(f=>f.id,f=>ArenaLayout.Seat(filled.Snapshot(),f.id));
            // Stand on the departing bot's pad to reproduce the user's join collision.
            var departing=filled.Fighters.Last(f=>f.bot);var occupied=before[departing.id];
            Check(filled.RoomPose(0,occupied,0),"host can occupy proposed join point");before[0]=occupied;
            Check(filled.JoinHuman(id,"JOINER "+id)!=null,"human replaces bot");
            var snapshot=filled.Snapshot();ClearSpawns(snapshot);
            foreach(var f in snapshot.fighters.Where(f=>f.id!=id))Check(ArenaLayout.Seat(snapshot,f.id)==before[f.id],"join never shifts existing player or bot "+f.id);
            var wire=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(snapshot));
            Check(ArenaLayout.Seat(wire,id)==ArenaLayout.Seat(snapshot,id)&&wire.fighters.Single(f=>f.id==id).hasRoomPose,"chosen spawn travels in welcome snapshot");
        }
        var stable=filled.Fighters.ToDictionary(f=>f.id,f=>ArenaLayout.Seat(filled.Snapshot(),f.id));filled.Remove(4);
        foreach(var f in filled.Fighters.Where(f=>f.id!=4&&stable.ContainsKey(f.id)))Check(ArenaLayout.Seat(filled.Snapshot(),f.id)==stable[f.id],"leave and bot refill keep existing poses stable");
        filled.JoinHuman(40,"RETURNED");ClearSpawns(filled.Snapshot());
        var match=Room(16);match.Remove(3);var guest=match.JoinHuman(40,"WATCHING");var active=match.Snapshot();
        Check(guest!=null&&!guest.alive&&guest.lives==0&&guest.hasRoomPose,"returning guest gets a spectator pose without a spare heart");
        Check(StareArenaFields.Contains(active,guest.roomPosition),"spectator entry is outside competitor fields");
        foreach(var f in active.fighters.Where(f=>f.id!=40&&!f.disconnected))Check(Vector3.Distance(guest.roomPosition,ArenaLayout.Seat(active,f.id))>=1.99f,"spectator spawn clear of "+f.id);
        return checks+" spare-heart, full-tournament and stable spawn checks passed";
    }
}
