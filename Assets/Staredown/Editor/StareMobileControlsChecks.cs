#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Staredown;

public static class StareMobileControlsChecks
{
 sealed class CaptureTransport:IStareTransport
 {
  public readonly List<WireMessage> sent=new List<WireMessage>();
  public string Error=>null;public void Tick(){}public bool Poll(out StareNetwork.Incoming packet){packet=default;return false;}
  public void Send(int id,WireMessage message){sent.Add(message);}public void Broadcast(WireMessage message){sent.Add(message);}
  public void ClosePeer(int id){}public void Dispose(){}
 }
 static int n;static void Check(bool ok,string label){if(!ok)throw new Exception("Mobile controls: "+label);n++;}
 static void Tick(object owner){owner.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);}
 static void Pause(StareMobileSession session,bool paused){typeof(StareMobileSession).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{paused});}
 static Tournament Model(StareMobileSession session)=>(Tournament)typeof(StareMobileSession).GetField("model",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session);
 static void FreshFrame(StareMobileSession session,byte[] jpeg)
 {typeof(StareCameraStream).GetField("publishAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session.CameraHeads,-10f);session.MobileCamera.SubmitTestFrame(jpeg);}
 public static string Run()
 {
  n=0;var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();
  if(!Application.isPlaying||session==null||session.MobileCamera==null||session.Connected||session.Busy||session.MobileCamera.Running)throw new Exception("Run mobile controls checks in disconnected phone Play scene with camera off");
  var camera=session.MobileCamera;var hud=session.GetComponent<StareMobileHud>();
  string[] keys={"Staredown.MobileShareVideo","Staredown.MicMuted","Staredown.PlayerNameConfirmed"};
  bool[] had=keys.Select(PlayerPrefs.HasKey).ToArray();int[] old=keys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
  bool hadName=PlayerPrefs.HasKey("Staredown.PlayerName");string oldName=PlayerPrefs.GetString("Staredown.PlayerName","");
  bool oldEnabled=session.enabled;var jpeg=StareCameraChecks.TestJpeg();
  try
  {
   session.enabled=false;StarePlayerName.Save("Phone fixture");session.SetVideoSharing(true);camera.BeginTestCapture();
   double at=100;long seq=1;for(int i=0;i<50;i++)camera.SubmitTestSample(0,0,true,at+=.034,seq++);
   Check(camera.Running&&camera.Ready&&camera.Valid,"camera fixture calibrated independently of video sharing");
   var net=new CaptureTransport();session.TestConnect(net,true);var model=Model(session);model.SetBotFill(false);model.Add(1,"Opponent");model.Input(1,0,0,true,true,0,0,0);Tick(session);
   Check(session.VideoSending&&model.Find(0).phoneCamera,"normal lobby announces active video profile");
   FreshFrame(session,jpeg);Check(net.sent.Any(m=>m.kind=="cameraFrame"),"sharing publishes bounded camera frame");int sharedFrames=net.sent.Count(m=>m.kind=="cameraFrame");
   var originalPreview=camera.Preview;int blinkBefore=camera.BlinkSequence;session.SetVideoSharing(false);FreshFrame(session,jpeg);Tick(session);
   Check(!session.VideoSending&&!session.VideoSharingEnabled&&!model.Find(0).phoneCamera,"private video disables room profile immediately");
   Check(net.sent.Count(m=>m.kind=="cameraFrame")==sharedFrames&&net.sent.Last(m=>m.kind=="cameraProfile").camera==false,"private frames never enter publication path after throttle is reset");
   Check(camera.Running&&camera.CameraEnabledIntent&&camera.Ready&&camera.Valid&&ReferenceEquals(camera.Preview,originalPreview),"privacy keeps camera preview, calibration and live eye tracking intact");
   Check(model.Find(0).phoneTrackingRequired&&model.Find(0).valid,"private video retains authoritative phone eye identity and valid input");
   camera.SubmitTestSample(.9f,.9f,true,at+=.034,seq++);camera.SubmitTestSample(.9f,.9f,true,at+=.034,seq++);
   Check(camera.BlinkSequence==blinkBefore+1,"local native-rate blink detection continues while video is private");
   camera.SubmitTestSample(0,0,true,at+=.034,seq++);
   Pause(session,true);session.SetVideoSharing(true);FreshFrame(session,jpeg);Check(!session.VideoSending&&net.sent.Count(m=>m.kind=="cameraFrame")==sharedFrames,"background state blocks publication even if capture callback races pause");
   session.SetVideoSharing(false);Pause(session,false);Tick(session);FreshFrame(session,jpeg);Check(!session.VideoSending&&!session.VideoSharingEnabled&&camera.Running,"resume preserves private video intent and eye capture");
   session.Leave();var next=new CaptureTransport();session.TestConnect(next,true);model=Model(session);model.SetBotFill(false);model.Add(1,"Opponent");model.Input(1,0,0,true,true,0,0,0);Tick(session);FreshFrame(session,jpeg);
   Check(!session.VideoSending&&camera.Running&&next.sent.All(m=>m.kind!="cameraFrame"),"joining another room preserves privacy without another camera enable prompt");
   session.SetVideoSharing(true);Check(session.VideoSending&&model.Find(0).phoneCamera,"player can deliberately restore sharing");
   foreach(bool landscape in new[]{false,true})
   {
    hud.SetReviewViewport(landscape?new Vector2(1600,900):new Vector2(900,1600),landscape?new Rect(65,32,1470,868):new Rect(0,50,900,1460));Tick(hud);Canvas.ForceUpdateCanvases();
    Check(hud.RoomMicButton.gameObject.activeInHierarchy&&hud.RoomMicButton.GetComponent<RectTransform>().rect.height>=60,"microphone is directly accessible in room in either orientation");
    hud.ShowSettings(true);Tick(hud);Check(hud.ShareVideoButton.gameObject.activeInHierarchy,"video sharing is available in phone Settings");hud.ShowSettings(false);
   }
   session.Voice.EnableMicrophone();Check(!session.Voice.MicMuted&&PlayerPrefs.GetInt("Staredown.MicMuted")==0,"Enable mic always requests unmuted intent");
   var lifeState=new ArenaState{phase=(int)MatchPhase.Duel,livesMode=true,fighters=new[]{new Fighter{id=0,alive=true,lives=2,pair=0},new Fighter{id=1,alive=true,lives=3,pair=0}},pairs=new[]{new Pairing{a=0,b=1,loser=0,winner=1}}};
   Check(StareMobileHud.MatchCaption(lifeState,lifeState.fighters[0],"")=="Life lost · 2 lives left","losing one life cannot display advanced or a continuing stare instruction");
   Check(StareMobileHud.MatchCaption(lifeState,lifeState.fighters[1],"")=="Round won · 3 lives left","round winner sees remaining lives");
   lifeState.fighters[0].lives=1;Check(StareMobileHud.MatchCaption(lifeState,lifeState.fighters[0],"")=="Life lost · 1 life left","last life has clear singular status");
   var sling=session.Slingshot;Check(!sling.Begin(7,Vector2.zero),"active contestants cannot fire spectator slingshot");
   model.Phase=MatchPhase.Duel;model.Find(0).alive=false;model.Pairs.Add(new Pairing{a=0,b=1});model.Find(1).pair=0;typeof(StareMobileSession).GetProperty("State").SetValue(session,model.Snapshot());Tick(hud);
   Check(session.CanMove&&session.CanThrow&&hud.MovementControl.gameObject.activeInHierarchy&&hud.SlingshotPanelRect.gameObject.activeInHierarchy,"eliminated phone players can walk and use slingshot at once");
   Check(hud.MovementControl.Begin(1,Vector2.right*40)&&sling.Begin(2,Vector2.zero),"different fingers own movement and slingshot independently");
   Check(!sling.Begin(3,Vector2.zero),"third finger cannot steal a drawn slingshot");sling.Drag(3,Vector2.down*120);Check(sling.Pull==Vector2.zero,"other fingers cannot alter current draw");
   sling.Drag(2,new Vector2(0,-100));Vector3 center=sling.Velocity;Check(sling.Strength>.8f&&center.y>0,"pulling down supplies power and a raised launch angle");
   sling.Drag(2,new Vector2(80,-100));Vector3 side=sling.Velocity;Check(Vector3.Angle(center,side)>10,"sideways pull changes trajectory instead of fixed launch");
   Check(!sling.Release(3)&&sling.Drawing,"unrelated finger release cannot shoot");Vector3 origin=sling.Origin,velocity=sling.Velocity;
   Check(sling.Release(2)&&model.Tosses.Count==1&&Vector3.Distance(model.Tosses[0].origin,origin)<.0001f&&Vector3.Distance(model.Tosses[0].velocity,velocity)<.0001f,"release uses exactly the same authoritative origin and velocity as the preview");
   Check(session.MoveInput.sqrMagnitude>0&&hud.MovementControl.PointerId==1,"shooting does not cancel walking finger");
   Check(!sling.Begin(2,Vector2.zero)&&sling.Cooldown>0,"reload prevents rapid touch spam");hud.MovementControl.Release();
   Check(Vector3.Distance(StareMobileSlingshot.Flight(origin,velocity,1),StareProjectile.Position(new Toss{origin=origin,velocity=velocity,born=0},1))<.0001f,"touch arc matches host projectile gravity");
   Check(!session.SendToss(new Vector3(float.NaN,0,0),velocity,0),"invalid launch values never enter transport");
   Pause(session,true);Check(!session.CanThrow&&!sling.Drawing,"background pause cancels spectator controls");Pause(session,false);hud.ShowSettings(true);Check(!sling.Drawing&&session.MoveInput==Vector2.zero,"opening settings clears touch actions");hud.ShowSettings(false);
   return n+" mobile privacy/microphone/multitouch slingshot checks passed";
  }
  finally
  {
   Pause(session,false);session.Leave();camera.StopCamera();session.enabled=oldEnabled;hud.ShowSettings(false);hud.ClearReviewViewport();
   for(int i=0;i<keys.Length;i++){if(had[i])PlayerPrefs.SetInt(keys[i],old[i]);else PlayerPrefs.DeleteKey(keys[i]);}
   if(hadName)PlayerPrefs.SetString("Staredown.PlayerName",oldName);else PlayerPrefs.DeleteKey("Staredown.PlayerName");PlayerPrefs.Save();
   // Restore the voice component's cached preference as well as disk state.
   typeof(StareVoice).GetProperty("MicMuted").SetValue(session.Voice,PlayerPrefs.GetInt("Staredown.MicMuted",0)!=0);
  }
 }
}
#endif
