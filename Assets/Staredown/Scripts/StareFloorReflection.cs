using UnityEngine;
namespace Staredown
{
 // A small planar capture of only contestants and their light pads. No recursive scene render.
 // Captures are throttled and exclude the venue and floor; device profiling remains required.
 public sealed class StareFloorReflection : MonoBehaviour
 {
  public Camera source;public const int ReflectionLayer=30;
  Camera capture;RenderTexture texture;float next;bool rendering;
  void Awake(){texture=new RenderTexture(Application.isMobilePlatform?512:1024,Application.isMobilePlatform?512:1024,16,RenderTextureFormat.ARGB32){name="Arena floor reflection",useMipMap=true,autoGenerateMips=true,filterMode=FilterMode.Trilinear,anisoLevel=4};texture.Create();var g=new GameObject("Floor reflection capture");g.transform.SetParent(transform,false);capture=g.AddComponent<Camera>();capture.enabled=false;capture.stereoTargetEye=StereoTargetEyeMask.None;}
  public static void Mark(Transform t,int layer=ReflectionLayer){foreach(var child in t.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;}
  void LateUpdate()
  {
   if(source==null||rendering||Time.unscaledTime<next)return;
   next=Time.unscaledTime+(Application.isMobilePlatform? .2f:.08f);
   Capture();
  }
  public void Capture()
  {
   if(source==null||capture==null||rendering)return;rendering=true;
   bool oldInvert=GL.invertCulling;
   try{
    capture.CopyFrom(source);capture.enabled=false;capture.stereoTargetEye=StereoTargetEyeMask.None;capture.targetTexture=texture;capture.cullingMask=1<<ReflectionLayer;capture.clearFlags=CameraClearFlags.SolidColor;capture.backgroundColor=Color.black;capture.allowHDR=false;capture.allowMSAA=false;
    Matrix4x4 mirror=Matrix4x4.Scale(new Vector3(1,-1,1));capture.transform.position=mirror.MultiplyPoint(source.transform.position);var angles=source.transform.eulerAngles;capture.transform.eulerAngles=new Vector3(-angles.x,angles.y,-angles.z);capture.worldToCameraMatrix=source.worldToCameraMatrix*mirror;
    capture.nearClipPlane=.05f;capture.farClipPlane=60;capture.projectionMatrix=source.projectionMatrix;
    Vector3 clipPoint=capture.worldToCameraMatrix.MultiplyPoint(new Vector3(0,.003f,0));Vector3 clipNormal=capture.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;capture.projectionMatrix=capture.CalculateObliqueMatrix(new Vector4(clipNormal.x,clipNormal.y,clipNormal.z,-Vector3.Dot(clipPoint,clipNormal)));
    Shader.SetGlobalMatrix("_ArenaReflectionVP",GL.GetGPUProjectionMatrix(capture.projectionMatrix,true)*capture.worldToCameraMatrix);
    GL.invertCulling=!oldInvert;capture.Render();Shader.SetGlobalTexture("_ArenaReflection",texture);Shader.SetGlobalFloat("_ArenaReflectionReady",1);
   }finally{GL.invertCulling=oldInvert;rendering=false;}
  }
  void OnDestroy(){Shader.SetGlobalFloat("_ArenaReflectionReady",0);if(texture!=null){texture.Release();Destroy(texture);}if(capture!=null)Destroy(capture.gameObject);}
 }
}
