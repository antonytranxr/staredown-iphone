using System;
using System.Linq;
using UnityEngine;
using Staredown;
public static class StareCrossplayReliabilityChecks
{
 static int n;static void Check(bool ok,string why){n++;if(!ok)throw new Exception("Crossplay reliability: "+why);}
 static Tournament Duel()
 {
  var t=new Tournament();t.Add(0,"Quest");var phone=t.Add(1,"Phone");phone.phoneCamera=true;
  t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);Check(t.Start(),"ready two-player match starts");
  for(int i=0;i<105&&t.Phase!=MatchPhase.Duel;i++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);t.Tick(.05f);}
  Check(t.Phase==MatchPhase.Duel&&t.Duel>0,"Go assigns a duel generation");return t;
 }
 static StareCapturedBlink Evidence(Tournament t,int seq=1)=>new StareCapturedBlink{matchToken=t.MatchToken,round=t.Round,duel=t.Duel,sequence=seq,valid=true,ready=true,age=.1f};
 public static string Rules()
 {
  n=0;var legacy=Duel();legacy.Input(1,0,0,false,false,1,0,0);legacy.Input(1,0,0,true,true,1,0,0);Check(legacy.Pairs[0].pending<0,"fixture reproduces old counter consumed during invalid tracking");
  var t=Duel();t.Input(1,0,0,false,false,1,0,0,false,true);
  var e=Evidence(t);Check(t.AcceptCapturedBlink(1,e)=="confirmed"&&t.Pairs[0].pending==1,"valid captured event survives invalid latest tracking");
  float pendingAt=t.Pairs[0].pendingAt;Check(t.AcceptCapturedBlink(1,e)=="confirmed"&&t.Pairs[0].pendingAt==pendingAt,"duplicate retry acknowledged without resetting result timer");
  t.Tick(.1f);Check(!t.Find(1).alive&&t.Find(1).status.StartsWith("BLINKED"),"captured event eliminates the phone");
  Check(t.AcceptCapturedBlink(1,e)=="confirmed","lost acknowledgement can retry after elimination");
  t=Duel();e=Evidence(t);t.Find(1).phoneCamera=false;Check(t.AcceptCapturedBlink(1,e)=="confirmed","camera disabled after capture cannot erase valid losing event");
  t=Duel();e=Evidence(t);Check(t.AcceptCapturedBlink(0,e)!="confirmed","transport sender cannot claim another phone's blink");
  e.duel++;Check(t.AcceptCapturedBlink(1,e)=="different duel","wrong duel rejected");e=Evidence(t);e.round++;Check(t.AcceptCapturedBlink(1,e)=="different duel","wrong round rejected");
  e=Evidence(t);e.matchToken="other match";Check(t.AcceptCapturedBlink(1,e)=="different duel","old match rejected");
  foreach(float age in new[]{-1,float.NaN,float.PositiveInfinity,3.01f}){e=Evidence(t);e.age=age;Check(t.AcceptCapturedBlink(1,e)=="invalid capture","invalid age rejected");}
  e=Evidence(t);e.valid=false;Check(t.AcceptCapturedBlink(1,e)=="invalid capture","invalid capture rejected");e=Evidence(t);e.ready=false;Check(t.AcceptCapturedBlink(1,e)=="invalid capture","uncalibrated capture rejected");
  t.Phase=MatchPhase.Countdown;e=Evidence(t);Check(t.AcceptCapturedBlink(1,e)=="duel ended","countdown blink free");
  t=Duel();int previous=t.Duel;var stale=Evidence(t);t.Remaining=.01f;t.Tick(.02f);Check(t.Phase==MatchPhase.Countdown,"timeout enters rematch countdown");
  for(int i=0;i<105&&t.Phase!=MatchPhase.Duel;i++){t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);t.Tick(.05f);}
  Check(t.Duel==previous+1&&t.AcceptCapturedBlink(1,stale)=="different duel","delayed previous attempt cannot lose new duel in same round");
  var state=t.Snapshot();var box=new StareBlinkOutbox();box.Record(state,2,10);var first=box.Next(state,10.1);Check(first!=null&&Math.Abs(first.age-.1f)<.001f,"outbox carries capture age without clock synchronization");
  Check(box.Next(state,10.2)==null,"retry bounded below 4Hz");var retry=box.Next(state,10.4);Check(retry!=null&&retry.sequence==first.sequence&&retry.age>first.age,"lost packet retried with same identity and growing age");
  box.Acknowledge(Evidence(t,999),"confirmed");Check(box.Pending,"unrelated receipt cannot clear pending blink");box.Acknowledge(retry,"confirmed");Check(!box.Pending&&box.Status.Contains("confirmed"),"correct receipt ends retries");
  box.Record(state,3,20);Check(box.Next(state,24)==null&&!box.Pending&&box.Status.Contains("timed out"),"bounded delivery timeout is visible");box.Record(state,4,30);state.duel++;Check(box.Next(state,30.1)==null&&!box.Pending,"new duel clears previous attempt");
  return n+" captured blink authority/delivery/compatibility checks passed";
 }
 public static string ProviderEvent()
 {
  n=0;var node=new GameObject("Capture then invalid fixture");var camera=node.AddComponent<StareMobileCamera>();int events=0;bool validAtEvent=false;double at=0;
  try
  {
   camera.BeginTestCapture();camera.BlinkCaptured+=(seq,time)=>{events++;validAtEvent=camera.Valid&&camera.Ready;at=time;};double now=Time.realtimeSinceStartupAsDouble;
   for(int i=0;i<=32;i++)camera.SubmitTestSample(0,0,true,now+i*.05,i+1);
   camera.SubmitTestBatchJson("{\"samples\":[{\"sequence\":34,\"timestamp\":"+(now+1.7).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"age\":0.15,\"valid\":true,\"left\":1,\"right\":1},{\"sequence\":35,\"timestamp\":"+(now+1.74).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"age\":0.11,\"valid\":true,\"left\":1,\"right\":1},{\"sequence\":36,\"timestamp\":"+(now+1.78).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"age\":0,\"valid\":false}]}");
   Check(events==1&&validAtEvent,"event captures eligible state before later invalid sample");Check(!camera.Valid&&camera.BlinkSequence==1,"latest invalid pose stays invalid without erasing event");Check(at<=Time.realtimeSinceStartupAsDouble&&Time.realtimeSinceStartupAsDouble-at<.3,"event uses local capture age");return n+" camera event provenance checks passed";
  }
  finally{camera.StopCamera();UnityEngine.Object.DestroyImmediate(node);}
 }
}
