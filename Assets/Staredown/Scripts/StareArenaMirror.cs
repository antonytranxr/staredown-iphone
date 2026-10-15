using UnityEngine;
namespace Staredown
{
 [DefaultExecutionOrder(2600)]
 public sealed class StareArenaMirror : MonoBehaviour
 {
  public const int CaptureSurfaceLayer=27;
  StareVisuals owner;Material surface;Renderer pane;Camera capture;readonly RenderTexture[] feeds=new RenderTexture[2];float next;bool rendering;
  public int Captures {get;private set;}
  public void Initialize(StareVisuals view,Material material){owner=view;surface=material;pane=GetComponent<Renderer>();}
  void LateUpdate()
  {
   var head=owner!=null?owner.LocalViewer:null;var source=head!=null?head.GetComponent<Camera>():Camera.main;
   bool visible=source!=null&&Vector3.Distance(source.transform.position,transform.position)<8&&Vector3.Dot(transform.forward,source.transform.position-transform.position)>.05f&&GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(source),pane.bounds);
   surface.SetFloat("_MirrorReady",visible&&feeds[0]!=null?1:0);
   if(!visible||rendering||Time.unscaledTime<next)return;next=Time.unscaledTime+.125f;Capture(source);
  }
  public static Matrix4x4 Reflection(Vector3 normal,Vector3 point)
  {
   normal.Normalize();float d=-Vector3.Dot(normal,point);var m=Matrix4x4.identity;
   for(int r=0;r<3;r++){for(int c=0;c<3;c++)m[r,c]-=2*normal[r]*normal[c];m[r,3]=-2*d*normal[r];}return m;
  }
  public static Matrix4x4 SurfaceProjection(Matrix4x4 view,Transform pane,float near,float far,Matrix4x4 fallback)
  {
   float left=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.PositiveInfinity,top=float.NegativeInfinity;
   for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
   {
    var point=view.MultiplyPoint(pane.TransformPoint(new Vector3(x*.5f,y*.5f,0)));if(point.z>=-near)return fallback;
    float factor=near/-point.z;left=Mathf.Min(left,point.x*factor);right=Mathf.Max(right,point.x*factor);bottom=Mathf.Min(bottom,point.y*factor);top=Mathf.Max(top,point.y*factor);
   }
   return Matrix4x4.Frustum(left,right,bottom,top,near,far);
  }
  public void Capture(Camera source)
  {
   if(rendering||source==null)return;rendering=true;bool invert=GL.invertCulling;
   if(capture==null){var node=new GameObject("Arena mirror capture");node.transform.SetParent(owner.transform,false);capture=node.AddComponent<Camera>();capture.enabled=false;for(int i=0;i<2;i++){feeds[i]=new RenderTexture(1024,1536,16){name="Arena mirror eye "+i,filterMode=FilterMode.Trilinear,useMipMap=true,autoGenerateMips=true,anisoLevel=4};feeds[i].Create();}surface.SetTexture("_MirrorLeft",feeds[0]);surface.SetTexture("_MirrorRight",feeds[1]);}
   var local=owner.LocalAvatar;var detail=local!=null?local.preset.GetComponent<LODGroup>():null;if(detail!=null)detail.ForceLOD(0);
   owner.SetLocalMetaCapture(true);
   try
   {
    capture.CopyFrom(source);capture.enabled=false;capture.stereoTargetEye=StereoTargetEyeMask.None;capture.allowHDR=false;capture.allowMSAA=false;capture.cullingMask=source.cullingMask&~((1<<CaptureSurfaceLayer)|(1<<2)|(1<<26));
    var reflection=Reflection(transform.forward,transform.position);
    int count=source.stereoEnabled?2:1;
    for(int i=0;i<count;i++)
    {
     var eye=i==0?Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right;
     var view=source.stereoEnabled?source.GetStereoViewMatrix(eye):source.worldToCameraMatrix;
     capture.transform.position=reflection.MultiplyPoint(view.inverse.MultiplyPoint(Vector3.zero));capture.worldToCameraMatrix=view*reflection;
     capture.projectionMatrix=source.stereoEnabled?source.GetStereoProjectionMatrix(eye):source.projectionMatrix;
     // Fill the render texture with the actual mirror portal, including each
     // eye's viewpoint, rather than spending most pixels outside its frame.
     capture.projectionMatrix=SurfaceProjection(capture.worldToCameraMatrix,transform,capture.nearClipPlane,capture.farClipPlane,capture.projectionMatrix);
     var point=capture.worldToCameraMatrix.MultiplyPoint(transform.position+transform.forward*.015f);var normal=capture.worldToCameraMatrix.MultiplyVector(transform.forward).normalized;
     capture.projectionMatrix=capture.CalculateObliqueMatrix(new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
     capture.targetTexture=feeds[i];GL.invertCulling=!invert;capture.Render();
     surface.SetMatrix(i==0?"_MirrorVPLeft":"_MirrorVPRight",GL.GetGPUProjectionMatrix(capture.projectionMatrix,true)*capture.worldToCameraMatrix);
    }
    if(count==1){surface.SetTexture("_MirrorRight",feeds[0]);surface.SetMatrix("_MirrorVPRight",GL.GetGPUProjectionMatrix(capture.projectionMatrix,true)*capture.worldToCameraMatrix);}
    surface.SetFloat("_MirrorReady",1);Captures++;
   }
   finally{if(detail!=null)detail.ForceLOD(-1);owner.SetLocalMetaCapture(false);GL.invertCulling=invert;rendering=false;}
  }
  void OnDestroy(){foreach(var feed in feeds)if(feed!=null){feed.Release();Destroy(feed);}if(capture!=null)Destroy(capture.gameObject);}
 }
}
