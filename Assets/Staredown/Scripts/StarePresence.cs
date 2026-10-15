using System;
using UnityEngine;
using UnityEngine.XR.Hands;
namespace Staredown
{
 [Serializable] public struct StareHandPose
 {public bool tracked;public Vector3 position;public Quaternion rotation;public float grip;}
 [Serializable] public struct StarePresenceFrame
 {public bool headTracked;public Quaternion head;public StareHandPose left,right;public float mouth;}
 public static class StarePresenceValidation
 {
  public static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
  public static bool Rotation(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w>.01f&&q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w<4;
  static StareHandPose Hand(StareHandPose p)
  {
   if(!p.tracked||!Finite(p.position.x)||!Finite(p.position.y)||!Finite(p.position.z)||p.position.sqrMagnitude>2.56f||!Rotation(p.rotation))return default;
   p.rotation=p.rotation.normalized;p.grip=Finite(p.grip)?Mathf.Clamp01(p.grip):0;return p;
  }
  public static StarePresenceFrame Sanitize(StarePresenceFrame p)
  {p.headTracked=p.headTracked&&Rotation(p.head);p.head=p.headTracked?p.head.normalized:Quaternion.identity;p.left=Hand(p.left);p.right=Hand(p.right);p.mouth=Finite(p.mouth)?Mathf.Clamp01(p.mouth):0;return p;}
 }
 public sealed class StarePresence : MonoBehaviour
 {
  StareGame game;StareVoice voice;OVRHand left,right;
  public void Initialize(StareGame owner,StareVoice chat)
  {game=owner;voice=chat;foreach(var hand in owner.rig.GetComponentsInChildren<OVRHand>(true))if(StareTrackedHands.IsLeft(hand))left=hand;else right=hand;}
  public StarePresenceFrame Capture()
  {
   var frame=new StarePresenceFrame();if(game==null||game.LocalId<0)return frame;
   var head=game.rig.centerEyeAnchor;var facing=game.CanMove?Quaternion.Euler(0,head.eulerAngles.y,0):Quaternion.LookRotation(ArenaLayout.Facing(game.State,game.LocalId));
   frame.headTracked=OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter);frame.head=Quaternion.Inverse(facing)*head.rotation;
   frame.left=ReadHand(true,left,head.position,facing);frame.right=ReadHand(false,right,head.position,facing);
   float speaking=voice!=null?voice.LocalMouth:0;frame.mouth=game.eyes.MouthTracked?Mathf.Max(game.eyes.Mouth,speaking*.6f):speaking;
   return StarePresenceValidation.Sanitize(frame);
  }
  StareHandPose ReadHand(bool isLeft,OVRHand hand,Vector3 head,Quaternion facing)
  {
   Pose pose;float grip=0;bool valid=StareTrackedHands.TryPalmPose(isLeft,game.rig.trackingSpace,hand,out pose);
   if(valid)
   {
    Vector3 middle,palm;if(StareTrackedHands.TryJoint(isLeft,game.rig.trackingSpace,hand,XRHandJointID.MiddleTip,out middle)&&StareTrackedHands.TryJoint(isLeft,game.rig.trackingSpace,hand,XRHandJointID.Palm,out palm))grip=Mathf.InverseLerp(.15f,.045f,Vector3.Distance(middle,palm));
   }
   else
   {
    Ray ray;bool pressed,held,recall;Vector2 stick;
    valid=StareControllerAim.TryRead(isLeft,game.rig.trackingSpace,out ray,out pressed,out held,out stick,out recall);
    var anchor=isLeft?game.rig.leftHandAnchor:game.rig.rightHandAnchor;
    pose=new Pose(StareControllerAim.GripPosition(isLeft,game.rig.trackingSpace,anchor.position),anchor.rotation);grip=held?1:0;
   }
   return new StareHandPose{tracked=valid,position=Quaternion.Inverse(facing)*(pose.position-head),rotation=Quaternion.Inverse(facing)*pose.rotation,grip=grip};
  }
 }
}
