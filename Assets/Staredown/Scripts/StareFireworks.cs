using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
 // One shared particle mesh, fixed rocket/audio pools, and a host-clock celebration schedule.
 public sealed class StareFireworks : MonoBehaviour
 {
  public const int Capacity=2048,RocketCapacity=12;
  const float Interval=.42f;
  struct Spark {public Vector3 position,velocity;public Color color;public float life,total,size;}
  struct Rocket {public Vector3 origin;public Color color;public float age,duration,height,trail;public int seed;public bool active;}
  readonly Spark[] sparks=new Spark[Capacity];readonly Rocket[] rockets=new Rocket[RocketCapacity];
  readonly Vector3[] vertices=new Vector3[Capacity*4];readonly Color[] colors=new Color[Capacity*4];
  readonly AudioSource[] sources=new AudioSource[16];readonly float[] volumes=new float[16];
  Mesh mesh;Material material;MeshRenderer cloud;Transform head;StareAnnouncer announcer;
  AudioClip launchClip,burstClip;int sparkIndex,rocketIndex,audioIndex,wave=-1;bool celebrating,simulating;
  public int Launches {get;private set;} public int Bursts {get;private set;} public int ActiveParticles {get;private set;}
  public int ActiveRockets {get;private set;}
  static readonly Color[] Palette={new Color(1,.68f,.12f),new Color(.18f,.85f,1),new Color(1,.3f,.6f),new Color(.6f,.38f,1)};
  public static Vector3 LaunchPosition(int index)
  {float angle=(index*137.5f+25)*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(angle)*16.8f,1.4f,Mathf.Cos(angle)*16.8f);}
  public void Initialize(Transform eye,StareAnnouncer voice)
  {
   head=eye;announcer=voice;if(mesh!=null)return;var node=new GameObject("Pooled arena fireworks");node.transform.SetParent(transform,false);
   cloud=node.AddComponent<MeshRenderer>();material=new Material(Resources.Load<Shader>("StareFirework"));cloud.sharedMaterial=material;cloud.shadowCastingMode=ShadowCastingMode.Off;cloud.receiveShadows=false;cloud.enabled=false;
   mesh=new Mesh{name="2048 pooled celebration sparks"};mesh.MarkDynamic();node.AddComponent<MeshFilter>().sharedMesh=mesh;
   var indices=new int[Capacity*6];var uv=new Vector2[Capacity*4];
   for(int i=0;i<Capacity;i++){int v=i*4,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;}
   mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=indices;mesh.bounds=new Bounds(Vector3.up*8,Vector3.one*80);
   launchClip=Resources.Load<AudioClip>("Audio/Effects/FireworkLaunch");burstClip=Resources.Load<AudioClip>("Audio/Effects/FireworkBurst");
   for(int i=0;i<sources.Length;i++){var audio=new GameObject("Pooled firework sound "+i);audio.transform.SetParent(transform,false);var source=audio.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=12;source.maxDistance=65;source.priority=120;sources[i]=source;}
  }
  public void Observe(ArenaState state)
  {
   if(state==null||mesh==null)return;
   bool champion=state.phase==(int)MatchPhase.Champion&&state.winner>=0;
   if(!champion){if(celebrating)Clear();celebrating=false;wave=-1;return;}
   float elapsed=Mathf.Clamp(8-state.remaining,0,8);int next=Mathf.FloorToInt(elapsed/Interval);
   if(!celebrating){celebrating=true;wave=next-1;}
   // Skip missed waves after a late snapshot rather than bursting the entire show in one frame.
   if(next!=wave&&next<=16){wave=next;Launch(next*2);Launch(next*2+1);if(next>=14){Launch(34+(next-14)*2);Launch(35+(next-14)*2);}}
  }
  void Launch(int index)
  {
   simulating=true;var color=Palette[(index/2)%Palette.Length];rockets[rocketIndex]=new Rocket{active=true,origin=LaunchPosition(index),color=color,duration=.95f+(index%3)*.12f,height=13+(index%4)*1.7f,seed=index};rocketIndex=(rocketIndex+1)%RocketCapacity;
   Sound(launchClip,LaunchPosition(index),.28f,1+(index%3)*.05f);Launches++;
  }
  void Sound(AudioClip clip,Vector3 at,float volume,float pitch)
  {
   if(clip==null)return;int index=audioIndex;var source=sources[index];audioIndex=(audioIndex+1)%sources.Length;volumes[index]=volume;source.Stop();source.transform.position=at;source.clip=clip;source.pitch=pitch;source.volume=volume*(announcer!=null&&announcer.IsSpeaking?.45f:1);source.Play();
  }
  void Emit(Vector3 at,Vector3 velocity,Color color,float size,float duration)
  {sparks[sparkIndex]=new Spark{position=at,velocity=velocity,color=color,size=size,life=duration,total=duration};sparkIndex=(sparkIndex+1)%Capacity;}
  void Explode(Rocket rocket,Vector3 at)
  {
   const int count=96;
   for(int i=0;i<count;i++)
   {
    float y=1-2*(i+.5f)/count,r=Mathf.Sqrt(1-y*y),a=i*2.399963f+rocket.seed;
    var direction=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);float speed=rocket.seed%3==0?5.8f:5.0f;
    if(rocket.seed%3==1){direction.y*=.20f;speed=6.2f;}
    Emit(at,direction*speed,Color.Lerp(rocket.color,Color.white,(i%5==0)?.55f:0),.13f+(i%3)*.032f,2.15f+(i%4)*.22f);
   }
   // Gold willow droplets fall beneath the colored shell; fine silver crackles add depth.
   for(int i=0;i<24;i++){var direction=RandomDirection(i*3+rocket.seed);Emit(at,direction*2.8f,new Color(1,.8f,.32f),.065f,2.7f+(i%3)*.15f);}
   for(int i=0;i<12;i++)Emit(at,RandomDirection(i+rocket.seed)*1.2f,Color.white,.095f,.3f);
   Sound(burstClip,at,.65f,.94f+(rocket.seed%3)*.05f);Bursts++;
  }
  static Vector3 RandomDirection(int i)=>new Vector3(Mathf.Sin(i*2.4f),Mathf.Cos(i*1.7f),Mathf.Cos(i*2.4f)).normalized;
  public void Advance(float dt)
  {
   if(mesh==null||!simulating)return;dt=Mathf.Clamp(dt,0,.05f);int activeRockets=0;
   for(int i=0;i<RocketCapacity;i++)
   {
    var rocket=rockets[i];if(!rocket.active)continue;rocket.age+=dt;
    float t=Mathf.Clamp01(rocket.age/rocket.duration);var at=rocket.origin+Vector3.up*(rocket.height*(2*t-t*t));
    rocket.trail+=dt;if(rocket.trail>=.025f){rocket.trail=0;Emit(at,Vector3.down*.8f,rocket.color,.085f,.38f);Emit(at,Vector3.zero,Color.white,.11f,.10f);}
    if(t>=1){rocket.active=false;Explode(rocket,at);}else activeRockets++;rockets[i]=rocket;
   }
   ActiveRockets=activeRockets;int alive=0;var forward=head!=null?head.forward:Vector3.forward;var up=head!=null?head.up:Vector3.up;var worldToLocal=transform.worldToLocalMatrix;
   for(int i=0;i<Capacity;i++)
   {
    var p=sparks[i];int j=i*4;p.life-=dt;
    if(p.life<=0){colors[j]=colors[j+1]=colors[j+2]=colors[j+3]=Color.clear;sparks[i]=p;continue;}
    p.velocity=p.velocity*Mathf.Exp(-dt*.65f)+Vector3.down*dt*1.15f;p.position+=p.velocity*dt;
    float fade=Mathf.Clamp01(p.life/p.total),size=p.size*(.55f+.45f*fade);var tangent=Vector3.ProjectOnPlane(p.velocity,forward);if(tangent.sqrMagnitude<.001f)tangent=up;tangent.Normalize();var side=Vector3.Cross(forward,tangent).normalized;
    var a=side*size;var b=tangent*size*(1+Mathf.Min(1.5f,p.velocity.magnitude*.35f));var c=p.color;c.a=fade*(.78f+.22f*Mathf.Sin(p.life*24+i));
    vertices[j]=worldToLocal.MultiplyPoint3x4(p.position-a-b);vertices[j+1]=worldToLocal.MultiplyPoint3x4(p.position+a-b);vertices[j+2]=worldToLocal.MultiplyPoint3x4(p.position+a+b);vertices[j+3]=worldToLocal.MultiplyPoint3x4(p.position-a+b);colors[j]=colors[j+1]=colors[j+2]=colors[j+3]=c;sparks[i]=p;alive++;
   }
   ActiveParticles=alive;simulating=alive>0||activeRockets>0;cloud.enabled=alive>0;if(alive>0){mesh.vertices=vertices;mesh.colors=colors;}
   for(int i=0;i<sources.Length;i++)if(sources[i].isPlaying)sources[i].volume=volumes[i]*(announcer!=null&&announcer.IsSpeaking?.45f:1);
  }
  public void Clear(){for(int i=0;i<Capacity;i++)sparks[i].life=0;for(int i=0;i<RocketCapacity;i++)rockets[i].active=false;foreach(var source in sources)if(source!=null)source.Stop();if(cloud!=null)cloud.enabled=false;ActiveParticles=ActiveRockets=0;simulating=false;}
  void OnDisable(){Clear();celebrating=false;wave=-1;}
  void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
 }
}
