using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
 public static class StareThrowAssist
 {
  public static bool CanThrow(ArenaState state,int id)
  {var fighter=Array.Find(state.fighters,f=>f.id==id);return fighter!=null&&!fighter.alive&&fighter.status!="DISCONNECTED"&&state.phase==(int)MatchPhase.Duel;}
  public static Vector3 Velocity(Vector3 origin,Vector3 target)
  {var delta=target-origin;float time=Mathf.Max(.5f,new Vector2(delta.x,delta.z).magnitude/10f);return Vector3.ClampMagnitude(delta/time+Vector3.up*(4.9f*time),14);}
  public static Vector3 Target(ArenaState state,Vector3 origin,Vector3 forward)
  {
   Vector3 target=origin+forward.normalized*14;float best=.94f;
   foreach(var fighter in state.fighters)if(fighter.alive&&fighter.pair>=0&&fighter.pair<state.pairs.Length&&state.pairs[fighter.pair].winner<0)
   {var at=ArenaLayout.Seat(state,fighter.id)+Vector3.up*1.4f;float dot=Vector3.Dot(forward.normalized,(at-origin).normalized);if(dot>best){best=dot;target=at;}}
   return target;
  }
  public static Vector3 SlingshotVelocity(ArenaState state,Vector3 origin,Vector3 direction,float strength)
  {
   // A little loft makes arena-distance shots comfortable, but never snaps to a fighter.
   // Both the preview and the networked projectile use this same launch velocity.
   var aim=direction.sqrMagnitude>.0001f?direction.normalized:Vector3.forward;
   return (aim+Vector3.up*.28f).normalized*Mathf.Lerp(6f,14f,Mathf.Clamp01(strength));
  }
 }
 // Calibrate the initial grab offset, then aim from the live fork-to-pouch displacement.
 // The small preload keeps aim stable near the fork without using a noisy UI pinch ray.
 public sealed class StareSlingshotDraw
 {
  Vector3 separation,initialForward;public Vector3 Direction{get;private set;}public bool Active{get;private set;}
  public Vector3 PouchPosition{get;private set;}public float Pull{get;private set;}public float Strength=>Mathf.Clamp01(Pull/.25f);
  public void Begin(Vector3 fork,Vector3 hand,Vector3 direction)
  {separation=fork-hand;initialForward=direction.sqrMagnitude>.0001f?direction.normalized:Vector3.forward;Direction=initialForward;Pull=0;PouchPosition=hand;Active=true;}
  public void Sample(Vector3 fork,Vector3 hand)
  {
   if(!Active)return;
   PouchPosition=hand;var displacement=fork-hand-separation;
   Pull=Mathf.Clamp(Vector3.Dot(displacement,initialForward),0,.5f);
   var aim=initialForward*.06f+displacement;
   if(aim.sqrMagnitude>.0001f)Direction=aim.normalized;
  }
  public bool Release(out float strength){strength=Strength;bool fire=Active&&Pull>=.08f;Cancel();return fire;}
  public void RotateWithRig(Quaternion rotation)
  {separation=rotation*separation;initialForward=rotation*initialForward;Direction=rotation*Direction;}
  public void Cancel(){Active=false;Pull=0;}
 }
 public sealed class StareSpectatorGuide
 {
  bool wasSpectator,wasFinal;float until;
  public void Remind(float now){until=now+6;}
  public bool Sample(bool spectator,bool final,bool menu,float now)
  {
   if(spectator&&(!wasSpectator||final&&!wasFinal))Remind(now);
   if(!spectator)until=0;wasSpectator=spectator;wasFinal=final;
   return spectator&&!menu&&now<until;
  }
 }
 public sealed class StareSpectatorTools : MonoBehaviour
 {
  struct HandInput {public bool tracked,down,allowed,controller;public Vector3 position;public Ray aim;}
  StareGame game;StareVisuals view;readonly HandInput[] inputs=new HandInput[2];readonly bool[] previousDown=new bool[2];
  readonly StareSlingshotDraw draw=new StareSlingshotDraw();readonly StareSpectatorGuide guide=new StareSpectatorGuide();
  Transform frame,pouch;TextMesh cue;LineRenderer cord,trajectory;Material lineMaterial;Vector3 fork,lastForward=Vector3.forward;int prop;bool hasAim;float nextToss,nextDebug;
  public int LocalTosses{get;private set;}public bool Drawing=>draw.Active;public bool Equipped=>frame!=null&&frame.gameObject.activeSelf;public Vector3 GripPosition=>frame.TransformPoint(new Vector3(0,-.13f,0));public Vector3 BallPosition=>pouch.position;public float DrawStrength=>draw.Strength;
  public bool CanThrow=>game!=null&&StareThrowAssist.CanThrow(game.State,game.LocalId);
  public void RotateWithRig(Quaternion rotation){lastForward=rotation*lastForward;draw.RotateWithRig(rotation);}
  public string InputDebug=>"SLING L "+(inputs[0].tracked?"OK":"LOST")+" R "+(inputs[1].tracked?"OK":"LOST")+" "+(inputs[1].down?"GRAB":"OPEN")+" "+Vector3.Distance(inputs[1].position,fork).ToString("0.00")+"m";
  public float Cooldown=>Mathf.Max(0,nextToss-Time.unscaledTime);
  public void Initialize(StareGame owner,StareVisuals visuals)
  {
   game=owner;view=visuals;frame=new GameObject("Spectator slingshot").transform;frame.SetParent(transform,false);
   var metal=view.Mat("Slingshot / ceramic pearl",new Color(.66f,.80f,.89f));var grip=view.Mat("Slingshot / graphite grip",new Color(.025f,.07f,.12f));var gold=view.Mat("Slingshot / luminous fork",StareVisuals.Gold,true);
   view.Shape(PrimitiveType.Capsule,"Slingshot handle",frame,new Vector3(0,-.13f,0),new Vector3(.050f,.10f,.050f),grip);
   for(int side=-1;side<=1;side+=2)
   {
    var arm=view.Shape(PrimitiveType.Capsule,"Slingshot swept fork",frame,new Vector3(side*.055f,.008f,0),new Vector3(.038f,.085f,.038f),metal);arm.transform.localRotation=Quaternion.Euler(0,0,-side*24);
    view.Shape(PrimitiveType.Sphere,"Elastic anchor light",frame,new Vector3(side*.085f,.080f,0),Vector3.one*.04f,gold);
   }
   pouch=view.Shape(PrimitiveType.Sphere,"Slingshot charged projectile",transform,Vector3.zero,Vector3.one*.070f,metal).transform;
   cue=view.Text("",transform,Vector3.zero,.016f,StareVisuals.Cyan,TextAnchor.MiddleCenter);
   lineMaterial=new Material(Resources.Load<Shader>("StareGlow")){name="Slingshot elastic and flight path",color=Color.white};
   cord=Line("Slingshot elastic",.009f);cord.positionCount=3;trajectory=Line("Slingshot predicted flight",.005f);trajectory.positionCount=48;
   Hide();
  }
  LineRenderer Line(string name,float width)
  {var root=new GameObject(name);root.layer=2;root.transform.SetParent(transform,false);var line=root.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.widthMultiplier=width;line.numCapVertices=4;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;}
  public void BeginInputFrame(){for(int i=0;i<2;i++)inputs[i]=default;}
  public void SampleInput(int side,bool down,bool tracked,Vector3 position,Ray aim,bool allowed,bool controller=false)
  {inputs[side]=new HandInput{tracked=tracked,down=down,position=position,aim=aim,allowed=allowed,controller=controller};}
  public static bool NearBall(Vector3 hand,Vector3 ball,bool controller)=>Vector3.Distance(hand,ball)<=(controller?.32f:.22f);
  public void TickInput()
  {
   var support=inputs[0];var hand=inputs[1];
   if(CanThrow&&Debug.isDebugBuild&&Time.unscaledTime>nextDebug){nextDebug=Time.unscaledTime+2;Debug.Log("[STARE SLING INPUT] "+InputDebug+" controllerL="+support.controller+" controllerR="+hand.controller+" menu="+game.MatchMenuOpen); }
   if(!CanThrow||game.MatchMenuOpen||!support.tracked||!support.allowed)
   {draw.Cancel();hasAim=false;Hide();RememberButtons();return;}
   if(!draw.Active&&support.aim.direction.sqrMagnitude>.5f){lastForward=support.aim.direction.normalized;hasAim=true;}
   if(!hasAim){lastForward=game.rig.centerEyeAnchor.forward;hasAim=true;}
   var rotation=Quaternion.LookRotation(lastForward,Vector3.up);
   // The handle centre sits in the left palm; the loaded ball is above the grip.
   frame.SetPositionAndRotation(support.position+rotation*Vector3.up*.13f,rotation);
   fork=frame.TransformPoint(new Vector3(0,.08f,-.035f));
   if(draw.Active)
   {
    if(!hand.tracked||!hand.allowed)draw.Cancel();
    else
    {
     draw.Sample(fork,hand.position);
     if(!hand.down){float strength;if(draw.Release(out strength))Launch(fork,StareThrowAssist.SlingshotVelocity(game.State,fork,draw.Direction,strength));}
    }
   }
   bool near=hand.tracked&&NearBall(hand.position,fork,hand.controller);
   if(!draw.Active&&hand.tracked&&hand.allowed&&hand.down&&!previousDown[1]&&Cooldown<=0&&near)
   {draw.Begin(fork,hand.position,lastForward);StareFeedback.Instance?.TossFeedback();}
   RememberButtons();frame.gameObject.SetActive(true);pouch.gameObject.SetActive(true);
   // Once grabbed, the pouch stays at the real right pinch, including sideways motion.
   var ball=draw.Active?draw.PouchPosition:fork;pouch.position=ball;
   var color=Color.Lerp(StareVisuals.Cyan,StareVisuals.Gold,draw.Strength);cord.enabled=true;cord.startColor=cord.endColor=color;
   cord.SetPosition(0,frame.TransformPoint(new Vector3(-.085f,.080f,0)));cord.SetPosition(1,ball);cord.SetPosition(2,frame.TransformPoint(new Vector3(.085f,.080f,0)));
   cue.gameObject.SetActive(!game.MatchMenuOpen);cue.transform.position=frame.position+frame.right*.23f+frame.up*.13f;
   cue.transform.rotation=game.rig.centerEyeAnchor.rotation;
   string hint=Cooldown>0?"RELOADING":draw.Active?(draw.Pull>=.08f?"RELEASE TO FIRE":"PULL TOWARD YOU"):!hand.tracked?"RAISE RIGHT HAND":near?(hand.controller?"HOLD RIGHT TRIGGER":"PINCH OR GRAB"):"RIGHT HAND: GRAB BALL";
   if(cue.text!=hint){cue.text=hint;cue.characterSize=.010f*.42f;var size=cue.GetComponent<Renderer>().localBounds.size;cue.characterSize*=Mathf.Min(1,.24f/Mathf.Max(.001f,size.x),.035f/Mathf.Max(.001f,size.y));}
   trajectory.enabled=draw.Active&&draw.Pull>=.08f;
   if(trajectory.enabled)
   {
    var velocity=StareThrowAssist.SlingshotVelocity(game.State,fork,draw.Direction,draw.Strength);trajectory.startColor=color;trajectory.endColor=new Color(color.r,color.g,color.b,.15f);
    trajectory.positionCount=48;int count=0;for(int i=0;i<48;i++){float t=i*.085f;var p=fork+velocity*t+Vector3.down*4.9f*t*t;trajectory.SetPosition(i,p);count++;if(p.y<.04f)break;}trajectory.positionCount=count;
   }
  }
  void RememberButtons(){for(int i=0;i<2;i++)previousDown[i]=inputs[i].down||!inputs[i].tracked;}
  bool Launch(Vector3 origin,Vector3 speed)
  {
   if(!CanThrow||Cooldown>0)return false;
   game.SendToss(origin,speed,prop++%3);nextToss=Time.unscaledTime+.85f;LocalTosses++;
   StareFeedback.Instance?.TossFeedback();if(Debug.isDebugBuild)Debug.Log("[STARE SLINGSHOT] launched="+LocalTosses+" origin="+origin.ToString("F2")+" velocity="+speed.ToString("F2"));return true;
  }
  public void TossFromHead()
  {if(Application.isMobilePlatform)return;var head=game.rig.centerEyeAnchor;Launch(head.position+head.forward*.25f-Vector3.up*.18f,StareThrowAssist.SlingshotVelocity(game.State,head.position,head.forward,.8f));}
  public void ShowHelp(){guide.Remind(Time.unscaledTime);}
  void Update()
  {
   bool spectator=Array.Exists(game.State.fighters,f=>f.id==game.LocalId&&!f.alive&&f.status!="DISCONNECTED")&&game.State.phase!=(int)MatchPhase.Champion;
   bool shown=guide.Sample(spectator,game.State.finalRound,game.MatchMenuOpen,Time.unscaledTime)&&!Drawing;
   view.UpdateThrowTutorial(shown,game.State.finalRound,CanThrow,Cooldown,LocalTosses,game.rig.centerEyeAnchor);
  }
  void Hide(){if(cue!=null)cue.gameObject.SetActive(false);if(frame!=null)frame.gameObject.SetActive(false);if(pouch!=null)pouch.gameObject.SetActive(false);if(cord!=null)cord.enabled=false;if(trajectory!=null)trajectory.enabled=false;}
  void OnDisable(){draw.Cancel();Hide();}void OnDestroy(){if(lineMaterial!=null)Destroy(lineMaterial);}
 }
 public sealed partial class StareVisuals
 {
  Transform throwTutorial;TextMesh throwTitle,throwInstructions;
  static void FitTutorialText(TextMesh label,string value,float width,float height,bool force=false)
  {if(!force&&label.text==value)return;label.text=value;label.characterSize=.015f*.42f;var size=label.GetComponent<Renderer>().localBounds.size;label.characterSize*=Mathf.Min(1,width/Mathf.Max(.001f,size.x),height/Mathf.Max(.001f,size.y));}
  public void UpdateThrowTutorial(bool shown,bool final,bool enabled,float cooldown,int thrown,Transform head)
  {
   if(throwTutorial==null)
   {
    throwTutorial=Group("Spectator throwing guide",head,new Vector3(0,-.28f,1.05f));
    Panel("Spectator guide",throwTutorial,Vector3.zero,new Vector2(.78f,.28f),UISurface,Gold,.035f);
    throwTitle=UILabel("",throwTutorial,new Vector3(-.35f,.087f,-.015f),.022f,Gold,true);
    throwInstructions=UILabel("",throwTutorial,new Vector3(-.35f,-.005f,-.015f),.018f,UIInk);
    FitTutorialText(UILabel("",throwTutorial,new Vector3(-.35f,-.097f,-.02f),.012f,UIMuted),"Free refills / B, Y or hold left pinky pinch: menu",.70f,.025f);
   }
   throwTutorial.gameObject.SetActive(shown);if(!shown)return;
   FitTutorialText(throwTitle,final?"FINAL / SLINGSHOT DISTRACTIONS":"SPECTATOR / SLINGSHOT READY",.70f,.035f);
   FitTutorialText(throwInstructions,"Left hand: hold handle; thumb swipe to move / turn.\nRight: grab ball, pull back, aim the arc, release.\nControllers: left stick moves; right trigger grabs.",.70f,.105f);
  }
 }
}
