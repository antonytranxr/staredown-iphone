using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
namespace Staredown
{
 public static class StareControllerAim
 {
  public static bool DemoBlinkHeld()
  {
   foreach(var device in InputSystem.devices)
   {
    if(!(device is XRController))continue;
    var tracked=device.TryGetChildControl<ButtonControl>("isTracked");
    var button=device.TryGetChildControl<ButtonControl>("primaryButton");
    if(tracked!=null&&tracked.isPressed&&button!=null&&button.isPressed)return true;
   }
   return OVRInput.Get(OVRInput.Button.One,OVRInput.Controller.LTouch)||OVRInput.Get(OVRInput.Button.One,OVRInput.Controller.RTouch);
  }
  public static Vector3 GripPosition(bool left,Transform space,Vector3 fallback)
  {
   foreach(var device in InputSystem.devices)
   {
    if(!(device is XRController))continue;bool matches=false;foreach(var usage in device.usages)if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))matches=true;
    var tracked=device.TryGetChildControl<ButtonControl>("isTracked");var pos=device.TryGetChildControl<Vector3Control>("devicePosition");
    if(matches&&tracked!=null&&tracked.isPressed&&pos!=null)return space.TransformPoint(pos.ReadValue());
   }
   var controller=left?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;
   return OVRInput.GetControllerPositionTracked(controller)?space.TransformPoint(OVRInput.GetLocalControllerPosition(controller)):fallback;
  }
  public static bool TryGetRay(Transform space,out Ray ray,out bool pressed)=>TryRead(false,space,out ray,out pressed,out _,out _,out _);
  public static bool TryRead(bool left,Transform space,out Ray ray,out bool pressed,out bool held,out Vector2 stick,out bool recall)
  {
   ray=default;pressed=held=recall=false;stick=Vector2.zero;if(space==null)return false;
   var active=OVRInput.GetActiveControllerForHand(left?OVRInput.Handedness.LeftHanded:OVRInput.Handedness.RightHanded);
   if(active==(left?OVRInput.Controller.LHand:OVRInput.Controller.RHand))return false;
   if(OVRInput.GetControllerIsInHandState(left?OVRInput.Hand.HandLeft:OVRInput.Hand.HandRight)==OVRInput.ControllerInHandState.ControllerNotInHand)return false;
   foreach(var device in InputSystem.devices)
   {
    if(!(device is XRController))continue;
    bool matches=false;foreach(var usage in device.usages)if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))matches=true;
    if(!matches)continue;
    var tracked=device.TryGetChildControl<ButtonControl>("isTracked");
    var position=device.TryGetChildControl<Vector3Control>("pointerPosition");var rotation=device.TryGetChildControl<QuaternionControl>("pointerRotation");
    var trigger=device.TryGetChildControl<ButtonControl>("triggerPressed");
    if(tracked==null||!tracked.isPressed||position==null||rotation==null||trigger==null)continue;
    var q=rotation.ReadValue();if(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w<.5f)continue;
    ray=new Ray(space.TransformPoint(position.ReadValue()),space.TransformDirection(q*Vector3.forward));
    held=trigger.isPressed;pressed=trigger.wasPressedThisFrame;
    var axis=device.TryGetChildControl<Vector2Control>("thumbstick");if(axis!=null)stick=axis.ReadValue();
    var secondary=device.TryGetChildControl<ButtonControl>("secondaryButton");var click=device.TryGetChildControl<ButtonControl>("thumbstickClicked");
    recall=(secondary!=null&&secondary.wasPressedThisFrame)||(click!=null&&click.wasPressedThisFrame);return true;
   }
   // OVRInput is an independent fallback if an OpenXR layout does not expose aim controls.
   var controller=left?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;
   stick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,controller);
   pressed=OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,controller);held=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,controller);
   recall=OVRInput.GetDown(OVRInput.Button.Two,controller)||OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,controller);
   if(!OVRInput.GetControllerPositionTracked(controller))return false;
   ray=new Ray(space.TransformPoint(OVRInput.GetLocalControllerPosition(controller)),space.TransformDirection(OVRInput.GetLocalControllerRotation(controller)*Vector3.forward));return true;
  }
 }
}
