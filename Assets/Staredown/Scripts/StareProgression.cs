using System;
using System.Collections.Generic;
using UnityEngine;
namespace Staredown
{
 public sealed class StareProgression : MonoBehaviour
 {
  [Serializable] public sealed class Wallet {public int coins,xp,unlocked=1,equipped;public List<string> claims=new List<string>();}
  static readonly int[] Levels={1,2,4,6},Costs={0,75,150,250};
  public static readonly string[] SkinNames={"Original","Glacier","Sunfire","Royal neon"};
  string sampledToken;int sampledId=-1,sampledWins=-1;bool sampledDead,sampledChampion;
  Wallet wallet;StareGame game;string session=Guid.NewGuid().ToString("N");
  public int Coins=>wallet.coins;public int XP=>wallet.xp;public int Equipped=>wallet.equipped;
  public int Level {get{int level=1,remaining=XP;while(level<100&&remaining>=RequiredXP(level)){remaining-=RequiredXP(level);level++;}return level;}}
  public static int RequiredXP(int level)=>100+(level-1)*40;
  public int LevelXP {get{int xp=XP;for(int i=1;i<Level;i++)xp-=RequiredXP(i);return xp;}}
  public string Status {get;private set;}="Win rounds to earn coins and XP.";
  public bool Unlocked(int skin)=>skin>=0&&skin<4&&(wallet.unlocked&(1<<skin))!=0;
  public bool Available(int skin)=>skin>=0&&skin<4&&(Unlocked(skin)||Level>=Levels[skin]&&Coins>=Costs[skin]);
  public string Label(int skin)=>SkinNames[skin]+(Unlocked(skin)?(Equipped==skin?" / equipped":" / unlocked"):(" / level "+Levels[skin]+" / "+Costs[skin]+" coins"));
  public void Initialize(StareGame owner)
  {
   game=owner;try{wallet=JsonUtility.FromJson<Wallet>(PlayerPrefs.GetString("Staredown.Wallet",""));}catch(Exception){}if(wallet==null)wallet=new Wallet();wallet.coins=Mathf.Clamp(wallet.coins,0,1000000);wallet.xp=Mathf.Clamp(wallet.xp,0,10000000);wallet.unlocked=(wallet.unlocked&15)|1;if(wallet.claims==null)wallet.claims=new List<string>();if(!Unlocked(wallet.equipped))wallet.equipped=0;
  }
  public bool Select(int skin)
  {
   if(!Available(skin)){Status="Reach the required level and earn more coins.";return false;}
   if(!Unlocked(skin)){wallet.coins-=Costs[skin];wallet.unlocked|=1<<skin;Status=SkinNames[skin]+" unlocked!";}else Status=SkinNames[skin]+" equipped.";
   wallet.equipped=skin;Save();return true;
  }
  bool Claim(string key,int coins,int xp)
  {
   if(wallet.claims.Contains(key))return false;wallet.claims.Add(key);while(wallet.claims.Count>512)wallet.claims.RemoveAt(0);wallet.coins=Mathf.Min(1000000,wallet.coins+coins);wallet.xp=Mathf.Min(10000000,wallet.xp+xp);Status="+"+coins+" coins / +"+xp+" XP";Save();return true;
  }
  public void Observe(ArenaState state,int localId,string room)
  {
   if(string.IsNullOrEmpty(state.matchToken)||state.phase==(int)MatchPhase.Lobby)return;var fighter=Array.Find(state.fighters,f=>f.id==localId);if(fighter==null||fighter.bot||fighter.status=="SPECTATOR / NEXT MATCH")return;bool dead=!fighter.alive,champion=state.phase==(int)MatchPhase.Champion&&state.winner==localId;if(sampledToken==state.matchToken&&sampledId==localId&&sampledWins==fighter.roundWins&&sampledDead==dead&&sampledChampion==champion)return;sampledToken=state.matchToken;sampledId=localId;sampledWins=fighter.roundWins;sampledDead=dead;sampledChampion=champion;string key=(room??session)+":"+state.matchToken+":"+localId;
   for(int win=1;win<=fighter.roundWins;win++)Claim(key+":round:"+win,10+win*5,25);
   if(!fighter.alive&&!fighter.disconnected)Claim(key+":finish",5,10);
   if(state.phase==(int)MatchPhase.Champion&&state.winner==localId)Claim(key+":champion",50,60);
  }
  void Save(){PlayerPrefs.SetString("Staredown.Wallet",JsonUtility.ToJson(wallet));PlayerPrefs.Save();}
 }
}
