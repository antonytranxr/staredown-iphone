using System;
using System.Linq;
using UnityEngine;
using Staredown;
public static class StareExperienceChecks
{
 public static string Run()
 {
  var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();var game=UnityEngine.Object.FindFirstObjectByType<StareGame>();if(view==null||game==null)throw new Exception("Play the Quest scene first");int n=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);n++;};
  check(!UnityEditor.PlayerSettings.SplashScreen.show&&!UnityEditor.PlayerSettings.SplashScreen.showUnityLogo,"Splash disabled");
  check(StareShortcutMenu.Visible(false,false,false,false,true,false,false),"Controller menu without tracked hands");
  check(StareShortcutMenu.Visible(false,false,true,false,false,true,false),"Controller menu available during spectator idle");
  check(!StareShortcutMenu.Visible(false,true,true,true,true,true,false),"Drawing sling hides shortcut");
  check(!StareShortcutMenu.Visible(false,false,true,true,false,false,false),"Hand shortcut cannot crowd sling");
  check(StareShortcutMenu.Visible(true,false,false,true,false,false,true),"Wrist dance picker keeps its anchor visible");
  check(!StareShortcutMenu.Visible(true,false,false,true,true,true,false),"Open main menu hides duplicate shortcut");
  var reflection=StareArenaMirror.Reflection(Vector3.right,new Vector3(4,0,0));var point=new Vector3(2,1,3);check(Vector3.Distance(reflection.MultiplyPoint(point),new Vector3(6,1,3))<.001f,"Mirror reflects point across plane");check(Vector3.Distance((reflection*reflection).MultiplyPoint(point),point)<.001f,"Double reflection restores point");
  var fixture=new GameObject("Body fit verification");var viewer=new GameObject("Body fit viewer").transform;
  try
  {
   for(int i=0;i<9;i++)
   {
    var actor=view.CreateAvatar(i,null,fixture.transform);var animator=actor.preset.GetComponent<Animator>();if(animator!=null){animator.Rebind();animator.Update(0);}
    var body=actor.root.gameObject.AddComponent<StareSelfAvatar>();body.Initialize(actor);body.SetViewer(viewer);
    foreach(float height in new[]{1.15f,1.75f}){viewer.position=new Vector3(3,height,-2);body.Align();float y=(actor.eyes.left.position.y+actor.eyes.right.position.y)*.5f;check(Mathf.Abs(y-height)<.05f,"Eye fit kind="+i+" height="+height);check(actor.root.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.enabled),"Whole body skins remain enabled kind="+i);}
    UnityEngine.Object.Destroy(actor.root.gameObject);
   }
  }
  finally{UnityEngine.Object.Destroy(fixture);UnityEngine.Object.Destroy(viewer.gameObject);}
  var head=game.rig.centerEyeAnchor;var anchor=new GameObject("Tilted dance button").transform;anchor.position=head.position+head.forward*.7f-Vector3.up*.4f;anchor.rotation=Quaternion.Euler(45,head.eulerAngles.y,20);anchor.localScale=Vector3.one*.65f;
  try{view.ShowDanceChoices(head,true,anchor);var picker=anchor.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Quick dance picker");check(Vector3.Dot(picker.up,Vector3.up)>.999f,"Dance picker stays upright despite button tilt");check(Mathf.Abs(picker.lossyScale.x-1)<.005f,"Dance picker physical size unchanged");check(picker.position.y>=head.position.y-.351f,"Dance picker raised into readable range");view.ShowDanceChoices(head,false);}finally{UnityEngine.Object.Destroy(anchor.gameObject);}
  check(view.ArenaFeedMode&&view.ArenaFeedReady,"Exploration jumbotron captures arena");check(view.boardLeft.transform.localPosition.z<-1.5f&&view.boardRight.transform.localPosition.z<-1.5f,"Name labels in front of curved display");
  string saved=view.boardLeft.text;view.boardLeft.text="A VERY LONG CONTESTANT NAME / HUMAN";StareVisuals.FitBoardLabel(view.boardLeft,7.8f,.052f);check(view.boardLeft.characterSize<=.052f,"Long contestant name scales down");view.boardLeft.text=saved;
  var mirror=view.GetComponentInChildren<StareArenaMirror>();check(mirror!=null,"Arena mirror exists");return n+" experience checks passed";
 }
 public static string DanceHands()
 {
  var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();var fixture=new GameObject("Animated dance hands verification");int n=0;
  Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  try
  {
   for(int kind=0;kind<9;kind++)
   {
    var actor=view.CreateAvatar(kind,null,fixture.transform);var animator=actor.preset.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
    var fingers=actor.preset.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("hand.")||t.name.StartsWith("Hand_")||t.name.Contains("finger")||t.name.Contains("Finger")).ToArray();
    check(fingers.Length>=2,"Dance skeleton has hands "+kind);
    var fighter=new Fighter{bot=false,dancing=true,presenceAt=0,presence=new StarePresenceFrame{left=new StareHandPose{tracked=true,position=new Vector3(-.4f,-.2f,.4f),rotation=Quaternion.Euler(0,70,0)},right=new StareHandPose{tracked=true,position=new Vector3(.4f,-.2f,.4f),rotation=Quaternion.Euler(0,-70,0)}}};var state=new ArenaState{clock=0};
    // First establish tracking, then select a dance: no remaining IK blend may distort its hands.
    fighter.dancing=false;actor.presence.Observe(fighter,state,false);actor.presence.SendMessage("LateUpdate");fighter.dancing=true;
    foreach(string clip in new[]{"Dance","Dance1","Dance2","Dance3","Dance4","Dance5","Dance6","Dance7"})
    {
     check(animator.HasState(0,Animator.StringToHash(clip)),"Dance state exists "+kind+" / "+clip);
     for(int sample=0;sample<3;sample++)
     {
      animator.Play(clip,0,(sample+.25f)/3);animator.Update(.01f);
      var positions=fingers.Select(t=>t.position).ToArray();var rotations=fingers.Select(t=>t.rotation).ToArray();
      actor.presence.Observe(fighter,state,false);actor.presence.SendMessage("LateUpdate");
      check(actor.presence.UsesDanceHands&&fingers.Select((t,i)=>Vector3.Distance(t.position,positions[i])<.0001f&&Quaternion.Angle(t.rotation,rotations[i])<.01f).All(v=>v),"Tracked poses do not overwrite dance hands/fingers "+kind+" / "+clip+" / "+sample);
      Pose left,right;check(actor.presence.TryDanceHand(true,out left)&&actor.presence.TryDanceHand(false,out right)&&left.position!=right.position,"Distinct animation targets available to Meta input "+kind);
     }
    }
    animator.Play("Idle",0,0);animator.Update(.01f);var idle=fingers.Select(t=>t.position).ToArray();fighter.dancing=false;actor.presence.Observe(fighter,state,false);
    for(int i=0;i<12;i++){animator.Update(.02f);actor.presence.SendMessage("LateUpdate");}
    check(!actor.presence.UsesDanceHands&&fingers.Select((t,i)=>Vector3.Distance(t.position,idle[i])>.005f).Any(v=>v),"Stopping dance restores tracked arms "+kind);
    UnityEngine.Object.Destroy(actor.root.gameObject);
   }
   return n+" dance hand checks passed";
  }
  finally{UnityEngine.Object.Destroy(fixture);}
 }
 public static string Render(string path,Vector3 position,Vector3 focus)
 {
  var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();var node=new GameObject("Experience visual review");var camera=node.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(focus);camera.fieldOfView=65;camera.nearClipPlane=.05f;camera.farClipPlane=150;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.enabled=false;camera.cullingMask=~((1<<2)|(1<<26));
  var target=new RenderTexture(1600,900,24);target.Create();camera.targetTexture=target;var saved=RenderTexture.active;
  try{var mirror=view.GetComponentInChildren<StareArenaMirror>();if(mirror!=null)mirror.Capture(camera);camera.Render();RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();System.IO.File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.Destroy(image);return path;}
  finally{RenderTexture.active=saved;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(node);}
 }
}
