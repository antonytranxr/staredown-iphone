using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
 public sealed partial class StareVisuals
 {
  Transform architecture; readonly List<Transform> contenderPads=new List<Transform>();
  readonly List<Mesh> artMeshes=new List<Mesh>(); Material softGlow, eyeMural; Mesh crowdMesh; ReflectionProbe arenaProbe;
  static Vector3 Radial(float angle,float radius,float y=0){float a=angle*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius);}
  GameObject MeshObject(string name,Transform parent,List<Vector3> verts,List<int> tris,Material mat,List<Color> colors=null,List<Vector2> uv=null)
  {
   var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(verts);m.SetTriangles(tris,0);if(colors!=null)m.SetColors(colors);if(uv!=null)m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();artMeshes.Add(m);
   var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;return g;
  }
  // Watertight profiled circular architecture, including bevels instead of faceted cubes.
  Transform Revolve(string name,Transform parent,Vector2[] profile,Material mat,int segments=128,float start=0,float sweep=360)
  {
   var vs=new List<Vector3>();var ts=new List<int>();var uv=new List<Vector2>();
   for(int i=0;i<=segments;i++)for(int j=0;j<profile.Length;j++){vs.Add(Radial(start+sweep*i/segments,profile[j].x,profile[j].y));uv.Add(new Vector2((float)i/segments*8,(float)j/(profile.Length-1)));}
   int stride=profile.Length;
   for(int i=0;i<segments;i++)for(int j=0;j<stride-1;j++){int a=i*stride+j,b=a+stride;ts.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
   return MeshObject(name,parent,vs,ts,mat,null,uv).transform;
  }
  Transform Band(string name,Transform parent,float radius,float width,float y,Material mat,int segments=128)
  {return Revolve(name,parent,new[]{new Vector2(radius-width/2,y),new Vector2(radius+width/2,y)},mat,segments);}
  void GlowRing(Transform parent,float radius,float width,float y,Color c,float strength=.35f)
  {
   if(softGlow==null){softGlow=new Material(Resources.Load<Shader>("StareGlow")){name="Soft neon spill"};}
   var vs=new List<Vector3>();var ts=new List<int>();var cs=new List<Color>();
   for(int i=0;i<=96;i++)for(int j=0;j<3;j++){vs.Add(Radial(i*360f/96,radius+(j-1)*width,y));cs.Add(new Color(c.r,c.g,c.b,j==1?strength:0));}
   for(int i=0;i<96;i++)for(int j=0;j<2;j++){int a=i*3+j;ts.AddRange(new[]{a,a+3,a+1,a+1,a+3,a+4});}
   MeshObject("Light spill",parent,vs,ts,softGlow,cs);
  }
  Transform Group(string name,Transform parent,Vector3 pos){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
  GameObject Bar(string name,Transform parent,Vector3 a,Vector3 b,float width,Material mat)
  {var g=Shape(PrimitiveType.Cube,name,parent,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),mat);g.transform.localRotation=Quaternion.LookRotation(b-a);return g;}
  void AudienceForm(string name,Transform parent,Vector3 p,Vector3 scale,Material mat)
  {
   if(crowdMesh==null)
   {
    var vs=new List<Vector3>();var ts=new List<int>();
    for(int y=0;y<=6;y++)for(int x=0;x<=8;x++){float phi=y*Mathf.PI/6,a=x*Mathf.PI*2/8;vs.Add(new Vector3(Mathf.Sin(phi)*Mathf.Cos(a),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(a))*.5f);}
    for(int y=0;y<6;y++)for(int x=0;x<8;x++){int n=y*9+x;ts.AddRange(new[]{n,n+1,n+9,n+1,n+10,n+9});}
    crowdMesh=new Mesh{name="Audience low polygon ellipsoid"};crowdMesh.SetVertices(vs);crowdMesh.SetTriangles(ts,0);crowdMesh.RecalculateNormals();crowdMesh.RecalculateBounds();artMeshes.Add(crowdMesh);
   }
   var t=Group(name,parent,p);t.localScale=scale;if(name.Contains("silhouette"))t.localScale=new Vector3(scale.x,scale.y*2,scale.z);t.gameObject.AddComponent<MeshFilter>().sharedMesh=crowdMesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
  }
  void BuildArchitecture()
  {
   architecture=Group("Arena / permanent architecture",transform,Vector3.zero);
   var graphite=Mat("Brushed graphite",new Color(.033f,.045f,.068f));graphite.SetFloat("_Metallic",.68f);graphite.SetFloat("_Glossiness",.83f);
   var basalt=Mat("Satin basalt",new Color(.016f,.026f,.043f));basalt.SetFloat("_Glossiness",.35f);
   var metal=Mat("Champagne titanium",new Color(.24f,.30f,.36f));metal.SetFloat("_Metallic",.8f);
   var black=Mat("Acoustic charcoal",new Color(.009f,.013f,.022f));
   var cyan=Mat("Ice LED",Cyan,true);var gold=Mat("Championship LED",Gold,true);var coral=Mat("Ember LED",Coral,true);
   // Continuous battle floor and recessed perimeter; the entire room reads as one built venue.
   Shape(PrimitiveType.Cylinder,"Collision floor",architecture,new Vector3(0,-.2f,0),new Vector3(27,.2f,27),graphite,true);
   Revolve("Beveled arena perimeter",architecture,new[]{new Vector2(12.7f,-.2f),new Vector2(12.7f,.04f),new Vector2(12.82f,.12f),new Vector2(13.18f,.12f),new Vector2(13.4f,-.05f)},metal);
   Band("Outer luminous rail",architecture,13.08f,.075f,.125f,gold);Band("Inner luminous rail",architecture,12.8f,.07f,.13f,cyan);
   GlowRing(architecture,12.76f,.65f,.015f,Cyan,.2f);
   for(int i=0;i<40;i++){float a=i*9;Bar("Radial floor seam",architecture,Radial(a,4.3f,.009f),Radial(a,12.65f,.009f),.022f,Mat("Floor seams",new Color(.08f,.12f,.17f)));}
   foreach(float r in new[]{4.6f,7.8f,10.8f})Band("Machined floor joint",architecture,r,.015f,.012f,metal);
   // Low championship dais, precisely matching the final-round player height.
   Revolve("Championship dais",architecture,new[]{new Vector2(0,.4f),new Vector2(3.7f,.4f),new Vector2(3.94f,.33f),new Vector2(4.05f,.12f),new Vector2(4.05f,0)},graphite);
   Band("Dais inlay",architecture,3.77f,.09f,.385f,gold);GlowRing(architecture,3.91f,.5f,.02f,Gold,.24f);
   Band("Dais inner engraving",architecture,3.55f,.025f,.405f,metal);
   Text("S T A R E D O W N",architecture,new Vector3(0,.41f,-2.55f),.12f,new Color(.44f,.53f,.62f)).transform.localRotation=Quaternion.Euler(90,0,0);
   for(int side=-1;side<=1;side+=2){var g=Group("Finalist spotlight disc",architecture,new Vector3(side*1.5f,.405f,0));Band("Finalist ring",g,.88f,.1f,0,side<0?cyan:coral,64);Band("Inner finalist ring",g,.7f,.025f,.001f,metal,64);GlowRing(g,.87f,.45f,.006f,side<0?Cyan:Coral,.5f);}
   // Dense seating, stepped risers and aisle lights create scale without individual animated spectators.
   for(int row=0;row<8;row++)
   {
    float radius=14+row*1.38f,y=1.0f+row*.74f;Color accent=row%3==0?Cyan:row%3==1?Gold:Coral;
    Revolve("Grandstand tier "+row,architecture,new[]{new Vector2(radius-.6f,y-.7f),new Vector2(radius-.6f,y-.08f),new Vector2(radius-.5f,y),new Vector2(radius+.7f,y),new Vector2(radius+.75f,y-.7f)},basalt);
    Band("Tier polished nosing",architecture,radius-.51f,.09f,y+.01f,metal);Band("Tier guide light",architecture,radius-.59f,.035f,y-.15f,Mat("tier"+row,accent,true));
    GlowRing(architecture,radius-.57f,.18f,y+.01f,accent,.16f);
    for(int sector=0;sector<12;sector++)
    {
     for(int seat=0;seat<9;seat++)
     {
      float a=sector*30+4+seat*2.6f;Vector3 p=Radial(a,radius,y);var chair=Group("Audience chair",architecture,p);chair.localRotation=Quaternion.Euler(0,a+180,0);
      Shape(PrimitiveType.Cube,"Backrest",chair,new Vector3(0,.37f,-.15f),new Vector3(.38f,.55f,.13f),black);
      if((seat+sector+row)%5==0)continue;
      Color coat=AudienceColor((seat+sector*3+row*7)%6);var skin=Mat("Audience coat"+((seat+sector*3+row*7)%6),coat*.23f);
      AudienceForm("Audience silhouette",chair,new Vector3(0,.39f,0),new Vector3(.26f,.24f,.23f),skin);
      AudienceForm("Audience head",chair,new Vector3(0,.79f,0),new Vector3(.23f,.26f,.22f),skin);
      Shape(PrimitiveType.Cube,"Audience visor",chair,new Vector3(0,.80f,.105f),new Vector3(.17f,.037f,.018f),Mat("Audience visor"+((seat+row)%3), (seat+row)%3==0?Cyan:(seat+row)%3==1?Coral:Violet,true));
     }
     var a0=Radial(sector*30+1, radius,y+.025f);var a1=Radial(sector*30+1,radius+.6f,y+.025f);Bar("Aisle step light",architecture,a0,a1,.045f,gold);
    }
   }
   // Enclosed outer shell, concourse portals and roof crown.
   Revolve("Arena outer wall",architecture,new[]{new Vector2(25.4f,0),new Vector2(25.4f,10.8f),new Vector2(25.9f,11.2f),new Vector2(27,11.2f),new Vector2(27,0)},black);
   for(int i=0;i<16;i++)
   {
    float a=i*22.5f+11.25f;var bay=Group("Structural bay",architecture,Radial(a,25.2f));bay.localRotation=Quaternion.Euler(0,a,0);
    Shape(PrimitiveType.Cube,"Wall rib",bay,new Vector3(0,8,0),new Vector3(.38f,7,.35f),metal);
    Shape(PrimitiveType.Cube,"Inset linear lamp",bay,new Vector3(0,8,-.2f),new Vector3(.055f,3.3f,.025f),i%2==0?cyan:coral);
    Shape(PrimitiveType.Cube,"Concourse portal",bay,new Vector3(0,3.5f,-.12f),new Vector3(2.0f,2.9f,.1f),black);
    Text("GATE "+(i+1).ToString("00"),bay,new Vector3(0,5.4f,-.25f),.085f,Gold);
    Bar("Roof truss",architecture,Radial(a,25.3f,10.8f),Radial(a,11.8f,14.3f),.19f,metal);
    Bar("Truss light",architecture,Radial(a,24.8f,10.8f),Radial(a,18,12.5f),.04f,i%2==0?cyan:coral);
   }
   Revolve("Suspended crown",architecture,new[]{new Vector2(11.2f,14.1f),new Vector2(11.2f,14.6f),new Vector2(12.2f,14.6f),new Vector2(12.2f,14.1f)},graphite);
   Band("Crown underglow",architecture,11.25f,-.11f,14.09f,cyan);Band("Crown gold rim",architecture,12.15f,-.045f,14.09f,gold);
   // Roof cap removes empty sky; open inner oculus has a shallow dark canopy.
   Revolve("Roof cladding",architecture,new[]{new Vector2(26.7f,11.3f),new Vector2(12.25f,14.65f)},basalt);
   Shape(PrimitiveType.Cylinder,"Oculus ceiling",architecture,new Vector3(0,16.5f,0),new Vector3(30,.1f,30),black);
   BuildEyeWall(metal,black,cyan,coral,gold);
   for(int i=0;i<20;i++)
   {
    var pad=Group("Contender pad "+i,transform,Vector3.zero);var c=AudienceColor(i%6);var led=Mat("Contender LED"+(i%6),c,true);
    Revolve("Chamfered contender pad",pad,new[]{new Vector2(0,.013f),new Vector2(.71f,.013f),new Vector2(.8f,-.03f),new Vector2(.83f,-.09f),new Vector2(.83f,-.14f)},graphite,64);
    Band("Broad light annulus",pad,.66f,.125f,.02f,led,64);Band("Inner steel inlay",pad,.52f,.025f,.025f,metal,64);GlowRing(pad,.71f,.55f,.025f,c,.35f);
    contenderPads.Add(pad);pad.gameObject.SetActive(false);
   }
   SetupArenaLighting();
   var cg=Group("Broadcast Camera",transform,new Vector3(10,8,-19));cg.LookAt(new Vector3(0,3,3));overview=cg.gameObject.AddComponent<Camera>();overview.fieldOfView=58;overview.nearClipPlane=.1f;overview.farClipPlane=100;overview.clearFlags=CameraClearFlags.SolidColor;overview.backgroundColor=new Color(.006f,.01f,.022f);overview.enabled=false;overview.allowHDR=true;
   CreateMenu();CombineArchitecture();
   Invoke(nameof(CaptureArenaReflection),.8f);
  }
  static Color AudienceColor(int i){switch(i%6){case 0:return Cyan;case 1:return Coral;case 2:return Violet;case 3:return Gold;case 4:return new Color(.28f,1,.63f);default:return new Color(1,.25f,.63f);}}
  void BuildEyeWall(Material metal,Material black,Material cyan,Material coral,Material gold)
  {
   var screen=Group("The Final / panoramic eye wall",architecture,new Vector3(0,6.6f,12.0f));portraitRoot=screen;
   // The screen's sweep wraps toward the audience at both ends.
   for(int i=0;i<24;i++)
   {
    float x=-10.5f+(i+.5f)*21/24;float bend=-1.6f*(x*x/110.25f);var p=new Vector3(x,0,bend);
    var panel=Shape(PrimitiveType.Cube,"LED wall module",screen,p,new Vector3(.88f,6.4f,.3f),black);panel.transform.localRotation=Quaternion.Euler(0,Mathf.Atan(3.2f*x/110.25f)*Mathf.Rad2Deg,0);
    Shape(PrimitiveType.Cube,"Upper fascia",screen,p+new Vector3(0,3.3f,0),new Vector3(.9f,.12f,.42f),metal);
    Shape(PrimitiveType.Cube,"Lower fascia",screen,p+new Vector3(0,-3.3f,0),new Vector3(.9f,.16f,.42f),metal);
    Shape(PrimitiveType.Cube,"Fascia light",screen,p+new Vector3(0,3.38f,-.22f),new Vector3(.86f,.045f,.03f),x<0?cyan:coral);
   }
   Shape(PrimitiveType.Cube,"Screen spine",screen,new Vector3(0,0,-.24f),new Vector3(.075f,6.4f,.04f),gold);
   headline=Text("S T A R E D O W N",screen,new Vector3(0,4.25f,-.6f),.38f,Color.white);
   subline=Text("16 CONTENDERS / ONE CHAMPION",screen,new Vector3(0,3.66f,-.6f),.1f,Cyan);
   portraitL=Eyes(screen,new Vector3(-5.2f,.10f,-1.8f),10.1f,Cyan);portraitR=Eyes(screen,new Vector3(5.2f,.10f,-1.8f),10.1f,Coral);
   AddEyeMural(screen);
   boardLeft=Text("FROST",screen,new Vector3(-5.2f,-2.6f,-1.8f),.19f,Cyan);boardRight=Text("EMBER",screen,new Vector3(5.2f,-2.6f,-1.8f),.19f,Coral);
   roundText=Text("THE FINAL STARE",screen,new Vector3(0,-3.85f,-.6f),.15f,Gold);
   // Two substantial support arches visually anchor the board behind the stage.
   for(int side=-1;side<=1;side+=2){Bar("Board support",architecture,new Vector3(side*10.4f,0,12.1f),new Vector3(side*10.4f,8.5f,10.6f),.45f,metal);}
  }
  void AddEyeMural(Transform screen)
  {
   var tex=Resources.Load<Texture2D>("StaredownEyeWall");if(tex==null)return;
   eyeMural=new Material(Resources.Load<Shader>("StareEyeWall")){name="Live panoramic eye artwork",mainTexture=tex};eyeMural.SetTexture("_ClosedTex",Resources.Load<Texture2D>("StaredownEyeWallClosed"));
   var vs=new List<Vector3>();var ts=new List<int>();var uv=new List<Vector2>();
   for(int i=0;i<=64;i++){float u=i/64f,x=(u-.5f)*21;float z=-1.6f*x*x/110.25f-.21f;vs.Add(new Vector3(x,-3.18f,z));vs.Add(new Vector3(x,3.18f,z));uv.Add(new Vector2(u,0));uv.Add(new Vector2(u,1));}
   for(int i=0;i<64;i++){int a=i*2;ts.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});}
   MeshObject("Live eye mural",screen,vs,ts,eyeMural,null,uv);
   portraitL.left.parent.gameObject.SetActive(false);portraitR.left.parent.gameObject.SetActive(false);
  }
  void UpdateEyeMural(Fighter left,Fighter right)
  {
   if(eyeMural==null)return;
   eyeMural.SetVector("_Closures",new Vector4(left==null?0:left.left,left==null?0:left.right,right==null?0:right.left,right==null?0:right.right));
   eyeMural.SetVector("_Species",new Vector4(left==null?0:Mathf.Clamp(left.avatar,0,1),right==null?1:Mathf.Clamp(right.avatar,0,1),0,0));
   CaptureSelectedEyes(left,right);
  }
  void SetupArenaLighting()
  {
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.09f,.13f,.22f);RenderSettings.ambientEquatorColor=new Color(.065f,.085f,.13f);RenderSettings.ambientGroundColor=new Color(.035f,.04f,.055f);
   RenderSettings.fog=true;RenderSettings.fogColor=new Color(.012f,.021f,.044f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.014f;
   foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){l.color=new Color(.7f,.82f,1);l.intensity=.7f;l.shadows=LightShadows.Soft;l.shadowStrength=.65f;}
   var fill=Group("Warm arena rim",transform,Vector3.zero).gameObject.AddComponent<Light>();fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(25,155,0);fill.color=new Color(1,.46f,.25f);fill.intensity=.65f;
   var top=Group("Stage softbox",transform,new Vector3(0,10,0)).gameObject.AddComponent<Light>();top.type=LightType.Point;top.color=new Color(.55f,.8f,1);top.range=18;top.intensity=2.2f;top.shadows=LightShadows.None;
   var probe=Group("Arena reflections",transform,new Vector3(0,3,0));arenaProbe=probe.gameObject.AddComponent<ReflectionProbe>();arenaProbe.mode=ReflectionProbeMode.Realtime;arenaProbe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;arenaProbe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;arenaProbe.resolution=128;arenaProbe.size=new Vector3(60,30,60);arenaProbe.boxProjection=true;arenaProbe.clearFlags=ReflectionProbeClearFlags.SolidColor;arenaProbe.backgroundColor=new Color(.015f,.025f,.05f);arenaProbe.intensity=.8f;
   QualitySettings.pixelLightCount=3;
  }
  void CaptureArenaReflection(){if(arenaProbe!=null)arenaProbe.RenderProbe();}
  void CombineArchitecture()
  {
   // Combine only permanent opaque geometry; player pads, text, eyelids and soft spill remain independent.
   var groups=architecture.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TextMesh>()==null&&r.sharedMaterial!=softGlow&& !r.transform.IsChildOf(portraitL.left.parent)&&!r.transform.IsChildOf(portraitR.left.parent)).GroupBy(r=>r.sharedMaterial).ToArray();
   foreach(var group in groups){var renders=group.ToArray();var combine=new List<CombineInstance>();foreach(var r in renders){var mf=r.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;combine.Add(new CombineInstance{mesh=mf.sharedMesh,transform=architecture.worldToLocalMatrix*r.transform.localToWorldMatrix});}
    if(combine.Count<2)continue;var mesh=new Mesh{name="Batched "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine.ToArray());artMeshes.Add(mesh);var g=new GameObject(mesh.name);g.transform.SetParent(architecture,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=group.Key;foreach(var r in renders)r.enabled=false;
   }
  }
  void UpdatePads(ArenaState state)
  {for(int i=0;i<contenderPads.Count;i++){bool show=i<state.fighters.Length&&state.fighters[i].alive&&!state.finalRound;contenderPads[i].gameObject.SetActive(show);if(show)contenderPads[i].position=ArenaLayout.Seat(state,state.fighters[i].id);}}
  void OnDestroy(){DisposeArenaFeed();DisposeInterface();DisposeEyeFeeds();Font.textureRebuilt-=RefreshFont;if(worldText!=null)Destroy(worldText);foreach(var m in artMeshes)if(m!=null)Destroy(m);foreach(var m in materials.Values)if(m!=null)Destroy(m);if(softGlow!=null)Destroy(softGlow);if(eyeMural!=null)Destroy(eyeMural);if(characterStudioEnvironment!=null)Destroy(characterStudioEnvironment);}
 }
}
