using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Staredown
{
 // Phone input owns only its finger. Walking and looking remain separate touches.
 public sealed class StareMobileSlingshot : MonoBehaviour
 {
  public const float MinimumPull=22,FullPull=112,ReloadSeconds=.85f;
  StareMobileSession session;LineRenderer arc;Material material;int pointer=int.MinValue,prop;
  Vector2 start,pull;float nextShot;
  public bool Drawing=>pointer!=int.MinValue;
  public int PointerId=>pointer;
  public Vector2 Pull=>pull;
  public float Strength=>Mathf.Clamp01(pull.magnitude/FullPull);
  public float Cooldown=>Mathf.Max(0,nextShot-Time.unscaledTime);
  public int LocalTosses {get;private set;}
  public Vector3 Origin=>session!=null&&session.ViewCamera!=null?session.ViewCamera.transform.TransformPoint(new Vector3(.06f,-.19f,.32f)):Vector3.zero;
  public Vector3 Direction=>session!=null&&session.ViewCamera!=null?Aim(session.ViewCamera.transform.rotation,pull):Vector3.forward;
  public Vector3 Velocity=>StareThrowAssist.SlingshotVelocity(session!=null?session.State:null,Origin,Direction,Strength);
  public string Hint=>Cooldown>0?"RELOADING":Drawing?(pull.magnitude>=MinimumPull?"RELEASE TO LAUNCH":"PULL BALL DOWN"):"PULL & RELEASE";
  public static Vector3 Aim(Quaternion view,Vector2 pull)
  {return view*(new Vector3(-pull.x/150f,-pull.y/195f,1).normalized);}
  public static Vector3 Flight(Vector3 origin,Vector3 velocity,float seconds)
  {return origin+velocity*seconds+Vector3.down*4.9f*seconds*seconds;}
  public void Initialize(StareMobileSession owner)
  {
   session=owner;var root=new GameObject("Phone slingshot predicted flight");root.transform.SetParent(transform,false);root.layer=2;
   arc=root.AddComponent<LineRenderer>();arc.useWorldSpace=true;arc.widthMultiplier=.004f;arc.numCapVertices=3;arc.shadowCastingMode=ShadowCastingMode.Off;arc.receiveShadows=false;arc.enabled=false;
   material=new Material(Resources.Load<Shader>("StareGlow")??Shader.Find("Sprites/Default")){name="Phone slingshot trajectory"};arc.sharedMaterial=material;
  }
  public bool Begin(int id,Vector2 position)
  {
   if(Drawing||session==null||!session.CanThrow||Cooldown>0)return false;
   pointer=id;start=position;pull=Vector2.zero;return true;
  }
  public void Drag(int id,Vector2 position)
  {if(id==pointer&&Drawing)pull=Vector2.ClampMagnitude(position-start,150);}
  public bool Release(int id)
  {
   if(!Drawing||id!=pointer)return false;
   bool fire=session!=null&&session.CanThrow&&Cooldown<=0&&pull.magnitude>=MinimumPull;
   Vector3 origin=Origin,velocity=Velocity;Cancel();
   if(!fire||!session.SendToss(origin,velocity,prop))return false;
   prop=(prop+1)%3;LocalTosses++;nextShot=Time.unscaledTime+ReloadSeconds;return true;
  }
  public void Cancel(){pointer=int.MinValue;pull=Vector2.zero;if(arc!=null)arc.enabled=false;}
  void LateUpdate()
  {
   if(session==null||!session.CanThrow){Cancel();return;}
   if(arc==null)return;arc.enabled=Drawing&&pull.magnitude>=MinimumPull;if(!arc.enabled)return;
   var color=Color.Lerp(StareVisuals.Cyan,StareVisuals.Gold,Strength);arc.startColor=color;arc.endColor=new Color(color.r,color.g,color.b,.16f);
   arc.positionCount=48;int count=0;Vector3 origin=Origin,velocity=Velocity;
   for(int i=0;i<48;i++){var position=Flight(origin,velocity,i*.075f);arc.SetPosition(i,position);count++;if(position.y<.04f)break;}arc.positionCount=count;
  }
  void OnApplicationPause(bool paused){if(paused)Cancel();}
  void OnDisable(){Cancel();}
  void OnDestroy(){if(material!=null)Destroy(material);}
 }
 public sealed class StareMobileSlingPad : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler,ICancelHandler
 {
  StareMobileSlingshot sling;RectTransform rect,ball;Vector2 rest;
  public void Initialize(StareMobileSlingshot owner,RectTransform projectile){sling=owner;rect=GetComponent<RectTransform>();ball=projectile;rest=ball.anchoredPosition;}
  Vector2 Local(PointerEventData e){Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out point);return point;}
  public void OnPointerDown(PointerEventData e){if(sling!=null)sling.Begin(e.pointerId,Local(e));}
  public void OnDrag(PointerEventData e){if(sling!=null)sling.Drag(e.pointerId,Local(e));}
  public void OnPointerUp(PointerEventData e){if(sling!=null)sling.Release(e.pointerId);}
  public void OnCancel(BaseEventData e){sling?.Cancel();}
  void LateUpdate(){if(ball!=null)ball.anchoredPosition=rest+(sling!=null?sling.Pull*.42f:Vector2.zero);}
  void OnDisable(){sling?.Cancel();}
 }
}
