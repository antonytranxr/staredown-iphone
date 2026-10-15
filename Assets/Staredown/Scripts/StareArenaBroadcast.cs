using UnityEngine;
namespace Staredown
{
 public sealed partial class StareVisuals
 {
  Camera arenaCapture;RenderTexture arenaFeed;float nextArenaFeed;
  public bool ArenaFeedReady=>arenaFeed!=null&&eyeMural!=null&&eyeMural.GetFloat("_ArenaReady")>.5f;
  public bool ArenaFeedMode=>eyeMural!=null&&eyeMural.GetFloat("_ArenaMode")>.5f;
  public RenderTexture ArenaFeed=>arenaFeed;
  void UpdateArenaFeed()
  {
   if(eyeMural==null||Time.unscaledTime<nextArenaFeed)return;
   nextArenaFeed=Time.unscaledTime+.125f;
   MarkMenusForCapture();
   if(arenaCapture==null)
   {
    var node=new GameObject("Jumbotron arena camera");node.transform.SetParent(transform,false);arenaCapture=node.AddComponent<Camera>();arenaCapture.enabled=false;arenaCapture.stereoTargetEye=StereoTargetEyeMask.None;
    arenaCapture.transform.position=new Vector3(0,7.8f,10);arenaCapture.transform.LookAt(new Vector3(0,.8f,-3));arenaCapture.fieldOfView=57;arenaCapture.aspect=20/5.6f;arenaCapture.nearClipPlane=.15f;arenaCapture.farClipPlane=150;
    arenaCapture.clearFlags=CameraClearFlags.Skybox;arenaCapture.allowHDR=false;arenaCapture.allowMSAA=false;arenaCapture.cullingMask=~((1<<StareArenaMirror.CaptureSurfaceLayer)|(1<<2)|(1<<26));
    arenaFeed=new RenderTexture(2048,576,16){name="Live arena jumbotron",filterMode=FilterMode.Trilinear,useMipMap=true,autoGenerateMips=true,anisoLevel=4};arenaFeed.Create();arenaCapture.targetTexture=arenaFeed;eyeMural.SetTexture("_ArenaTex",arenaFeed);
   }
   SetLocalMetaCapture(true);
   try{arenaCapture.Render();eyeMural.SetFloat("_ArenaReady",1);}
   finally{SetLocalMetaCapture(false);}
  }
  public static void FitBoardLabel(TextMesh text,float maxWidth,float size)
  {
   if(text==null)return;text.characterSize=size;
   text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
   float advance=0;foreach(char c in text.text){CharacterInfo glyph;if(text.font.GetCharacterInfo(c,out glyph,text.fontSize,text.fontStyle))advance+=glyph.advance;}
   float width=advance*size/10;
   if(width>maxWidth)text.characterSize=size*maxWidth/width;
  }
  void BuildArenaMirror()
  {
   var node=Group("Arena dressing mirror",transform,new Vector3(-11.4f,1.58f,-2.6f));node.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(-node.position,Vector3.up).normalized);
   var border=Mat("Mirror satin frame",new Color(.07f,.15f,.22f));var glow=Mat("Mirror cyan edge",Cyan,true);
   Shape(PrimitiveType.Cube,"Mirror backing",node,new Vector3(0,0,-.08f),new Vector3(2.55f,3.05f,.12f),border);
   for(int side=-1;side<=1;side+=2){Shape(PrimitiveType.Cube,"Mirror upright",node,new Vector3(side*1.24f,0,.005f),new Vector3(.07f,3.05f,.08f),glow);Shape(PrimitiveType.Cube,"Mirror rail",node,new Vector3(0,side*1.48f,.005f),new Vector3(2.55f,.07f,.08f),glow);}
   var pane=Shape(PrimitiveType.Quad,"Live mirror surface",node,Vector3.zero,new Vector3(2.4f,2.88f,1),border);pane.layer=StareArenaMirror.CaptureSurfaceLayer;
   var material=new Material(Resources.Load<Shader>("StareArenaMirror")){name="Arena live mirror"};materials["Live arena mirror"]=material;pane.GetComponent<Renderer>().sharedMaterial=material;
   var title=Text("YOUR LOOK",node,new Vector3(0,1.72f,.02f),.07f,Cyan);title.transform.localRotation=Quaternion.Euler(0,180,0);
   pane.AddComponent<StareArenaMirror>().Initialize(this,material);
  }
  void DisposeArenaFeed(){if(arenaFeed!=null){arenaFeed.Release();Destroy(arenaFeed);}if(arenaCapture!=null)Destroy(arenaCapture.gameObject);}
 }
}
