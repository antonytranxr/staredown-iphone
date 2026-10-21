using System;
using System.Reflection;
using UnityEngine;
using Staredown;

// Synchronous Play-mode fixture: no Relay, camera permission or headset needed.
public static class StarePresentationParityChecks
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 public static string Run()
 {
  int count=0,originalRate=Application.targetFrameRate,originalSleep=Screen.sleepTimeout;bool background=Application.runInBackground,muted=UnityEditor.EditorUtility.audioMasterMute;
  Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Presentation parity: "+label);count++;};
  var fixture=new GameObject("Crossplatform presentation checks");UnityEditor.EditorUtility.audioMasterMute=true;
  try
  {
   var cameraRoot=new GameObject("Ordinary phone camera");cameraRoot.transform.SetParent(fixture.transform,false);
   var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,1.6f,-6);
   var phone=fixture.AddComponent<StareMobileSession>();phone.enabled=false;phone.ViewCamera=camera;
   var voice=fixture.AddComponent<StareAnnouncer>();voice.enabled=false;voice.Initialize(phone);
   int sources=fixture.GetComponents<AudioSource>().Length;voice.Initialize(phone);
   check(fixture.GetComponents<AudioSource>().Length==sources,"phone announcer initialization is idempotent");
   check(voice.VoiceVolume>.8f&&!voice.VoiceMuted,"phone announcer has its own audible voice source");
   var music=fixture.AddComponent<AudioSource>();music.mute=true;music.volume=0;
   check(!voice.VoiceMuted&&voice.VoiceVolume>.8f,"muted music does not mute announcer");
   foreach(string cue in new[]{"Welcome","Round","Final","Three","Two","One","Fight","Eliminated","Complete","Champion","Pause","Resume"})
    check(Resources.Load<AudioClip>("Audio/Announcer/"+cue)!=null,"shared announcer cue imported: "+cue);
   var cues=new StareAnnouncerCues();var state=new ArenaState{phase=(int)MatchPhase.Countdown,remaining=4,round=1,
    fighters=new[]{new Fighter{id=0,ready=true,valid=true,pair=0},new Fighter{id=1,ready=true,valid=true,pair=0}},pairs=new[]{new Pairing{a=0,b=1}}};
   check(cues.Next(state)=="Round","phone countdown announces round");state.remaining=3;check(cues.Next(state)=="Three","phone countdown announces three");
   state.phase=(int)MatchPhase.Duel;state.remaining=40;check(cues.Next(state)=="Fight","phone round announces fight");
   var bees=fixture.AddComponent<StareDistractions>();bees.enabled=false;bees.Initialize(phone);sources=fixture.GetComponentsInChildren<AudioSource>().Length;
   bees.Initialize(phone);check(fixture.GetComponentsInChildren<AudioSource>().Length==sources,"phone bees initialize once");
   for(int i=0;i<40;i++)bees.Observe(state,0,camera.transform,.05f);
   check(bees.LocalBees==1&&bees.ActiveBees>=1,"phone competitor sees first-stage bee");
   state.remaining=25;bees.Observe(state,0,camera.transform,.05f);
   check(bees.LocalBees==3&&bees.ActiveBees<=StareDistractions.MaxVisibleBees,"phone receives all staged bees within shared budget");
   state.fighters[0].alive=false;bees.Observe(state,0,camera.transform,.05f);
   check(bees.LocalBees==0,"eliminated phone has no bees at its own eyes");
   state.phase=(int)MatchPhase.Lobby;bees.Observe(state,0,camera.transform,.05f);
   check(bees.ActiveBees==0&&bees.BuzzVolume==0,"lobby clears phone insects and buzz");
   var fireworks=fixture.AddComponent<StareFireworks>();fireworks.Initialize(camera.transform,voice);
   sources=fixture.GetComponentsInChildren<AudioSource>().Length;fireworks.Initialize(camera.transform,voice);
   check(fixture.GetComponentsInChildren<AudioSource>().Length==sources,"phone fireworks initialize once");
   state.phase=(int)MatchPhase.Champion;state.winner=1;int peak=0;
   for(int i=0;i<160;i++)
   {state.remaining=8-i*.05f;fireworks.Observe(state);fireworks.Advance(.05f);peak=Mathf.Max(peak,fireworks.ActiveParticles);
    check(fireworks.ActiveParticles<=StareFireworks.Capacity&&fireworks.ActiveRockets<=StareFireworks.RocketCapacity,"bounded phone celebration frame "+i);}
   check(fireworks.Launches==40&&fireworks.Bursts==40&&peak>650,"phone receives the full shared championship show");
   state.phase=(int)MatchPhase.Lobby;fireworks.Observe(state);fireworks.Advance(.05f);
   check(fireworks.ActiveParticles==0&&fireworks.ActiveRockets==0,"phone fireworks clear outside celebration");

   foreach(var gesture in new[]{OVRHand.MicrogestureType.SwipeForward,OVRHand.MicrogestureType.SwipeBackward,OVRHand.MicrogestureType.SwipeLeft,OVRHand.MicrogestureType.SwipeRight})
   {
    check(!StareSlingshotGesture.Reserved(gesture,1,false,true,true),"equipped sling permits right "+gesture);
    check(!StareSlingshotGesture.Reserved(gesture,0,true,true,true),"drawing sling permits left "+gesture);
    check(StareSlingshotGesture.Reserved(gesture,1,true,true,false),"drawing reserves right "+gesture);
   }
   check(!StareSlingshotGesture.Reserved(OVRHand.MicrogestureType.ThumbTap,1,false,true,false),"right teleport tap works away from ball");
   check(StareSlingshotGesture.Reserved(OVRHand.MicrogestureType.ThumbTap,1,false,true,true),"ball grab does not accidentally open teleport");
   check(!StareSlingshotGesture.Reserved(OVRHand.MicrogestureType.ThumbTap,0,false,true,true),"left teleport remains available with equipped sling");

   check(StareArenaMirror.TargetRefreshRate(false,0,false)==24&&StareArenaMirror.TargetRefreshRate(false,0,true)==12,"Quest mirror has faster target and adaptive fallback");
   check(StareArenaMirror.TargetRefreshRate(true,0,false)==15&&StareArenaMirror.TargetRefreshRate(true,0,true)==6&&StareArenaMirror.TargetRefreshRate(true,2,false)==3,"phone mirror increases cadence while preserving heat fallback");
   foreach(var scenario in new[]{new Vector2(72,24),new Vector2(30,15)})
   {
    float next=0;int captures=0;
    for(int frame=0;frame<(int)scenario.x*10;frame++){float now=frame/scenario.x;if(now<next)continue;next=StareArenaMirror.NextCaptureTime(now,next,scenario.y);captures++;}
    check(Mathf.Abs(captures-scenario.y*10)<=2,"mirror scheduling keeps cadence "+scenario.x+" FPS / "+scenario.y+" captures");
    check(StareArenaMirror.NextCaptureTime(8,1,scenario.y)>8,"slow frame skips missed captures");
   }
   var mirror=fixture.AddComponent<StareArenaMirror>();mirror.enabled=false;var observe=typeof(StareArenaMirror).GetMethod("ObserveFrameBudget",Private);
   Action<float,int> frames=(dt,n)=>{for(int i=0;i<n;i++)observe.Invoke(mirror,new object[]{dt,72f});};
   frames(1/72f,90);check(mirror.RefreshRate==24,"normal frame budget retains faster mirror");
   frames(.03f,100);check(mirror.RefreshRate==12,"sustained slow frames reduce mirror cost");
   frames(1/72f,400);check(mirror.RefreshRate==24,"sustained recovery restores smoother mirror");
   typeof(StareArenaMirror).GetField("averageCapture",Private).SetValue(mirror,.012f);frames(1/72f,90);
   check(mirror.RefreshRate==12,"costly capture reduces mirror even before the game misses its frame budget");
   return count+" crossplatform bees / announcer / fireworks / spectator gesture / mirror checks passed; peak fireworks="+peak;
  }
  finally{UnityEngine.Object.DestroyImmediate(fixture);Application.targetFrameRate=originalRate;Application.runInBackground=background;Screen.sleepTimeout=originalSleep;UnityEditor.EditorUtility.audioMasterMute=muted;}
 }
}
