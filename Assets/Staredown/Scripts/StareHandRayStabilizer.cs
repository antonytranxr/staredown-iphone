using UnityEngine;
namespace Staredown
{
 // Lock the system aim before finger closure; release smoothly after the pinch.
 public sealed class StareHandRayStabilizer
 {
  Ray stable;bool initialized,locked;float releaseAt;
  public void Reset(){initialized=locked=false;}
  public Ray Sample(Ray raw,bool pinching,float strength,float now,float dt)
  {
   if(!initialized){stable=raw;initialized=true;}
   if(pinching||strength>.25f){locked=true;releaseAt=now+.09f;return stable;}
   if(locked&&now<releaseAt)return stable;
   locked=false;float blend=1-Mathf.Exp(-18*Mathf.Min(dt,.05f));
   stable=new Ray(Vector3.Lerp(stable.origin,raw.origin,blend),Vector3.Slerp(stable.direction,raw.direction,blend).normalized);
   return stable;
  }
 }
}
