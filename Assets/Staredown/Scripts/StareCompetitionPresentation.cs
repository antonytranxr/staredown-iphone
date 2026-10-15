using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
 public sealed class StareCompetitionPresentation : MonoBehaviour
 {
  StareGame game;StareVisuals view;MeshRenderer[] fields;Material fieldMaterial;Mesh fieldMesh;
  Transform lightningRoot;LineRenderer[] bolts=new LineRenderer[4];Light skyFlash;Material boltMaterial;int strike=-1,audibleStrike=-1;AudioSource thunder;AudioClip thunderClip;StareAnnouncer announcer;
  MaterialPropertyBlock properties;Material baseSky,flashSky;float skyExposure;
  public void Initialize(StareGame owner,StareVisuals visuals)
  {
   game=owner;view=visuals;properties=new MaterialPropertyBlock();fieldMaterial=new Material(Resources.Load<Shader>("StareEnergyField"));fieldMesh=new Mesh{name="Shared competitor protective field"};
   var vertices=new Vector3[66];var uv=new Vector2[66];var indices=new int[32*6];
   for(int i=0;i<=32;i++){float a=i*Mathf.PI*2/32;vertices[i*2]=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));vertices[i*2+1]=vertices[i*2]+Vector3.up;uv[i*2]=new Vector2(i/32f,0);uv[i*2+1]=new Vector2(i/32f,1);if(i<32){int t=i*6,v=i*2;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v+2;indices[t+4]=v+1;indices[t+5]=v+3;}}
   fieldMesh.vertices=vertices;fieldMesh.uv=uv;fieldMesh.triangles=indices;fieldMesh.RecalculateBounds();fields=new MeshRenderer[Tournament.Capacity];
   for(int i=0;i<fields.Length;i++){var node=new GameObject("Competitor protection field "+i);node.transform.SetParent(transform,false);node.AddComponent<MeshFilter>().sharedMesh=fieldMesh;fields[i]=node.AddComponent<MeshRenderer>();fields[i].sharedMaterial=fieldMaterial;fields[i].shadowCastingMode=ShadowCastingMode.Off;fields[i].receiveShadows=false;fields[i].enabled=false;node.transform.localScale=new Vector3(StareArenaFields.Radius,2.25f,StareArenaFields.Radius);}
   lightningRoot=new GameObject("Moonlit sky lightning").transform;lightningRoot.SetParent(transform,false);
   boltMaterial=new Material(Resources.Load<Shader>("StareGlow"));boltMaterial.SetColor("_Color",new Color(.72f,.84f,1,1));
   for(int i=0;i<bolts.Length;i++)
   {
    var bolt=new GameObject(i==0?"Sky lightning main bolt":"Sky lightning branch "+i);bolt.transform.SetParent(lightningRoot,false);
    var line=bolt.AddComponent<LineRenderer>();line.sharedMaterial=boltMaterial;line.useWorldSpace=true;line.positionCount=i==0?12:5;
    line.startWidth=i==0?.16f:.085f;line.endWidth=.025f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;bolts[i]=line;
   }
   skyFlash=new GameObject("Lightning sky flash").AddComponent<Light>();skyFlash.transform.SetParent(lightningRoot,false);
   skyFlash.type=LightType.Directional;skyFlash.color=new Color(.8f,.88f,1);skyFlash.shadows=LightShadows.None;skyFlash.renderMode=LightRenderMode.ForcePixel;
   thunder=new GameObject("Sky lightning thunder").AddComponent<AudioSource>();thunder.transform.SetParent(lightningRoot,false);thunder.playOnAwake=false;thunder.spatialBlend=.65f;thunder.minDistance=18;thunder.maxDistance=80;thunder.rolloffMode=AudioRolloffMode.Linear;thunder.priority=110;thunderClip=Resources.Load<AudioClip>("Audio/Effects/Thunder");announcer=GetComponent<StareAnnouncer>();
   skyFlash.transform.rotation=Quaternion.Euler(55,-25,0);skyFlash.intensity=0;skyFlash.enabled=false;

  }
  void LateUpdate()
  {
   if(game==null||fields==null)return;if(properties==null)properties=new MaterialPropertyBlock();var state=game.State;bool active=state.phase!=(int)MatchPhase.Lobby;int index=0;
   foreach(var f in state.fighters)if(active&&f.alive&&index<fields.Length){var field=fields[index++];field.enabled=true;field.transform.position=ArenaLayout.Seat(state,f.id);properties.SetColor("_Color",f.bot?new Color(1,.66f,.2f):new Color(.18f,.85f,1));field.SetPropertyBlock(properties);}
   for(int i=index;i<fields.Length;i++)fields[i].enabled=false;
   bool enabled=view.CurrentArenaTheme==1&&state.phase==(int)MatchPhase.Duel&&!state.trackingPaused;
   lightningRoot.gameObject.SetActive(enabled);if(!enabled){if(thunder!=null&&thunder.isPlaying)thunder.Stop();skyFlash.intensity=0;skyFlash.enabled=false;RestoreSky();return;}
   // Shared tournament clock keeps each client's sky strikes in the same rhythm.
   int cycle=Mathf.FloorToInt(state.clock/13f);float age=state.clock-cycle*13f-(6f+Mathf.Sin(cycle*2.17f)*1.4f);
   float flash=FlashEnvelope(age);
   if(RenderSettings.skybox!=flashSky&&RenderSettings.skybox!=null)
   {
    baseSky=RenderSettings.skybox;if(flashSky!=null)Destroy(flashSky);flashSky=new Material(baseSky){name="Runtime lightning sky"};
    skyExposure=baseSky.HasProperty("_Exposure")?baseSky.GetFloat("_Exposure"):1;RenderSettings.skybox=flashSky;
   }
   if(flashSky!=null&&flashSky.HasProperty("_Exposure"))flashSky.SetFloat("_Exposure",skyExposure+flash*2.5f);
   skyFlash.intensity=flash*3.2f;skyFlash.enabled=flash>0;
   if(flash>0&&strike!=cycle){strike=cycle;BuildStrike(cycle);}
   if(flash>0&&audibleStrike!=cycle){audibleStrike=cycle;if(thunderClip!=null){thunder.transform.position=bolts[0].GetPosition(11);thunder.pitch=.92f+(cycle%3)*.055f;thunder.clip=thunderClip;thunder.PlayDelayed(.22f);}}
   thunder.volume=announcer!=null&&announcer.IsSpeaking?.22f:.5f;
   for(int i=0;i<bolts.Length;i++){bolts[i].enabled=flash>0;bolts[i].startColor=bolts[i].endColor=new Color(1,1,1,flash);}

  }
  public static float FlashEnvelope(float age)
  {
   if(age<0||age>.42f)return 0;
   if(age<.075f)return 1-age/.1f;
   if(age<.16f)return 0;
   return Mathf.Clamp01(1-(age-.16f)/.26f)*.72f;
  }
  void BuildStrike(int cycle)
  {
   float azimuth=cycle*2.39996f;Vector3 center=new Vector3(Mathf.Sin(azimuth)*27,0,Mathf.Cos(azimuth)*27);
   for(int i=0;i<bolts[0].positionCount;i++)
   {
    float t=i/11f;Vector3 p=center+Vector3.up*Mathf.Lerp(38,8,t);
    p.x+=Mathf.Sin(i*17.17f+cycle*3.1f)*1.7f;p.z+=Mathf.Cos(i*11.31f+cycle)*1.4f;bolts[0].SetPosition(i,p);
   }
   for(int branch=1;branch<bolts.Length;branch++)
   {
    Vector3 start=bolts[0].GetPosition(branch*2+1);Vector3 direction=new Vector3(branch%2==0?-1:1,-1.1f,.3f).normalized;
    for(int i=0;i<5;i++)bolts[branch].SetPosition(i,start+direction*i*1.7f+Vector3.right*(i==0?0:Mathf.Sin(i*7.1f+cycle)*.6f));
   }
  }
  void RestoreSky(){if(flashSky!=null&&RenderSettings.skybox==flashSky)RenderSettings.skybox=baseSky;}
  void OnDisable(){RestoreSky();if(thunder!=null)thunder.Stop();if(skyFlash!=null){skyFlash.intensity=0;skyFlash.enabled=false;}if(lightningRoot!=null)lightningRoot.gameObject.SetActive(false);}
  void OnDestroy(){RestoreSky();if(flashSky!=null)Destroy(flashSky);if(fieldMesh!=null)Destroy(fieldMesh);if(fieldMaterial!=null)Destroy(fieldMaterial);if(boltMaterial!=null)Destroy(boltMaterial);}
 }
}
