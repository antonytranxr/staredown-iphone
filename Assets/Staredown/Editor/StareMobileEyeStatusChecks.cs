using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Staredown;

public static class StareMobileEyeStatusChecks
{
 static int n;static void Check(bool ok,string label){if(!ok)throw new Exception("Mobile eye status: "+label);n++;}
 static Tournament Room(int count=2){var t=new Tournament();for(int id=0;id<count;id++){t.Add(id,"Player "+id).phoneCamera=id==0;t.Input(id,0,0,true,true,0,0,0);}Check(t.Start(),"calibrated room starts");return t;}
 static void Step(Tournament t,float dt,bool lost){foreach(var f in t.Fighters)t.Input(f.id,0,0,!(lost&&f.id==0),true,0,0,0);t.Tick(dt);}
 public static string Rules()
 {
  n=0;var t=Room(4);int round=t.Round;float previous=t.Remaining;
  for(int i=0;i<101&&t.Phase==MatchPhase.Countdown;i++){Step(t,.05f,true);Check(!t.TrackingPaused,"absent phone never pauses countdown");Check(t.Phase!=MatchPhase.Countdown||t.Remaining<previous,"countdown always decreases");previous=t.Remaining;}
  Check(t.Phase==MatchPhase.Duel&&t.Round==round&&t.Duel==1,"missing phone reaches same Go without restart");Check(t.Find(0).alive&&t.Find(0).faceMissingFor==0,"one-second face grace begins only after Go");
  Step(t,.4f,true);Check(t.Find(0).alive&&!t.TrackingPaused&&Mathf.Abs(t.Find(0).faceMissingFor-.4f)<.001f,"short missing face has grace while duel clock runs");
  var copy=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(t.Snapshot()));Check(Mathf.Abs(copy.fighters.First(f=>f.id==0).faceMissingFor-.4f)<.001f,"authoritative grace progress reaches phone HUD snapshot");
  Step(t,.2f,false);Check(t.Find(0).faceMissingFor==0&&t.Duel==1&&t.Round==round,"recovery clears grace without restarting the duel");
  Step(t,.6f,true);Step(t,.45f,true);Check(!t.Find(0).alive&&t.Find(0).status.StartsWith("FACE LOST"),"sustained tracking loss forfeits phone only");Check(t.Phase==MatchPhase.Duel&&t.Round==round&&!t.TrackingPaused,"another duel keeps going without reset");
  t=Room(4);while(t.Phase==MatchPhase.Countdown)Step(t,.05f,false);foreach(var pair in t.Pairs){int loser=pair.a==0?pair.b:pair.a;t.Blink(loser);}Step(t,.1f,false);Check(t.Find(0).alive&&t.Phase==MatchPhase.Intermission,"phone advances to next round");t.Find(0).phoneCamera=false;Step(t,4.1f,false);Check(t.Phase==MatchPhase.Countdown&&t.Find(0).phoneTrackingRequired,"camera-off cannot change phone into headset between rounds");Step(t,.1f,true);Check(!t.TrackingPaused,"disabled camera cannot freeze next round countdown");
  var off=new Tournament();off.Add(0,"Phone");off.Add(1,"Headset");off.Input(0,0,0,true,true,0,0,0,false,true);off.Input(1,0,0,true,true,0,0,0);Check(off.Start()&&off.Find(0).phoneTrackingRequired,"phone capability survives video profile switching at admission");Step(off,.1f,true);Check(!off.TrackingPaused,"admission race cannot make missing phone tracking pause the countdown");
  Check(StareMobileHud.EyeLabel(false,0)=="LOST","invalid tracking cannot show open eyes");Check(StareMobileHud.EyeLabel(true,0)=="OPEN"&&StareMobileHud.EyeLabel(true,.45f)=="CLOSING"&&StareMobileHud.EyeLabel(true,.8f)=="CLOSED","eye indicators use real closure thresholds");
  return n+" uninterrupted mobile countdown/grace/identity/eye status checks passed";
 }
 public static string Hud()
 {
  n=0;var p=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();if(p==null)throw new Exception("Start phone Play scene first");var hud=p.GetComponent<StareMobileHud>();var face=p.MobileCamera;var state=p.State;int id=p.LocalId;bool wasEnabled=p.enabled;
  Action tick=()=>typeof(StareMobileHud).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
  try
  {
   p.Leave();p.enabled=false;face.BeginTestCapture();var t=Room();typeof(StareMobileSession).GetProperty("LocalId").SetValue(p,0);typeof(StareMobileSession).GetProperty("State").SetValue(p,t.Snapshot());
   long seq=1;double at=10;face.SubmitTestSample(0,0,true,at,seq++);face.SubmitTestSample(0,0,true,at+=1.6,seq++);
   foreach(bool landscape in new[]{false,true})
   {
    t.Phase=MatchPhase.Countdown;typeof(StareMobileSession).GetProperty("State").SetValue(p,t.Snapshot());
    var pixels=landscape?new Vector2(1600,900):new Vector2(900,1600);var safe=landscape?new Rect(65,32,1470,868):new Rect(0,50,900,1460);hud.SetReviewViewport(pixels,safe);tick();Canvas.ForceUpdateCanvases();
    Check(hud.CompetitionPreview&&hud.PreviewExpanded&&!hud.CollapsedPreview,"contest auto-expands preview in both orientations");Check(hud.CameraPreviewRect.rect.height>=166&&hud.CameraPanelRect.rect.width>=240,"selfie is larger during countdown");Check(hud.EyeStatus=="Eyes open"&&hud.LeftEyeStatus=="L · OPEN"&&hud.RightEyeStatus=="R · OPEN","open tracked eyes get clear live status");Check(hud.EyeGuidance.Contains("until GO"),"countdown explicitly permits blinking");
    hud.TogglePreview();tick();Check(hud.CompetitionPreview&&hud.PreviewExpanded,"accidental selfie tap cannot hide match feedback");
    face.SubmitTestSample(.81f,0,true,at+=.02,seq++);tick();Check(hud.LeftEyeStatus=="L · CLOSED"&&hud.RightEyeStatus=="R · OPEN","each eye updates independently");face.SubmitTestSample(0,0,false,at+=.02,seq++);tick();Check(hud.EyeStatus=="Eyes not found"&&hud.LeftEyeStatus.Contains("LOST")&&hud.EyeGuidance.Contains("before GO"),"tracking loss is explicit with no countdown reset promise");
    t.Phase=MatchPhase.Duel;t.Find(0).faceMissingFor=.65f;typeof(StareMobileSession).GetProperty("State").SetValue(p,t.Snapshot());tick();Check(hud.TrackingGraceRemaining<=.35f+.001f&&hud.EyeGuidance.Contains("you're out"),"lost tracking shows host grace and clear consequence");
    face.SubmitTestSample(0,0,true,at+=.02,seq++);tick();Check(hud.EyeStatus=="Eyes open"&&hud.TrackingGraceRemaining==1,"fresh eyes clear local warning immediately");
    t.Phase=MatchPhase.Countdown;
   }
   t.Find(0).alive=false;t.Phase=MatchPhase.Duel;typeof(StareMobileSession).GetProperty("State").SetValue(p,t.Snapshot());tick();Check(!hud.CompetitionPreview&&hud.CollapsedPreview,"eliminated players get compact camera back");
   return n+" mobile competition selfie/live eye/grace/portrait/landscape UI checks passed";
  }
  finally{p.Leave();face.StopCamera();p.enabled=wasEnabled;typeof(StareMobileSession).GetProperty("State").SetValue(p,state);typeof(StareMobileSession).GetProperty("LocalId").SetValue(p,id);hud.ClearReviewViewport();}
 }
}
