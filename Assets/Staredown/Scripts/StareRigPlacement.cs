using UnityEngine;
namespace Staredown
{
    public static class StareRigPlacement
    {
        public static Quaternion Rotation(Vector3 facing,Vector3 localEyeForward)
        {
            facing.y=0;localEyeForward.y=0;
            if(facing.sqrMagnitude<.001f)facing=Vector3.forward;
            if(localEyeForward.sqrMagnitude<.001f)localEyeForward=Vector3.forward;
            return Quaternion.LookRotation(facing)*Quaternion.Inverse(Quaternion.LookRotation(localEyeForward));
        }
        public static Vector3 Position(Vector3 floor,Quaternion rotation,Vector3 localEye)
        {
            // Preserve the tracked vertical distance from the physical floor,
            // including seated play and crouching; only cancel horizontal offset.
            var offset=rotation*localEye;offset.y=0;
            return floor-offset;
        }
    }
}
