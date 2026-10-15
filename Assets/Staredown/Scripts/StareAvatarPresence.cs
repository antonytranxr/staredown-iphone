using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Staredown
{
 // Apply live tracking after Animator evaluation, including independent continuous eyelids.
 [DefaultExecutionOrder(2200)]
 public sealed class StareAvatarPresence : MonoBehaviour
 {
  sealed class Arm
  {
   public Transform upper,lower,wrist,hand;public Quaternion correction;
   public float blend;public Vector3 point;public Quaternion rotation=Quaternion.identity;public bool sampled;
  }
  CreatureView avatar;Transform head,mouth;Animator animator;Arm left,right;
  StarePresenceFrame frame;bool observed,human,fresh,danceHands;float l,r,yaw,pitch,headBlend,mouthAmount;
  Quaternion smoothHead=Quaternion.identity,headCorrection=Quaternion.identity;
  static readonly Dictionary<string,Vector3> mouthAnchors=new Dictionary<string,Vector3>();static Mesh mouthMesh;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){mouthAnchors.Clear();mouthMesh=null;}
  public Transform MouthTransform=>mouth;
  public bool UsesDanceHands=>danceHands;
  public bool TryDanceHand(bool isLeft,out Pose pose)
  {
   pose=default;var arm=isLeft?left:right;if(!danceHands||arm==null)return false;
   var eye=(avatar.eyes.left.position+avatar.eyes.right.position)*.5f;
   pose=new Pose(arm.hand.position-eye,arm.hand.rotation*Quaternion.Inverse(arm.correction));return true;
  }
  public void Bind(CreatureView actor,StareVisuals view)
  {
   avatar=actor;var nodes=actor.preset.GetComponentsInChildren<Transform>(true);
   head=nodes.FirstOrDefault(t=>t.name=="head"||t.name=="Head");animator=actor.preset.GetComponent<Animator>();
   left=BindArm(nodes,"l","L");right=BindArm(nodes,"r","R");
   if(left!=null&&right!=null&&transform.InverseTransformPoint(left.hand.position).x<transform.InverseTransformPoint(right.hand.position).x){var swap=left;left=right;right=swap;}
   if(head!=null)headCorrection=Quaternion.Inverse(Quaternion.LookRotation(-transform.forward,transform.up))*head.rotation;
   if(head!=null&&actor.eyes.left!=null&&actor.eyes.right!=null)BuildMouth(view);
  }
  Arm BindArm(Transform[] nodes,string side,string old)
  {
   var arm=new Arm{upper=nodes.FirstOrDefault(t=>t.name=="upperarm."+side||t.name=="UpperArm_"+old),lower=nodes.FirstOrDefault(t=>t.name=="lowerarm."+side||t.name=="Forearm_"+old),hand=nodes.FirstOrDefault(t=>t.name=="hand."+side||t.name=="Hand_"+old)};
   arm.wrist=nodes.FirstOrDefault(t=>t.name=="wrist."+side)??arm.hand;
   if(arm.upper==null||arm.lower==null||arm.hand==null)return null;
   var reference=Quaternion.LookRotation(arm.hand.up,-transform.forward);arm.correction=Quaternion.Inverse(reference)*arm.hand.rotation;return arm;
  }
  public void Observe(Fighter fighter,ArenaState state,bool eliminating)
  {
   observed=true;human=!fighter.bot;fresh=human&&!fighter.disconnected&&state.clock-fighter.presenceAt<=.4f;
   danceHands=!eliminating&&fighter.dancing;
   if(danceHands){ReleaseArm(left);ReleaseArm(right);}
   frame=fresh?fighter.presence:default;l=fighter.bot&&eliminating?1:fighter.left;r=fighter.bot&&eliminating?1:fighter.right;yaw=fighter.yaw;pitch=fighter.pitch;
  }
  void LateUpdate()
  {
   if(!observed||avatar==null)return;
   // Continuous 0..1 closure is never converted to the tournament's binary blink event.
   avatar.eyes.Set(l,r,yaw,pitch);
   if(human&&animator!=null)animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   if(avatar.meta!=null&&avatar.meta.Loaded)return;
   float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),blend=1-Mathf.Exp(-25*dt);
   headBlend=Mathf.MoveTowards(headBlend,fresh&&frame.headTracked?1:0,dt*10);
   smoothHead=Quaternion.Slerp(smoothHead,frame.headTracked?frame.head:Quaternion.identity,blend);
   var facing=Quaternion.LookRotation(-transform.forward,transform.up);
   if(head!=null&&headBlend>0){head.rotation=Quaternion.Slerp(head.rotation,facing*smoothHead*headCorrection,headBlend);}
   var eye=avatar.eyes.left!=null&&avatar.eyes.right!=null?(avatar.eyes.left.position+avatar.eyes.right.position)*.5f:transform.position+Vector3.up*1.53f;
   if(!danceHands){DriveArm(left,frame.left,eye,facing,true,dt);DriveArm(right,frame.right,eye,facing,false,dt);}
   mouthAmount=Mathf.Lerp(mouthAmount,human&&fresh?frame.mouth:0,blend);
   if(mouth!=null){mouth.gameObject.SetActive(mouthAmount>.025f);var scale=mouth.localScale;scale.y=Mathf.Lerp(.006f,.065f,mouthAmount)/Mathf.Max(.0001f,Mathf.Abs(mouth.parent.lossyScale.y));mouth.localScale=scale;}
  }
  static void ReleaseArm(Arm arm){if(arm!=null){arm.blend=0;arm.sampled=false;}}
  void DriveArm(Arm arm,StareHandPose pose,Vector3 eye,Quaternion facing,bool isLeft,float dt)
  {
   if(arm==null)return;arm.blend=Mathf.MoveTowards(arm.blend,human&&fresh&&pose.tracked?1:0,dt*10);if(arm.blend<=0)return;
   if(pose.tracked)
   {if(!arm.sampled){arm.point=pose.position;arm.rotation=pose.rotation;arm.sampled=true;}float s=1-Mathf.Exp(-22*dt);arm.point=Vector3.Lerp(arm.point,pose.position,s);arm.rotation=Quaternion.Slerp(arm.rotation,pose.rotation,s);}
   var target=eye+facing*arm.point;var rotation=facing*arm.rotation*arm.correction;
   var delta=rotation*Quaternion.Inverse(arm.hand.rotation);var wristRotation=delta*arm.wrist.rotation;
   var wristTarget=target-delta*(arm.hand.position-arm.wrist.position);
   var pole=transform.position+transform.right*(isLeft?.85f:-.85f)+transform.forward*.25f+Vector3.up*.65f;
   SolveArm(arm.upper,arm.lower,arm.wrist,Vector3.Lerp(arm.wrist.position,wristTarget,arm.blend),pole);
   arm.wrist.rotation=Quaternion.Slerp(arm.wrist.rotation,wristRotation,arm.blend);arm.hand.rotation=Quaternion.Slerp(arm.hand.rotation,rotation,arm.blend);
  }
  public static void SolveArm(Transform upper,Transform lower,Transform wrist,Vector3 target,Vector3 pole)
  {
   var start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,wrist.position);
   if(a<.0001f||b<.0001f)return;var direction=target-start;if(direction.sqrMagnitude<.000001f)direction=Vector3.down;direction.Normalize();
   float distance=Mathf.Clamp(Vector3.Distance(start,target),Mathf.Abs(a-b)+.0001f,a+b-.0001f);
   float along=(a*a+distance*distance-b*b)/(2*distance);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
   var bend=Vector3.ProjectOnPlane(pole-start,direction);if(bend.sqrMagnitude<.00001f)bend=Vector3.ProjectOnPlane(Vector3.up,direction);if(bend.sqrMagnitude<.00001f)bend=Vector3.right;
   var elbow=start+direction*along+bend.normalized*height;var goal=start+direction*distance;
   upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
   lower.rotation=Quaternion.FromToRotation(wrist.position-lower.position,goal-lower.position)*lower.rotation;
  }
  void BuildMouth(StareVisuals view)
  {
   Vector3 local;
   string mouthKey=avatar.preset.name;
   if(!mouthAnchors.TryGetValue(mouthKey,out local))
   {
    var origin=(avatar.eyes.left.position+avatar.eyes.right.position)*.5f-transform.up*.20f;
    var forward=-transform.forward;var point=origin+forward*.04f;float nearest=3;
    var skins=avatar.preset.GetComponentsInChildren<SkinnedMeshRenderer>();var heads=skins.Where(s=>s.name.Contains("Head")).ToArray();if(heads.Length>0)skins=heads;
    foreach(var skin in skins)
    {
     var baked=new Mesh();skin.BakeMesh(baked);var probe=new GameObject("Mouth surface probe");probe.transform.SetParent(skin.transform,false);var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=baked;Physics.SyncTransforms();RaycastHit hit;
     if(collider.Raycast(new Ray(origin+forward*1.5f,-forward),out hit,3)&&hit.distance<nearest){nearest=hit.distance;point=hit.point+forward*.009f;}
     collider.enabled=false;Destroy(probe);Destroy(baked);
    }
    local=head.InverseTransformPoint(point);mouthAnchors[mouthKey]=local;
   }
   var node=new GameObject("Tracked talking mouth");mouth=node.transform;mouth.SetParent(head,false);mouth.localPosition=local;mouth.rotation=Quaternion.LookRotation(-transform.forward,transform.up);
   mouth.localScale=new Vector3(.13f/Mathf.Abs(head.lossyScale.x),.006f/Mathf.Abs(head.lossyScale.y),.01f/Mathf.Abs(head.lossyScale.z));
   if(mouthMesh==null)
   {
    var vertices=new Vector3[25];var triangles=new int[24*6];
    for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;vertices[i+1]=new Vector3(Mathf.Cos(a)*.5f,Mathf.Sin(a)*.5f,0);int next=(i+1)%24+1;triangles[i*6]=0;triangles[i*6+1]=i+1;triangles[i*6+2]=next;triangles[i*6+3]=0;triangles[i*6+4]=next;triangles[i*6+5]=i+1;}
    mouthMesh=new Mesh{name="Shared talking mouth"};mouthMesh.vertices=vertices;mouthMesh.triangles=triangles;mouthMesh.normals=Enumerable.Repeat(Vector3.forward,vertices.Length).ToArray();mouthMesh.SetUVs(3,Enumerable.Repeat(Vector2.right,vertices.Length).ToList());mouthMesh.RecalculateBounds();
   }
   node.layer=head.gameObject.layer;node.AddComponent<MeshFilter>().sharedMesh=mouthMesh;var material=view.Mat("Mouth cavity",new Color(.065f,.025f,.035f));material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",0);node.AddComponent<MeshRenderer>().sharedMaterial=material;node.SetActive(false);
  }
 }
}
