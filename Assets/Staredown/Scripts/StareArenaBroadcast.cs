using UnityEngine;
namespace Staredown
{
 public sealed partial class StareVisuals
 {
  // The roster is rendered directly on the physical screen. Keep the public
  // inspection properties without allocating a second arena camera or texture.
  public bool ArenaFeedReady=>false;
  public bool ArenaFeedMode=>false;
  public bool ScoreboardMode=>eyeMural!=null&&eyeMural.GetFloat("_ArenaMode")>1.5f;
  public RenderTexture ArenaFeed=>null;
  public bool PhoneView=>GetComponent<StareMobileSession>()!=null;
  float nextCaptureLayerRefresh;
  void RefreshCaptureExclusions()
  {
   // Menus can be created lazily. Keep them out of mirrors and eye feeds even
   // though the old arena-camera path no longer runs.
   if(Time.unscaledTime<nextCaptureLayerRefresh)return;
   nextCaptureLayerRefresh=Time.unscaledTime+.5f;MarkMenusForCapture();
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
   Shape(PrimitiveType.Cube,"Mirror backing",node,new Vector3(0,0,-.08f),new Vector3(5.1f,3.05f,.12f),border);
   for(int side=-1;side<=1;side+=2){Shape(PrimitiveType.Cube,"Mirror upright",node,new Vector3(side*2.48f,0,.005f),new Vector3(.07f,3.05f,.08f),glow);Shape(PrimitiveType.Cube,"Mirror rail",node,new Vector3(0,side*1.48f,.005f),new Vector3(5.1f,.07f,.08f),glow);}
   var pane=Shape(PrimitiveType.Quad,"Live mirror surface",node,Vector3.zero,new Vector3(4.8f,2.88f,1),border);pane.layer=StareArenaMirror.CaptureSurfaceLayer;
   var material=new Material(Resources.Load<Shader>("StareArenaMirror")){name="Arena live mirror"};materials["Live arena mirror"]=material;pane.GetComponent<Renderer>().sharedMaterial=material;
   var title=Text("YOUR LOOK",node,new Vector3(0,1.72f,.02f),.07f,Cyan);title.transform.localRotation=Quaternion.Euler(0,180,0);
   pane.AddComponent<StareArenaMirror>().Initialize(this,material);
  }
 }
}
