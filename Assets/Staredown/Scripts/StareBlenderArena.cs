using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
 public sealed partial class StareVisuals
 {
  Transform referenceEyeWall,finalPads;StareFloorReflection floorReflection;bool referenceBuilt,framedFinal;
  readonly List<Transform> finalPadObjects=new List<Transform>();
  void BuildReferenceArena()
  {
   var model=Resources.Load<GameObject>("ReferenceArena/ReferenceArena");
   if(model==null){Debug.LogError("Blender arena asset missing");BuildArchitecture();return;}
   referenceBuilt=true;architecture=Group("Reference arena / Blender authored",transform,Vector3.zero);
   var shell=Instantiate(model,architecture);shell.name="ReferenceArena / imported Blender mesh";
   var floorMaterial=new Material(Resources.Load<Shader>("StarePolishedFloor")){name="Reference polished graphite"};materials["reference floor"]=floorMaterial;
   foreach(var r in shell.GetComponentsInChildren<MeshRenderer>())
   {
    var originals=r.sharedMaterials;var converted=new Material[originals.Length];
    for(int i=0;i<originals.Length;i++)
    {
     string n=originals[i].name;Material m;
     if(n.Contains("GraphiteFloor"))m=floorMaterial;
     else if(n.Contains("LedIce"))m=Mat("Reference ice",new Color(.55f,.86f,1),true);
     else if(n.Contains("LedAmber"))m=Mat("Reference amber",new Color(1,.61f,.3f),true);
     else if(n.Contains("LedViolet"))m=Mat("Reference violet",Violet,true);
     else if(n.Contains("WarmMetal"))m=Mat("Reference warm titanium",new Color(.09f,.073f,.06f));
     else if(n.Contains("TitaniumTrim"))m=Mat("Reference trim",new Color(.2f,.23f,.29f));
     else if(n.Contains("RecessBlack"))m=Mat("Reference recess",new Color(.007f,.012f,.022f));
     else m=Mat("Reference grandstand graphite",new Color(.038f,.049f,.071f));
     if(m.HasProperty("_Metallic")){m.SetFloat("_Metallic",.58f);m.SetFloat("_Glossiness",n.Contains("GraphiteStand")? .52f:.7f);}converted[i]=m;
    }
    r.sharedMaterials=converted;
    if(r.name.Contains("Floor_Hero"))r.gameObject.AddComponent<MeshCollider>();
   }
   Band("Arena fine champagne rim",architecture,12.57f,.062f,.03f,Mat("Reference floor champagne",new Color(1,.72f,.48f),true),192);
   GlowRing(architecture,12.55f,.5f,.038f,new Color(1,.48f,.21f),.15f);
   for(int row=0;row<5;row++)GlowRing(architecture,13.5f+row*2.4f,.4f,1.013f+row,new Color(1,.54f,.28f),.08f);
   StareReferenceCrowd.Build(this,architecture);
   for(int i=0;i<Tournament.Capacity;i++){var pad=BuildReferencePad("Battle light disc "+i,AudienceColor(i%6));contenderPads.Add(pad);pad.gameObject.SetActive(false);}
   finalPads=Group("Finalist light discs",transform,Vector3.zero);
   for(int i=0;i<2;i++){var pad=BuildReferencePad("Final light "+i,i==0?Cyan:Coral);pad.SetParent(finalPads,false);pad.localPosition=new Vector3(i==0?-1.5f:1.5f,0,0);finalPadObjects.Add(pad);}
   finalPads.gameObject.SetActive(false);
   BuildReferenceEyeWall();BuildChampionshipSetting();SetupReferenceLighting();
   var c=Group("Broadcast Camera",transform,new Vector3(0,10.8f,-21.5f));c.LookAt(new Vector3(0,-.15f,.3f));overview=c.gameObject.AddComponent<Camera>();overview.stereoTargetEye=StereoTargetEyeMask.None;overview.fieldOfView=44;overview.nearClipPlane=.1f;overview.farClipPlane=500;overview.clearFlags=CameraClearFlags.Skybox;overview.backgroundColor=new Color(.008f,.013f,.024f);overview.enabled=false;
   BuildArenaMirror();CreateMenu();if(!Application.isMobilePlatform){floorReflection=gameObject.AddComponent<StareFloorReflection>();floorReflection.source=Camera.main;}
   // Authoring meshes are already sector-batched in Blender; keep those sectors for culling.
  }
  Transform BuildReferencePad(string name,Color accent)
  {
   var p=Group(name,transform,Vector3.zero);var dark=Mat("Reference pad rim",new Color(.035f,.044f,.062f));
   Revolve("Recessed podium",p,new[]{new Vector2(0,.012f),new Vector2(.72f,.012f),new Vector2(.8f,.005f),new Vector2(.86f,-.04f),new Vector2(.86f,-.075f)},dark,64);
   Color pale=Color.Lerp(accent,Color.white,.62f);var inner=Mat("Reference pad enamel"+accent,pale*.6f);inner.EnableKeyword("_EMISSION");inner.SetColor("_EmissionColor",pale*.07f);inner.SetFloat("_Metallic",.3f);inner.SetFloat("_Glossiness",.72f);
   Band("Inset pale enamel",p,.40f,.58f,.018f,inner,64);
   Band("Bright fine outer ring",p,.77f,.05f,.022f,Mat("Reference brilliant ring"+accent,pale,true),64);
   Band("Color rim",p,.81f,.023f,.021f,Mat("Reference rim"+accent,accent,true),64);
   Band("Inner inlay",p,.22f,.018f,.023f,dark,48);
   GlowRing(p,.79f,.36f,.025f,accent,.2f);StareFloorReflection.Mark(p);return p;
  }
  void BuildReferenceEyeWall()
  {
   referenceEyeWall=Group("Final / immersive eye backdrop",transform,new Vector3(0,6.6f,12.0f));portraitRoot=referenceEyeWall;
   var vs=new List<Vector3>();var ts=new List<int>();var uv=new List<Vector2>();
   for(int i=0;i<=64;i++){float u=i/64f,x=(u-.5f)*20,z=-1.5f*x*x/100;vs.Add(new Vector3(x,-2.8f,z));vs.Add(new Vector3(x,2.8f,z));uv.Add(new Vector2(u,.39f));uv.Add(new Vector2(u,.88f));}
   for(int i=0;i<64;i++){int a=i*2;ts.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});}
   eyeMural=new Material(Resources.Load<Shader>("StareEyeWall")){name="Close-up finalist eyes",mainTexture=Resources.Load<Texture2D>("StaredownEyeWall")};eyeMural.SetTexture("_ClosedTex",Resources.Load<Texture2D>("StaredownEyeWallClosed"));
   var display=MeshObject("Panoramic eye display",referenceEyeWall,vs,ts,eyeMural,null,uv);display.layer=StareArenaMirror.CaptureSurfaceLayer;
   var frame=Mat("Sculpted eye wall frame",new Color(.27f,.29f,.34f));
   for(int i=0;i<40;i++){float x=-10+(i+.5f)*.5f,z=-1.5f*x*x/100;for(int side=-1;side<=1;side+=2){var rail=Shape(PrimitiveType.Cube,"Eye wall silver rail",referenceEyeWall,new Vector3(x,side*2.88f,z+.015f),new Vector3(.52f,.13f,.18f),frame);rail.transform.localRotation=Quaternion.Euler(0,Mathf.Atan(3*x/100)*Mathf.Rad2Deg,0);}}
   portraitL=Eyes(referenceEyeWall,Vector3.zero,1,Cyan);portraitR=Eyes(referenceEyeWall,Vector3.zero,1,Coral);portraitL.left.parent.gameObject.SetActive(false);portraitR.left.parent.gameObject.SetActive(false);
   headline=Text("THE FINAL STARE",referenceEyeWall,new Vector3(0,-3.35f,0),.09f,Color.white);subline=Text("",referenceEyeWall,new Vector3(0,-3.7f,0),.045f,Cyan);
   headline.gameObject.SetActive(true);subline.gameObject.SetActive(true);
   boardLeft=Text("FROST",referenceEyeWall,new Vector3(-5.6f,-3.13f,-1.8f),.1f,Cyan);boardRight=Text("EMBER",referenceEyeWall,new Vector3(5.6f,-3.13f,-1.8f),.1f,Coral);roundText=Text("",referenceEyeWall,new Vector3(0,-2.55f,-1.8f),.06f,Gold);
   StareFloorReflection.Mark(referenceEyeWall,StareArenaMirror.CaptureSurfaceLayer);referenceEyeWall.gameObject.SetActive(true);
  }
  void SetupReferenceLighting()
  {
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.25f,.31f,.43f);RenderSettings.ambientEquatorColor=new Color(.16f,.19f,.27f);RenderSettings.ambientGroundColor=new Color(.065f,.075f,.10f);
   RenderSettings.fog=true;RenderSettings.fogColor=new Color(.23f,.27f,.37f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0022f;
   foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){l.color=new Color(.86f,.92f,1);l.intensity=1.05f;l.transform.rotation=Quaternion.Euler(48,-25,0);l.shadows=Application.isMobilePlatform?LightShadows.None:LightShadows.Soft;l.shadowStrength=.65f;}
   var fill=Group("Warm rim softbox",transform,Vector3.zero).gameObject.AddComponent<Light>();fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(22,135,0);fill.color=new Color(1,.69f,.48f);fill.intensity=.58f;
   QualitySettings.pixelLightCount=Application.isMobilePlatform?1:3;
   QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=Application.isMobilePlatform?0:35;
   if(Application.isMobilePlatform){fill.renderMode=LightRenderMode.ForceVertex;QualitySettings.shadows=ShadowQuality.Disable;QualitySettings.antiAliasing=2;OVRManager.foveatedRenderingLevel=OVRManager.FoveatedRenderingLevel.Medium;OVRManager.useDynamicFoveatedRendering=true;}
   var probe=Group("Arena reflections",transform,new Vector3(0,2,0));arenaProbe=probe.gameObject.AddComponent<ReflectionProbe>();arenaProbe.mode=ReflectionProbeMode.Realtime;arenaProbe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;arenaProbe.resolution=128;arenaProbe.size=new Vector3(60,20,60);arenaProbe.boxProjection=true;arenaProbe.intensity=.72f;arenaProbe.farClipPlane=500;arenaProbe.clearFlags=ReflectionProbeClearFlags.Skybox;arenaProbe.backgroundColor=new Color(.02f,.03f,.06f);Invoke(nameof(CaptureArenaReflection),1);
  }
  void UpdateReferencePresentation(ArenaState s,bool broadcast)
  {
   if(!referenceBuilt)return;referenceEyeWall.gameObject.SetActive(true);finalPads.gameObject.SetActive(s.finalRound);headline.characterSize=.036f;
   if(framedFinal!=s.finalRound)
   {
    framedFinal=s.finalRound;
    overview.transform.position=framedFinal?new Vector3(0,1.22f,-2.5f):new Vector3(0,10.8f,-21.5f);
    overview.transform.LookAt(framedFinal?new Vector3(0,1.25f,0):new Vector3(0,-.15f,.3f));
    overview.fieldOfView=framedFinal?58:44;overview.ResetProjectionMatrix();
   }
   if(floorReflection!=null)floorReflection.source=broadcast?overview:Camera.main;
  }
 }
}
