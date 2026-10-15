using UnityEngine;
namespace Staredown
{
 public sealed class StareNavigation : MonoBehaviour
 {
  StareGame game;OVRCameraRig rig;StareVisuals view;StareTeleport teleport;
  public bool TeleportAiming=>teleport!=null&&teleport.Aiming;OVRHand[] gestureHands;OVRHand.MicrogestureType[] previousGesture;readonly float[] gestureAt=new float[2];bool turnArmed=true;float hiddenFor;readonly StareMenuHold menuHold=new StareMenuHold();
  public void Initialize(StareGame owner,StareVisuals visuals)
  {game=owner;rig=owner.rig;view=visuals;gestureHands=rig.GetComponentsInChildren<OVRHand>(true);previousGesture=new OVRHand.MicrogestureType[gestureHands.Length];teleport=gameObject.AddComponent<StareTeleport>();teleport.Initialize(game);}
  void Update()
  {
   if(rig==null)return;
   if(game.IsSpectator)rig.transform.position+=StareArenaFields.Clamp(game.State,game.LocalFloor)-game.LocalFloor;
   bool left=StareControllerAim.TryRead(true,rig.trackingSpace,out _,out _,out _,out var move,out var recallLeft);
   bool right=StareControllerAim.TryRead(false,rig.trackingSpace,out _,out _,out _,out var turn,out var recallRight);
   if(left&&move.magnitude>.2f)Move(Vector2.ClampMagnitude(move,1)*1.2f*Mathf.Min(Time.unscaledDeltaTime,.05f));
   if(right&&Mathf.Abs(turn.x)<.35f)turnArmed=true;
   if(right&&turnArmed&&Mathf.Abs(turn.x)>.7f){Turn(Mathf.Sign(turn.x)*30);turnArmed=false;}
   bool deliberateMenu=menuHold.Sample(StareHandAim.MenuPinch(true),game.IsHandInteractionBusy,Time.unscaledDeltaTime);
   if(recallLeft||recallRight||deliberateMenu)game.RecallMenu();
   PollMicrogestures();
   // If a seated user opens/resumes facing away, recover without a controller.
   if(game.InLobby&&game.MatchMenuOpen)
   {
    var menu=view.characterMenu.gameObject.activeSelf?view.characterMenu:view.menu;
    var to=menu.position-rig.centerEyeAnchor.position;
    if(Vector3.Dot(rig.centerEyeAnchor.forward,to.normalized)<.15f)hiddenFor+=Time.unscaledDeltaTime;else hiddenFor=0;
    if(hiddenFor>1.2f){game.RecallMenu();hiddenFor=0;}
   }
  }
  void PollMicrogestures()
  {
   for(int i=0;i<gestureHands.Length;i++)
   {
    var hand=gestureHands[i];
    if(!hand.isActiveAndEnabled||!hand.IsTracked||hand.IsSystemGestureInProgress){previousGesture[i]=OVRHand.MicrogestureType.Invalid;continue;}
    var gesture=hand.GetMicrogestureType();
    bool recognized=gesture!=OVRHand.MicrogestureType.NoGesture&&gesture!=OVRHand.MicrogestureType.Invalid;
    int side=StareTrackedHands.IsLeft(hand)?0:1;
    if(recognized&&gesture!=previousGesture[i]&&Time.unscaledTime>=gestureAt[side])
    {HandleMicrogesture(gesture,side);gestureAt[side]=Time.unscaledTime+.25f;}
    previousGesture[i]=gesture;
   }
  }
  public void HandleMicrogesture(OVRHand.MicrogestureType gesture)=>HandleMicrogesture(gesture,1);
  public void HandleMicrogesture(OVRHand.MicrogestureType gesture,int side)
  {
   // Keep the pulling hand dedicated to the ball; the left hand can still move.
   if(game.SlingshotAvailable&&side==1)return;
   if(gesture==OVRHand.MicrogestureType.ThumbTap)
   {
    Ray ray;bool pinch;
    if(StareHandAim.TryRead(side==0,rig.trackingSpace,out ray,out pinch)){teleport.Tap(side,ray);return;}
    var anchor=side==0?rig.leftHandAnchor:rig.rightHandAnchor;var hand=anchor.GetComponentInChildren<OVRHand>(true);
    if(hand!=null&&hand.IsTracked&&hand.IsPointerPoseValid&&hand.PointerPose!=null)teleport.Tap(side,new Ray(hand.PointerPose.position,hand.PointerPose.forward));
    return;
   }
   if(Debug.isDebugBuild&&gesture!=OVRHand.MicrogestureType.ThumbTap)Debug.Log("[STARE GESTURE] "+gesture);
   // Every swipe is one deliberate step or snap turn, never continuous motion.
   if(gesture==OVRHand.MicrogestureType.SwipeForward)Move(new Vector2(0,.45f));
   else if(gesture==OVRHand.MicrogestureType.SwipeBackward)Move(new Vector2(0,-.45f));
   else if(gesture==OVRHand.MicrogestureType.SwipeLeft){if(side==0)Move(new Vector2(-.45f,0));else Turn(-30);}
   else if(gesture==OVRHand.MicrogestureType.SwipeRight){if(side==0)Move(new Vector2(.45f,0));else Turn(30);}
  }
  public void Move(Vector2 amount)
  {
   if(!game.CanMove)return;
   var head=rig.centerEyeAnchor;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
   if(forward.sqrMagnitude<.1f)return;
   var shift=forward*amount.y+Vector3.Cross(Vector3.up,forward)*amount.x;
   var next=game.LocalFloor+shift;
   // Keep motion on the player's current floor and within the arena footprint.
   if(game.IsSpectator)next=StareArenaFields.Clamp(game.State,next);
   else {var horizontal=Vector2.ClampMagnitude(new Vector2(next.x,next.z),12.2f);next=new Vector3(horizontal.x,next.y,horizontal.y);}
   rig.transform.position+=next-game.LocalFloor;
  }
  public void Turn(float degrees)
  {rig.transform.RotateAround(rig.centerEyeAnchor.position,Vector3.up,degrees);game.RotateSlingshotAim(Quaternion.AngleAxis(degrees,Vector3.up));}
 }
 public sealed class StareMenuHold
 {
  float held;bool fired;
  public bool Sample(bool pinching,bool busy,float dt)
  {if(!pinching||busy){held=0;fired=false;return false;}held+=dt;if(fired||held<.85f)return false;fired=true;return true;}
 }
 public sealed partial class StareVisuals
 {
  public void PlaceLobbyMenus(Transform head)
  {
   var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
   if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
   foreach(var panel in new[]{menu,characterMenu})
   {panel.localScale=Vector3.one*.5f;panel.SetPositionAndRotation(head.position+forward*2.5f,Quaternion.LookRotation(forward));}
   Physics.SyncTransforms();
  }
 }
}
