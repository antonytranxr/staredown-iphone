using System;
using System.Linq;
using UnityEngine;
public static class StareCompetitionChecks
{
public static string Rules()
{
int n=0;System.Action<bool,string> check=(ok,label)=>{if(!ok)throw new System.Exception(label);n++;};var t=new Staredown.Tournament();t.Add(0,"HOST");t.Add(1,"FRIEND");t.SetBotFill(true);check(t.Fighters.Count==Staredown.Tournament.Capacity,"Mixed fill");t.SetBotFill(false);check(t.Fighters.Count==2&&t.Fighters.All(f=>!f.bot),"Humans-only removes bots");t.Add(2,"THIRD");t.SetBotFill(true);check(t.Fighters.Count==Staredown.Tournament.Capacity&&t.Fighters.Count(f=>!f.bot)==3,"Refill preserves humans");check(t.SelectSkin(0,2)&&!t.SelectSkin(0,4),"Cosmetics validated");foreach(var f in t.Fighters.Where(f=>!f.bot))t.Input(f.id,0,0,true,true,0,0,0);check(t.Start(),"Calibrated room starts");check(!string.IsNullOrEmpty(t.MatchToken),"Unique match reward token");check(!t.SetBotFill(false),"Match roster remains locked");check(t.Dance(0,true,3)&&t.Find(0).dancing&&t.Find(0).dance==3,"Competitor can dance");check(!t.Dance(0,true,9),"Invalid dance rejected");var copy=JsonUtility.FromJson<Staredown.WireMessage>(JsonUtility.ToJson(new Staredown.WireMessage{kind="state",state=t.Snapshot()})).state;check(copy.botFill&&copy.matchToken==t.MatchToken&&copy.fighters.First(f=>f.id==0).dance==3&&copy.fighters.First(f=>f.id==0).skin==2,"Room mode, dance, skin and reward token serialize");t.Find(0).alive=false;var state=t.Snapshot();var competitor=state.fighters.First(f=>f.alive);var protectedSeat=Staredown.ArenaLayout.Seat(state,competitor.id);check(!Staredown.StareArenaFields.Contains(state,protectedSeat)&&!t.RoomPose(0,protectedSeat,0),"Protected competitor zone enforced on host");var random=new System.Random(99);for(int i=0;i<200;i++){var input=new Vector3((float)random.NextDouble()*30-15,0,(float)random.NextDouble()*30-15);var clamped=Staredown.StareArenaFields.Clamp(state,input);check(Staredown.StareArenaFields.Contains(state,clamped),"Safe spectator clamp "+i);}check(Staredown.ArenaLayout.Seat(state,0).y==0,"Eliminated human returns to arena floor");check(t.RoomPose(0,new Vector3(0,0,-12),45),"Spectator floor pose accepted");check(!t.RoomPose(0,new Vector3(0,1.08f,-12),45),"Old seating floor rejected");
string walletKey="Staredown.Wallet";bool had=PlayerPrefs.HasKey(walletKey);string saved=PlayerPrefs.GetString(walletKey,"");var root=new GameObject("Reward verification");try{PlayerPrefs.DeleteKey(walletKey);var wallet=root.AddComponent<Staredown.StareProgression>();wallet.Initialize(null);var reward=new Staredown.ArenaState{phase=(int)Staredown.MatchPhase.Duel,matchToken="TEST_MATCH",fighters=new[]{new Staredown.Fighter{id=5,roundWins=2,alive=false}}};wallet.Observe(reward,5,"ROOM");check(wallet.Coins==40&&wallet.XP==60,"Round and elimination rewards");wallet.Observe(reward,5,"ROOM");check(wallet.Coins==40,"Duplicate snapshots never duplicate rewards");reward.phase=(int)Staredown.MatchPhase.Champion;reward.winner=5;reward.fighters[0].alive=true;wallet.Observe(reward,5,"ROOM");check(wallet.Coins==90&&wallet.XP==120&&wallet.Level==2,"Champion reward levels player up");var restored=root.AddComponent<Staredown.StareProgression>();restored.Initialize(null);restored.Observe(reward,5,"ROOM");check(restored.Coins==90&&restored.Level==2,"Reconnect and reload preserve rewards without duplication");check(restored.Select(1)&&restored.Unlocked(1)&&restored.Coins==15,"Unlocked skin costs coins");check(restored.Select(0)&&restored.Coins==15&&!restored.Select(3),"Equip free skins; locked level gates enforced");
// Rejoins use fresh transport IDs and must not mint participation rewards.
t.Remove(1);
for(int id=50;id<53;id++)
{
 check(t.JoinHuman(id,"RETURNING PLAYER")!=null,"Departed seat can be rejoined");
 restored.Observe(t.Snapshot(),id,"ROOM");
 check(restored.Coins==15&&restored.XP==120,"Next-match spectator earns no rewards on rejoin "+id);
 t.Remove(id);
}
var nextMatch=new Staredown.ArenaState{phase=(int)Staredown.MatchPhase.Duel,matchToken="NEXT_MATCH",fighters=new[]{new Staredown.Fighter{id=52,roundWins=0,alive=false,status="BLINK / SPECTATOR"}}};
restored.Observe(nextMatch,52,"ROOM");
check(restored.Coins==20&&restored.XP==130,"Returning player earns participation after competing in next match");
restored.Observe(nextMatch,52,"ROOM");
check(restored.Coins==20&&restored.XP==130,"Returning competitor finish reward remains idempotent");
}finally{if(had)PlayerPrefs.SetString(walletKey,saved);else PlayerPrefs.DeleteKey(walletKey);PlayerPrefs.Save();UnityEngine.Object.DestroyImmediate(root);}return n+" competition/progression checks passed";

}
}
