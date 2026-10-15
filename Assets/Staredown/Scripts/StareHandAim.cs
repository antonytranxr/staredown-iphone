using UnityEngine;
using UnityEngine.XR.Hands;
namespace Staredown
{
    public static class StareHandAim
    {
        public static bool MiddlePinch(bool left)
        {
            var device=left?MetaAimHand.left:MetaAimHand.right;
            return device!=null&&device.isTracked.isPressed&&(((MetaAimFlags)device.aimFlags.ReadValue()&MetaAimFlags.SystemGesture)==0)&&device.middlePressed.isPressed;
        }
        public static bool MenuPinch(bool left)
        {var hand=left?MetaAimHand.left:MetaAimHand.right;return hand!=null&&hand.added&&hand.isTracked.isPressed&&!SystemGesture(left)&&hand.littlePressed.isPressed&&!hand.indexPressed.isPressed&&!hand.middlePressed.isPressed;}
        public static bool SystemGesture(bool left)
        {var hand=left?MetaAimHand.left:MetaAimHand.right;return hand!=null&&hand.added&&(((MetaAimFlags)hand.aimFlags.ReadValue()&MetaAimFlags.SystemGesture)!=0);}
        public static bool PhysicalIndexPinch(bool left)
        {var hand=left?MetaAimHand.left:MetaAimHand.right;return hand!=null&&hand.added&&hand.isTracked.isPressed&&(((MetaAimFlags)hand.aimFlags.ReadValue()&MetaAimFlags.SystemGesture)==0)&&hand.indexPressed.isPressed;}
        public static float PinchStrength(bool left)
        {var hand=left?MetaAimHand.left:MetaAimHand.right;return hand!=null&&hand.added?hand.pinchStrengthIndex.ReadValue():0;}
        public static bool TryRead(bool left,Transform trackingSpace,out Ray ray,out bool pinch)
        {
            ray=default;pinch=false;
            var hand=left?MetaAimHand.left:MetaAimHand.right;
            if(hand==null||!hand.added||trackingSpace==null||!hand.isTracked.isPressed)return false;
            var flags=(MetaAimFlags)hand.aimFlags.ReadValue();
            if((flags&MetaAimFlags.SystemGesture)!=0)return false;
            if((flags&MetaAimFlags.Valid)==0)return false;
            ray=new Ray(trackingSpace.TransformPoint(hand.devicePosition.ReadValue()),trackingSpace.TransformDirection(hand.deviceRotation.ReadValue()*Vector3.forward));
            pinch=hand.indexPressed.isPressed;
            return true;
        }
    }
    // Tracking recovery while already pinching must not click a new button.
    public sealed class StarePinchGate
    {
        bool armed;
        public bool Sample(bool tracked,bool pinching)
        {
            if(!tracked){armed=false;return false;}
            if(!pinching){armed=true;return false;}
            if(!armed)return false;
            armed=false;return true;
        }
    }
}
