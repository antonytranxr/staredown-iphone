using System;
using System.Linq;
using UnityEngine;
using Staredown;

// External proposal while Android packaging runs. Copy into the Editor folder
// only after the current build has finished and the runtime guard is fixed.
public static class StarePausedPhoneChecks
{
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception("Paused phone: "+label);checks++;}
 static Tournament Duel(int players=4)
 {
  var room=new Tournament(1);
  for(int i=0;i<players;i++){room.Add(i,"PLAYER "+i);room.Input(i,0,0,true,true,0,0,0);}
  room.Find(0).phoneCamera=true;Check(room.Start(),"room admitted calibrated competitors");
  for(int i=0;i<250&&room.Phase==MatchPhase.Countdown;i++){Feed(room);room.Tick(.025f);}
  Check(room.Phase==MatchPhase.Duel,"countdown reached a fresh duel");return room;
 }
 static void Feed(Tournament room,int closed=-1,int[] missing=null)
 {
  foreach(var f in room.Fighters)
  {bool valid=missing==null||!missing.Contains(f.id);room.Input(f.id,f.id==closed?1:0,f.id==closed?1:0,valid,true,f.blinkSequence,0,0);}
 }
 static void Step(Tournament room,float duration,int closed=-1,int[] missing=null)
 {
  for(float elapsed=0;elapsed<duration-.000001f;)
  {float dt=Mathf.Min(.025f,duration-elapsed);Feed(room,closed,missing);room.Tick(dt);elapsed+=dt;}
 }
 public static string PauseAndOrderingChecks()
 {
  checks=0;
  var paused=Duel();var phonePair=paused.Pairs.First(p=>p.a==0||p.b==0);float before=paused.Remaining;
  // First tick is deliberately long enough to cross closure debounce. The
  // pause must be computed from this tick's VR validity before raw phone eyes.
  Feed(paused,0,new[]{1});paused.Tick(.05f);
  Check(paused.TrackingPaused&&phonePair.pending<0,"new headset tracking loss protects phone blink on the very first paused tick");
  Step(paused,.25f,0,new[]{1});
  Check(phonePair.pending<0&&paused.Fighters.All(f=>f.alive)&&Mathf.Abs(paused.Remaining-before)<.001f,"held phone eyes closed during global tracking pause do not queue a loss");
  Check(paused.Find(0).phoneClosedFor==0,"paused closure time never accumulates");
  Feed(paused,0);paused.Tick(.025f);
  Check(!paused.TrackingPaused&&phonePair.pending<0&&paused.Find(0).alive,"resume starts fresh closure debounce rather than carrying paused closure");
  Feed(paused,0);paused.Tick(.025f);
  Check(phonePair.pending==0,"holding eyes closed after resume still queues a blink after debounce");
  Step(paused,.1f,0);
  Check(!paused.Find(0).alive&&phonePair.winner!=0,"continued post-resume closure resolves as a normal blink loss");

  var reopened=Duel();var reopenedPair=reopened.Pairs.First(p=>p.a==0||p.b==0);
  Step(reopened,.2f,0,new[]{1});Step(reopened,.1f,-1,new[]{1});Step(reopened,.2f);
  Check(reopened.Find(0).alive&&reopenedPair.pending<0&&!reopened.TrackingPaused,"phone may blink and reopen during pause without deferred loss");

  var absent=Duel();var absentPair=absent.Pairs.First(p=>p.a==0||p.b==0);float absentBefore=absent.Remaining;
  Step(absent,Tournament.PhoneFaceLossGrace+.2f,-1,new[]{0,1});
  Check(!absent.Find(0).alive&&absentPair.winner!=0,"face-loss grace still progresses while another headset causes a global tracking pause");
  Check(absent.TrackingPaused&&Mathf.Abs(absent.Remaining-absentBefore)<.001f,"unrelated headset pause persists after phone forfeit");

  var earlier=Duel(2);var pair=earlier.Pairs[0];
  Step(earlier,Tournament.PhoneFaceLossGrace-.01f,-1,new[]{0});
  earlier.Blink(1);Feed(earlier,-1,new[]{0});earlier.Tick(.05f);
  Check(earlier.Find(0).alive&&earlier.Find(1).alive&&pair.pending==1&&pair.winner<0,"earlier blink inside arbitration window cannot be overwritten by later face-forfeit threshold");
  Feed(earlier,-1,new[]{0});earlier.Tick(.05f);
  Check(!earlier.Find(1).alive&&earlier.Find(0).alive&&pair.winner==0,"earlier pending blink completes normal arbitration before missing-face forfeit");
  Check(earlier.Find(1).status=="BLINKED / SPECTATOR","short pending-blink priority preserves correct elimination reason");
  return checks+" paused phone and event-order checks passed";
 }
}
