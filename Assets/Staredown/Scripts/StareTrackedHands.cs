using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
namespace Staredown
{
 // Only present the tracked OpenXR skeleton, never its idle bind pose.
 [DefaultExecutionOrder(1000)]
 public sealed class StareTrackedHands : MonoBehaviour
 {
  OVRHand hand;OVRSkeleton skeleton;Renderer[] meshes;StareGame game;
  void Awake(){hand=GetComponent<OVRHand>();skeleton=GetComponent<OVRSkeleton>();meshes=GetComponentsInChildren<Renderer>(true);SetVisible(false);}
  void LateUpdate(){if(game==null)game=FindFirstObjectByType<StareGame>();SetVisible((game==null||!game.Dancing)&&hand!=null&&hand.IsTracked&&hand.HandConfidence==OVRHand.TrackingConfidence.High&&skeleton!=null&&skeleton.IsInitialized&&skeleton.IsDataValid);}
  void OnDisable(){SetVisible(false);}
  void SetVisible(bool shown){if(meshes==null)return;foreach(var mesh in meshes)if(mesh!=null)mesh.enabled=shown;}
  static readonly List<XRHandSubsystem> interactionSystems=new List<XRHandSubsystem>();
  static int systemsFrame=-1;
  public static bool TryPalmPose(bool left,Transform space,OVRHand hand,out Pose pose)
  {
   pose=default;if(space==null)return false;
   if(systemsFrame!=Time.frameCount){SubsystemManager.GetSubsystems(interactionSystems);systemsFrame=Time.frameCount;}
   foreach(var system in interactionSystems)
   {Pose p;var h=left?system.leftHand:system.rightHand;if(system.running&&h.isTracked&&h.GetJoint(XRHandJointID.Palm).TryGetPose(out p)){pose=OrientPalm(left,space,hand,new Pose(space.TransformPoint(p.position),space.rotation*p.rotation));return true;}}
   var skeleton=hand!=null?hand.GetComponent<OVRSkeleton>():null;
   if(hand==null||!hand.IsTracked||skeleton==null||!skeleton.IsDataValid)return false;
   foreach(var bone in skeleton.Bones)if(bone.Id==OVRSkeleton.BoneId.XRHand_Palm||bone.Id==OVRSkeleton.BoneId.Hand_WristRoot){pose=OrientPalm(left,space,hand,new Pose(bone.Transform.position,bone.Transform.rotation));return true;}
   return false;
  }
  static Pose OrientPalm(bool left,Transform space,OVRHand hand,Pose pose)
  {
   Vector3 wrist,middle,index,little;
   if(TryJoint(left,space,hand,XRHandJointID.Wrist,out wrist)&&TryJoint(left,space,hand,XRHandJointID.MiddleProximal,out middle)&&TryJoint(left,space,hand,XRHandJointID.IndexProximal,out index)&&TryJoint(left,space,hand,XRHandJointID.LittleProximal,out little))
   {var fingers=middle-wrist;var normal=Vector3.Cross(fingers,index-little)*(left?1:-1);if(fingers.sqrMagnitude>.0001f&&normal.sqrMagnitude>.000001f)pose.rotation=Quaternion.LookRotation(fingers,normal);}
   return pose;
  }
  public static bool IsLeft(OVRHand hand)=>hand!=null&&hand.GetHand()==OVRPlugin.Hand.HandLeft;
  // Read positions from the same tracking space as the headset, independently of far-UI aim.
  public static bool TryJoint(bool left,Transform space,OVRHand hand,XRHandJointID joint,out Vector3 position)
  {
   position=Vector3.zero;if(space==null)return false;
   if(systemsFrame!=Time.frameCount){SubsystemManager.GetSubsystems(interactionSystems);systemsFrame=Time.frameCount;}
   foreach(var system in interactionSystems)
   {Pose pose;var h=left?system.leftHand:system.rightHand;if(system.running&&h.isTracked&&h.GetJoint(joint).TryGetPose(out pose)){position=space.TransformPoint(pose.position);return true;}}
   if(hand==null||!hand.isActiveAndEnabled||!hand.IsTracked)return false;
   // Meta OpenXR joint translations are absolute tracking-space poses, not local bone offsets.
   var provider=hand as OVRSkeleton.IOVRSkeletonDataProvider;var data=provider.GetSkeletonPoseData();
   var skeleton=hand.GetComponent<OVRSkeleton>();
   if(skeleton!=null&&(skeleton.GetSkeletonType()==OVRSkeleton.SkeletonType.XRHandLeft||skeleton.GetSkeletonType()==OVRSkeleton.SkeletonType.XRHandRight)&&data.IsDataValid&&data.BoneTranslations!=null)
   {
    int index=joint==XRHandJointID.Palm?0:joint==XRHandJointID.Wrist?1:(int)joint-1; // Unity orders wrist/palm; OpenXR orders palm/wrist.
    if(index>=0&&index<data.BoneTranslations.Length){var point=data.BoneTranslations[index];position=space.TransformPoint(new Vector3(point.x,point.y,-point.z));return true;}
   }
   if(skeleton==null||!skeleton.IsDataValid)return false;
   OVRSkeleton.BoneId id=joint==XRHandJointID.IndexTip?OVRSkeleton.BoneId.Hand_IndexTip:joint==XRHandJointID.ThumbTip?OVRSkeleton.BoneId.Hand_ThumbTip:joint==XRHandJointID.MiddleTip?OVRSkeleton.BoneId.Hand_MiddleTip:OVRSkeleton.BoneId.Hand_WristRoot;
   foreach(var bone in skeleton.Bones)if(bone.Id==id){position=bone.Transform.position;return true;}return false;
  }
  public static bool TryInteractionPosition(bool left,Transform space,OVRHand hand,out Vector3 position)
  {
   Vector3 a,b;
   if(left)return TryJoint(left,space,hand,XRHandJointID.Palm,out position);
   if(TryJoint(left,space,hand,XRHandJointID.IndexTip,out a)&&TryJoint(left,space,hand,XRHandJointID.ThumbTip,out b)){position=(a+b)*.5f;return true;}
   position=Vector3.zero;return false;
  }
  public static bool TryGrip(OVRHand hand,out Vector3 position)
  {var rig=hand!=null?hand.GetComponentInParent<OVRCameraRig>():null;return TryInteractionPosition(false,rig!=null?rig.trackingSpace:null,hand,out position);}
 }
 // Hysteresis stops a noisy release; a full-hand grasp can also hold the pouch.
 public sealed class StarePhysicalGrab
 {
  bool held;
  public bool Sample(bool tracked,float pinchDistance,bool grasp,bool sdkPinch)
  {if(!tracked){held=false;return false;}held=sdkPinch||grasp||pinchDistance<(held?.045f:.025f);return held;}
  public bool Read(bool left,Transform space,OVRHand hand,out bool tracked,out Vector3 position)
  {
   tracked=StareTrackedHands.TryInteractionPosition(left,space,hand,out position);
   Vector3 index=Vector3.zero,thumb=Vector3.zero,palm=Vector3.zero,middle=Vector3.zero;
   bool tips=StareTrackedHands.TryJoint(left,space,hand,XRHandJointID.IndexTip,out index)&&StareTrackedHands.TryJoint(left,space,hand,XRHandJointID.ThumbTip,out thumb);
   bool grasp=StareTrackedHands.TryJoint(left,space,hand,XRHandJointID.Palm,out palm)&&StareTrackedHands.TryJoint(left,space,hand,XRHandJointID.MiddleTip,out middle)&&tips&&Vector3.Distance(index,palm)<.065f&&Vector3.Distance(middle,palm)<.065f;
   bool sdk=StareHandAim.PhysicalIndexPinch(left)||(hand!=null&&hand.IsTracked&&hand.GetFingerIsPinching(OVRHand.HandFinger.Index));
   return Sample(tracked,tips?Vector3.Distance(index,thumb):float.PositiveInfinity,grasp,sdk);
  }
}
}
