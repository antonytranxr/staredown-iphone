using System;
namespace Staredown
{
 [Serializable] public sealed class StareCapturedBlink
 {
  public string matchToken;public int round,duel,sequence;public bool valid,ready;public float age;
 }
 // One losing event per duel, bounded retries and an explicit host receipt.
 // Age is measured locally; device clocks are never compared over the network.
 public sealed class StareBlinkOutbox
 {
  public const float MaximumAge=3;
  StareCapturedBlink pending;double capturedAt,nextSend;
  public string Status {get;private set;}="Blink delivery: ready";
  public bool Pending=>pending!=null;
  public void Record(ArenaState state,int sequence,double at)
  {
   if(pending!=null||sequence<=0||double.IsNaN(at)||double.IsInfinity(at))return;
   pending=new StareCapturedBlink{matchToken=state.matchToken,round=state.round,duel=state.duel,sequence=sequence,valid=true,ready=true};capturedAt=at;nextSend=0;Status="Blink delivery: sending";
  }
  public StareCapturedBlink Next(ArenaState state,double now)
  {
   if(pending==null)return null;
   if(state.matchToken!=pending.matchToken||state.round!=pending.round||state.duel!=pending.duel||state.phase!=(int)MatchPhase.Duel){pending=null;Status="Blink delivery: duel ended";return null;}
   if(now-capturedAt>MaximumAge){pending=null;Status="Blink delivery: timed out / check connection";return null;}
   if(now<nextSend)return null;nextSend=now+.25;
   return new StareCapturedBlink{matchToken=pending.matchToken,round=pending.round,duel=pending.duel,sequence=pending.sequence,valid=pending.valid,ready=pending.ready,age=(float)Math.Max(0,now-capturedAt)};
  }
  public void Acknowledge(StareCapturedBlink receipt,string result)
  {
   if(pending==null||receipt==null||receipt.matchToken!=pending.matchToken||receipt.round!=pending.round||receipt.duel!=pending.duel||receipt.sequence!=pending.sequence)return;
   pending=null;Status=result=="confirmed"?"Blink delivery: host confirmed":"Blink delivery: "+result;
  }
  public void Clear(){pending=null;Status="Blink delivery: ready";}
 }
}
