using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
 public static class StareTeleportTarget
 {
  public static bool Valid(Vector3 target,Vector3 floor)
  {
   if(float.IsNaN(target.x)||float.IsNaN(target.y)||float.IsNaN(target.z)||float.IsInfinity(target.x)||float.IsInfinity(target.y)||float.IsInfinity(target.z))return false;
   float radius=new Vector2(target.x,target.z).magnitude;
   return Mathf.Abs(target.y-floor.y)<.08f&&Vector3.Distance(target,floor)<=8&&
    (floor.y<.5f?radius<=12.2f:radius>=StareSpectatorArea.InnerRadius&&radius<=StareSpectatorArea.OuterRadius);
  }
  public static bool Arc(Ray ray,Vector3 floor,Vector3[] points,out Vector3 destination,out int count)
  {
   destination=floor;count=1;points[0]=ray.origin;
   var velocity=ray.direction.normalized*7.2f;
   for(int i=1;i<points.Length;i++)
   {
    float t=i*.085f;var next=ray.origin+velocity*t+Vector3.down*4.9f*t*t;
    if(next.y<=floor.y&&points[i-1].y>=floor.y)
    {destination=Vector3.Lerp(points[i-1],next,Mathf.InverseLerp(points[i-1].y,next.y,floor.y));destination.y=floor.y;points[i]=destination;count=i+1;return Valid(destination,floor);}
    points[i]=next;count=i+1;
   }
   return false;
  }
 }
 public sealed class StareTeleport : MonoBehaviour
 {
  StareGame game;LineRenderer arc,marker;Material material;readonly Vector3[] points=new Vector3[40];
  Vector3 destination;int side;float expires;bool valid;
  public bool Aiming{get;private set;}public bool ValidTarget=>Aiming&&valid;public Vector3 Destination=>destination;
  public void Initialize(StareGame owner){game=owner;}
  public void Tap(int handSide,Ray ray)
  {
   if(!game.CanMove||game.IsHandInteractionBusy||game.RayHitsUi(ray)){Cancel();return;}
   if(Aiming&&side==handSide)
   {
    Aim(ray);
    if(valid){game.rig.transform.position+=destination-game.LocalFloor;StareFeedback.Instance?.TossFeedback();Cancel();}
    return;
   }
   side=handSide;Aiming=true;expires=Time.unscaledTime+10;Aim(ray);
  }
  public void Aim(Ray ray)
  {
   if(!Aiming)return;if(arc==null)Build();
   int count;valid=StareTeleportTarget.Arc(ray,game.LocalFloor,points,out destination,out count);if(game.IsSpectator)valid=valid&&StareArenaFields.Contains(game.State,destination);
   arc.enabled=true;arc.positionCount=count;for(int i=0;i<count;i++)arc.SetPosition(i,points[i]);
   var color=valid?new Color(.15f,.95f,.72f):new Color(1,.28f,.16f);arc.startColor=arc.endColor=color;
   marker.enabled=count>1&&Mathf.Abs(points[count-1].y-game.LocalFloor.y)<.01f;marker.startColor=marker.endColor=color;
   for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;marker.SetPosition(i,destination+Vector3.up*.025f+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.28f);}
  }
  void Update()
  {
   if(!Aiming)return;
   if(!game.CanMove||game.IsHandInteractionBusy||Time.unscaledTime>expires){Cancel();return;}
   Ray ray;bool pinch;
   if(StareHandAim.TryRead(side==0,game.rig.trackingSpace,out ray,out pinch))
   {if(pinch||game.RayHitsUi(ray)){Cancel();return;}Aim(ray);return;}
   var anchor=side==0?game.rig.leftHandAnchor:game.rig.rightHandAnchor;var hand=anchor.GetComponentInChildren<OVRHand>(true);
   if(hand==null||!hand.IsTracked||!hand.IsPointerPoseValid||hand.PointerPose==null||hand.IsSystemGestureInProgress){Cancel();return;}
   ray=new Ray(hand.PointerPose.position,hand.PointerPose.forward);if(game.RayHitsUi(ray)){Cancel();return;}Aim(ray);
  }
  void Build()
  {
   material=new Material(Resources.Load<Shader>("StareGlow")){name="Teleport landing guide",color=Color.white};
   arc=Line("Thumb tap teleport arc",.012f);marker=Line("Teleport landing ring",.022f);marker.loop=true;marker.positionCount=48;
  }
  LineRenderer Line(string name,float width)
  {var go=new GameObject(name);go.layer=2;go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.widthMultiplier=width;line.numCapVertices=4;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;}
  public void Cancel(){Aiming=valid=false;if(arc!=null)arc.enabled=false;if(marker!=null)marker.enabled=false;}
  void OnDisable(){Cancel();}void OnDestroy(){if(material!=null)Destroy(material);}
 }
}
