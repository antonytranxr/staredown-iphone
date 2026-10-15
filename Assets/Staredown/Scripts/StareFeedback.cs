using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Staredown
{
    public sealed class StareFeedback : MonoBehaviour
    {
        const int Capacity=768,RingCount=32,CardCount=12;
        static readonly Color Cyan=new Color(.12f,.82f,1),Gold=new Color(1,.65f,.12f),Red=new Color(1,.16f,.06f);
        public static StareFeedback Instance {get;private set;}
        public float Pressure {get;private set;}
        public int EliminationBursts {get;private set;}
        public StareFireworks Fireworks {get;private set;}
        public int FaceBursts {get;private set;}
        public int ActiveParticles {get;private set;}
        public string LastEvent {get;private set;}="";
        struct Mote {public Vector3 position,velocity;public float life,total,size,gravity,drag,stretch;public Color color;}
        struct Previous {public bool alive;public int wins;public Vector3 seat;}
        sealed class Ring {public MeshRenderer renderer;public Transform root;public MaterialPropertyBlock properties=new MaterialPropertyBlock();public float age,duration,radius;public Color color;}
        sealed class ResultCard {public Transform root;public TextMesh text;public float age,duration;public Vector3 position,ground;public Color color;public bool active,secondary;}
        readonly ResultCard[] cards=new ResultCard[CardCount];int cardIndex;Transform badge;Renderer badgeRenderer;Material badgeMaterial;
        readonly Mote[] motes=new Mote[Capacity];readonly Ring[] rings=new Ring[RingCount];
        readonly Dictionary<int,Previous> previous=new Dictionary<int,Previous>();
        readonly HashSet<int> landed=new HashSet<int>();readonly List<int> retired=new List<int>();float trailAt,impactAt,eliminateSoundAt;
        readonly Dictionary<string,AudioClip> sounds=new Dictionary<string,AudioClip>();
        readonly Vector3[] vertices=new Vector3[Capacity*4];readonly Color[] colors=new Color[Capacity*4];
        StareGame game;Transform head;Material glow,spark,peripheral;Mesh mesh;MeshRenderer cloud,edgeRenderer;Transform edge;
        TextMesh banner;float bannerUntil,beatAt,hoverAt,eventGlow,hapticUntil;Color eventColor;
        AudioSource sound,heartbeat;int phase=-1,second=-1,moteIndex,ringIndex;bool initialized;
        void Start()
        {
            Instance=this;game=GetComponent<StareGame>();head=game.rig.centerEyeAnchor;
            glow=new Material(Resources.Load<Shader>("StareGlow"));glow.SetColor("_Color",Color.white);
            var g=new GameObject("Pooled arena sparks");g.transform.SetParent(transform,false);
            spark=new Material(Resources.Load<Shader>("StareSpark"));
            cloud=g.AddComponent<MeshRenderer>();cloud.sharedMaterial=spark;cloud.shadowCastingMode=ShadowCastingMode.Off;cloud.receiveShadows=false;
            mesh=new Mesh{name="768 pooled feedback particles"};mesh.MarkDynamic();g.AddComponent<MeshFilter>().sharedMesh=mesh;
            var indices=new int[Capacity*6];for(int i=0;i<Capacity;i++){int v=i*4,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;}
            var uv=new Vector2[Capacity*4];for(int i=0;i<Capacity;i++){uv[i*4]=new Vector2(0,0);uv[i*4+1]=new Vector2(1,0);uv[i*4+2]=new Vector2(1,1);uv[i*4+3]=new Vector2(0,1);}
            mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=indices;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*80);
            var waveShader=Resources.Load<Shader>("StareShockwave");Destroy(glow);glow=new Material(waveShader);
            for(int i=0;i<RingCount;i++)
            {
                var r=GameObject.CreatePrimitive(PrimitiveType.Quad);r.name="Pooled cinematic wave "+i;Destroy(r.GetComponent<Collider>());r.transform.SetParent(transform,false);r.transform.rotation=Quaternion.Euler(90,0,0);
                var renderer=r.GetComponent<MeshRenderer>();renderer.sharedMaterial=glow;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
                rings[i]=new Ring{renderer=renderer,root=r.transform};
            }
            var view=game.GetComponent<StareVisuals>();
            for(int i=0;i<CardCount;i++)
            {
                var root=new GameObject("Pooled arena result "+i).transform;root.SetParent(transform,false);
                var text=view.Text("",root,Vector3.zero,.055f,Red);text.fontStyle=FontStyle.Bold;
                root.gameObject.SetActive(false);cards[i]=new ResultCard{root=root,text=text};
            }
            edge=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;edge.name="Peripheral eye warning";Destroy(edge.GetComponent<Collider>());
            edge.SetParent(head,false);edge.localPosition=new Vector3(0,0,.65f);edge.localScale=new Vector3(2.4f,2.4f,1);
            peripheral=new Material(Resources.Load<Shader>("StarePeripheral"));peripheral.SetColor("_Color",Color.clear);
            edgeRenderer=edge.GetComponent<MeshRenderer>();edgeRenderer.sharedMaterial=peripheral;edgeRenderer.shadowCastingMode=ShadowCastingMode.Off;edgeRenderer.receiveShadows=false;
            banner=game.GetComponent<StareVisuals>().Text("",head,new Vector3(0,.20f,1.1f),.026f,Cyan);
            badge=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;badge.name="Competition result badge";Destroy(badge.GetComponent<Collider>());
            badge.SetParent(head,false);badge.localPosition=new Vector3(0,.20f,1.115f);badge.localScale=new Vector3(.62f,.145f,1);
            badgeMaterial=new Material(Resources.Load<Shader>("StarePanel"));badgeMaterial.SetColor("_Color",new Color(.018f,.026f,.046f));badgeMaterial.SetVector("_Size",new Vector4(.62f,.145f,.025f,.003f));badgeMaterial.SetFloat("_Selected",1);
            badgeRenderer=badge.GetComponent<Renderer>();badgeRenderer.sharedMaterial=badgeMaterial;badgeRenderer.shadowCastingMode=ShadowCastingMode.Off;badgeRenderer.receiveShadows=false;badgeRenderer.enabled=false;
            foreach(var clip in Resources.LoadAll<AudioClip>("Audio/Effects"))sounds[clip.name]=clip;
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.priority=80;
            heartbeat=gameObject.AddComponent<AudioSource>();heartbeat.playOnAwake=false;heartbeat.spatialBlend=0;heartbeat.priority=96;
            Fireworks=gameObject.AddComponent<StareFireworks>();Fireworks.Initialize(head,GetComponent<StareAnnouncer>());
            initialized=true;Debug.Log("[STARE FX] ready: pooled sparks, eye warning, elimination rings, victory and feedback audio");
        }
        public void TossFeedback(){if(!initialized)return;Play("Start",.12f);Rumble(.08f,.08f);}
        public static float ClosurePressure(float left,float right,bool valid,bool active)
        {return valid&&active?Mathf.InverseLerp(.28f,.70f,Mathf.Max(left,right)):0;}
        public void Observe(ArenaState state,int localId,float left,float right,bool valid,float dt)
        {
            bool active=false;
            foreach(var fighter in state.fighters)
            {
                if(previous.TryGetValue(fighter.id,out var old)&&old.alive&&!fighter.alive)
                {
                    EliminationImpact(old.seat);
                    Result(old.seat,fighter.name,Red);EliminationBursts++;
                    if(Time.unscaledTime>=eliminateSoundAt){Play("Eliminate",.56f);eliminateSoundAt=Time.unscaledTime+.15f;}LastEvent="Eliminated";
                    if(fighter.id==localId){Show("ELIMINATED\n+5 COINS / NOW SPECTATING",Red,2.8f);Flash(Red,.25f);Rumble(.32f,.18f);}
                }
                if(previous.TryGetValue(fighter.id,out var prior)&&fighter.roundWins>prior.wins)
                {
                    Vector3 winAt=ArenaLayout.Seat(state,fighter.id);PulseRing(winAt,Gold,1.6f,1.2f);Burst(winAt+Vector3.up*1.8f,Gold,16,1.6f);
                    if(fighter.id==localId&&fighter.alive){Show("ROUND WON\n+"+(10+fighter.roundWins*5)+" COINS / +25 XP",Gold,2.2f);Flash(Gold,.10f);Rumble(.12f,.10f);}
                }
                previous[fighter.id]=new Previous{alive=fighter.alive,wins=fighter.roundWins,seat=ArenaLayout.Seat(state,fighter.id)};
                if(fighter.id==localId&&fighter.alive&&fighter.pair>=0&&fighter.pair<state.pairs.Length&&state.pairs[fighter.pair].winner<0)active=true;
            }
            int tick=Mathf.CeilToInt(state.remaining);
            bool changed=phase!=state.phase;
            if(changed)
            {
                if(state.phase==(int)MatchPhase.Lobby){previous.Clear();ClearParticles();}
                if(state.phase==(int)MatchPhase.Duel)
                {
                    if(active)Show("HOLD YOUR STARE",Cyan,1.1f);PulseRing(Vector3.zero,Cyan,12,.85f);Play("Start",.27f);LastEvent="Duel";
                    foreach(var f in state.fighters)if(f.alive)Burst(ArenaLayout.Seat(state,f.id)+Vector3.up*.08f,Cyan,8,.85f);
                }
                if(state.phase==(int)MatchPhase.Countdown&&state.finalRound){if(active)Show("FINAL SHOWDOWN",Gold,1.8f);PulseRing(Vector3.zero,Gold,4,1.4f);}
                if(state.phase==(int)MatchPhase.Champion)
                {
                    var winner=System.Array.Find(state.fighters,f=>f.id==state.winner);
                    Vector3 at=winner!=null?ArenaLayout.Seat(state,winner.id):Vector3.zero;
                    Burst(at+Vector3.up*2.4f,Gold,128,4);PulseRing(at,Gold,5,2.4f);PulseRing(at,Cyan,3.2f,1.4f);
                    Result(at,winner!=null?winner.name:"CHAMPION",Gold,true);
                    Show(state.winner==localId?"CHAMPION!\n+50 COINS / +60 XP":"CHAMPION CROWNED",Gold,4);
                    Flash(Gold,.16f);Play("Victory",.28f);LastEvent="Champion";
                }
            }
            if(active&&state.phase==(int)MatchPhase.Countdown&&!state.trackingPaused&&tick!=second&&tick>=1&&tick<=3)
            {Show(tick.ToString(),state.finalRound?Gold:Cyan,.7f);Play("Tick",.19f);PulseRing(Vector3.zero,Cyan,2+tick,.6f);}
            if(state.phase==(int)MatchPhase.Lobby){landed.Clear();}
            bool trail=Time.unscaledTime>=trailAt;if(trail)trailAt=Time.unscaledTime+.06f;
            foreach(var toss in state.tosses)
            {
                if(toss.exploded)
                {
                    if(landed.Add(toss.id))
                    {
                        var color=toss.kind==0?Red:toss.kind==1?Cyan:Gold;
                        Burst(toss.burstPosition,color,32,.65f);PulseRing(toss.burstPosition,color,.85f,.38f,true);PulseRing(toss.burstPosition,Color.white,.40f,.20f,true);
                        float near=Mathf.Clamp01(1-Vector3.Distance(head.position,toss.burstPosition)/12);
                        if(Time.unscaledTime>=impactAt){Play("Impact",.04f+.22f*near);impactAt=Time.unscaledTime+.12f;}
                        if(toss.burstTarget==localId){Flash(color,.14f);Rumble(.12f,.10f);}
                        FaceBursts++;LastEvent="Face burst";
                        if(Debug.isDebugBuild)Debug.Log("[STARE FACE BURST] toss="+toss.id+" target="+toss.burstTarget+" position="+toss.burstPosition.ToString("F2"));
                    }
                    continue;
                }
                float age=Mathf.Max(0,state.clock-toss.born);
                Vector3 position=toss.origin+toss.velocity*age+Vector3.down*4.9f*age*age;
                if(position.y<=.05f&&!landed.Contains(toss.id))
                {
                    landed.Add(toss.id);
                    float hit=(toss.velocity.y+Mathf.Sqrt(Mathf.Max(0,toss.velocity.y*toss.velocity.y+19.6f*(toss.origin.y-.05f))))/9.8f;
                    position=toss.origin+toss.velocity*hit+Vector3.down*4.9f*hit*hit;position.y=.05f;
                    Burst(position,Cyan,12,.5f);PulseRing(position,Cyan,.65f,.35f);
                    if(Time.unscaledTime>=impactAt){Play("Impact",.09f);impactAt=Time.unscaledTime+.12f;}
                }
                else if(trail&&!landed.Contains(toss.id))Burst(position,toss.kind==0?Red:toss.kind==1?Cyan:Gold,1,.22f);
            }
            retired.Clear();foreach(int id in landed){bool found=false;foreach(var toss in state.tosses)if(toss.id==id){found=true;break;}if(!found)retired.Add(id);}foreach(int id in retired)landed.Remove(id);
            Fireworks.Observe(state);
            phase=state.phase;second=tick;
            active=active&&state.phase==(int)MatchPhase.Duel&&!state.trackingPaused;
            float target=ClosurePressure(left,right,valid,active);
            Pressure=Mathf.MoveTowards(Pressure,target,dt*(target>Pressure?9:12));
            if(Pressure>.15f&&Time.unscaledTime>=beatAt)
            {
                beatAt=Time.unscaledTime+Mathf.Lerp(1.05f,.36f,Pressure);
                if(sounds.TryGetValue("Heartbeat",out var clip))heartbeat.PlayOneShot(clip,.08f+Pressure*.10f);
                Rumble(.04f+Pressure*.06f,.055f);
            }
            if(!active){Pressure=0;if(heartbeat!=null)heartbeat.Stop();}
        }
        void LateUpdate()
        {
            if(!initialized)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            var announcer=GetComponent<StareAnnouncer>();sound.volume=announcer!=null&&announcer.IsSpeaking?.45f:1;
            Observe(game.State,game.LocalId,game.eyes.Left,game.eyes.Right,game.eyes.Valid,dt);
            eventGlow=Mathf.MoveTowards(eventGlow,0,dt*.55f);
            Color warning=Color.Lerp(Gold,Red,Pressure);warning.a=Pressure*.24f;
            if(eventGlow>warning.a){warning=eventColor;warning.a=eventGlow;}
            peripheral.SetColor("_Color",warning);edgeRenderer.enabled=warning.a>.001f;
            if(Time.unscaledTime>bannerUntil)
            {banner.text=Pressure>.18f?"HOLD STEADY":"";banner.color=warning;banner.color=new Color(warning.r,warning.g,warning.b,1);}
            else
            {float remaining=bannerUntil-Time.unscaledTime;var c=banner.color;c.a=Mathf.Clamp01(remaining*3);banner.color=c;}
            AdvanceEffects(dt);
            badgeRenderer.enabled=!string.IsNullOrEmpty(banner.text);
            if(badgeRenderer.enabled)
            {
                var bounds=banner.GetComponent<Renderer>().localBounds.size;
                float width=Mathf.Clamp(bounds.x+.05f,.18f,.92f),height=Mathf.Clamp(bounds.y+.035f,.10f,.23f);
                badgeMaterial.SetColor("_Accent",banner.color);badgeMaterial.SetVector("_Size",new Vector4(width,height,.025f,.003f));badge.localScale=new Vector3(width,height,1);
            }

            if(hapticUntil>0&&Time.unscaledTime>=hapticUntil){StopHaptics();hapticUntil=0;}
        }
        public void AdvanceEffects(float dt)
        {
            dt=Mathf.Clamp(dt,0,.05f);UpdateParticles(dt);UpdateCards(dt);Fireworks.Advance(dt);
            foreach(var r in rings)
            {
                if(!r.renderer.enabled)continue;r.age+=dt;float t=r.age/r.duration;
                if(t>=1){r.renderer.enabled=false;continue;}
                float radius=Mathf.Lerp(.16f,r.radius,1-Mathf.Pow(1-t,3));r.root.localScale=new Vector3(radius*2,radius*2,1);
                var c=r.color;c.a=Mathf.Pow(1-t,1.5f);r.properties.SetColor("_Color",c);r.properties.SetFloat("_Age",t);r.renderer.SetPropertyBlock(r.properties);
            }
        }
        void Show(string text,Color color,float duration){banner.text=text;banner.characterSize=(text.Length==1?.065f:.026f)*.42f;banner.fontStyle=FontStyle.Bold;banner.color=color;bannerUntil=Time.unscaledTime+duration;}
        void Flash(Color color,float amount){eventColor=color;eventGlow=amount;}
        void Rumble(float amount,float seconds)
        {
            if(!Application.isMobilePlatform)return;
            foreach(var hand in new[]{OVRInput.Controller.LTouch,OVRInput.Controller.RTouch})if(OVRInput.IsControllerConnected(hand))OVRInput.SetControllerVibration(.35f,amount,hand);
            hapticUntil=Time.unscaledTime+seconds;
        }
        void StopHaptics(){if(Application.isMobilePlatform){OVRInput.SetControllerVibration(0,0,OVRInput.Controller.LTouch);OVRInput.SetControllerVibration(0,0,OVRInput.Controller.RTouch);}}
        void Play(string name,float volume){if(sound!=null&&sounds.TryGetValue(name,out var clip))sound.PlayOneShot(clip,volume);}
        public void UI(bool click,Vector3 position)
        {
            if(!initialized)return;
            if(!click&&Time.unscaledTime<hoverAt)return;hoverAt=Time.unscaledTime+.10f;
            Play(click?"Click":"Hover",click?.16f:.07f);
            if(click)Burst(position,Cyan,8,.3f);
        }
        public void Burst(Vector3 position,Color color,int count,float duration)
        {
            for(int i=0;i<Mathf.Min(count,Capacity);i++)
            {
                float angle=i*2.399963f;var velocity=new Vector3(Mathf.Sin(angle),.6f+(i%7)*.14f,Mathf.Cos(angle))*(.4f+(i%5)*.24f);
                motes[moteIndex]=new Mote{position=position,velocity=velocity,life=duration,total=duration,size=.028f+(i%3)*.012f,color=color,gravity=.55f,stretch=2};moteIndex=(moteIndex+1)%Capacity;
            }
        }
        void EliminationImpact(Vector3 seat)
        {
            var center=seat+Vector3.up*1.1f;
            for(int i=0;i<56;i++){float a=i*2.399963f;var velocity=new Vector3(Mathf.Sin(a),.3f+(i%7)*.16f,Mathf.Cos(a))*(1.5f+(i%4)*.35f);Emit(center,velocity,i%7==0?Color.white:Color.Lerp(Red,Gold,(i%3)*.15f),.045f+(i%3)*.012f,.55f+(i%4)*.20f,1.3f,1.1f,3.2f);}
            RisingFragments(seat,Gold,20);
            PulseRing(seat,Red,3.1f,1.15f);PulseRing(seat,Color.white,1.35f,.30f);PulseRing(center,Gold,1.15f,.38f,true);
        }
        void Emit(Vector3 at,Vector3 velocity,Color color,float size,float duration,float gravity,float drag,float stretch)
        {motes[moteIndex]=new Mote{position=at,velocity=velocity,color=color,size=size,life=duration,total=duration,gravity=gravity,drag=drag,stretch=stretch};moteIndex=(moteIndex+1)%Capacity;}
        void RisingFragments(Vector3 ground,Color color,int count)
        {for(int i=0;i<count;i++){float a=i*2.399963f;var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));Emit(ground+radial*(.32f+(i%4)*.09f)+Vector3.up*(.15f+(i%7)*.20f),radial*.35f+Vector3.up*(1.25f+(i%5)*.3f),color,.035f,.9f+(i%4)*.15f,-.35f,.5f,2.4f);}}
        void PulseRing(Vector3 position,Color color,float radius,float duration,bool air=false)
        {var r=rings[ringIndex];ringIndex=(ringIndex+1)%RingCount;r.root.position=position+(air?Vector3.zero:Vector3.up*.042f);r.root.rotation=air?head.rotation:Quaternion.Euler(90,0,0);r.root.localScale=Vector3.one*.01f;r.renderer.enabled=true;r.age=0;r.duration=duration;r.radius=radius;r.color=color;}
        void Result(Vector3 at,string name,Color color,bool champion=false)
        {
            var card=cards[cardIndex];cardIndex=(cardIndex+1)%CardCount;card.ground=at;card.position=at+Vector3.up*(champion?2.75f:2.3f);card.age=0;card.duration=champion?4:2.15f;card.color=color;card.active=true;card.secondary=false;
            name=string.IsNullOrEmpty(name)?"CONTENDER":name;string display=name.Length>20?name.Substring(0,20):name;
            card.text.text=(champion?"CHAMPION":"ELIMINATED")+"\n"+display.ToUpperInvariant();card.root.gameObject.SetActive(true);
        }
        void UpdateCards(float dt)
        {
            foreach(var card in cards)
            {
                if(!card.active)continue;card.age+=dt;float t=card.age/card.duration;
                if(t>=1){card.active=false;card.root.gameObject.SetActive(false);continue;}
                if(!card.secondary&&card.age>.22f)
                {card.secondary=true;RisingFragments(card.ground,card.color,32);PulseRing(card.ground,card.color,2.1f,.95f);}
                card.root.position=card.position+Vector3.up*Mathf.SmoothStep(0,.18f,t);card.root.rotation=head.rotation;
                float entry=Mathf.Clamp01(card.age/.18f);card.root.localScale=Vector3.one*(1+2.15f*Mathf.Pow(entry-1,3)+1.15f*Mathf.Pow(entry-1,2));
                var color=card.color;color.a=Mathf.Min(entry,Mathf.Clamp01((1-t)*4));card.text.color=color;
            }
        }
        void UpdateParticles(float dt)
        {
            Vector3 right=head.right,up=head.up,forward=head.forward;var worldToLocal=transform.worldToLocalMatrix;int alive=0;
            for(int i=0;i<Capacity;i++)
            {
                var p=motes[i];int j=i*4;p.life-=dt;
                if(p.life<=0){colors[j]=colors[j+1]=colors[j+2]=colors[j+3]=Color.clear;motes[i]=p;continue;}
                p.velocity=p.velocity*Mathf.Exp(-p.drag*dt)+Vector3.down*dt*p.gravity;p.position+=p.velocity*dt;
                float fade=Mathf.Clamp01(p.life/p.total),size=p.size*(.4f+.6f*fade);Color c=p.color;c.a=fade*.8f;
                Vector3 axis=Vector3.ProjectOnPlane(p.velocity,forward);if(axis.sqrMagnitude<.001f)axis=up;axis.Normalize();Vector3 a=Vector3.Cross(forward,axis).normalized*size,b=axis*size*p.stretch;
                vertices[j]=worldToLocal.MultiplyPoint3x4(p.position-a-b);vertices[j+1]=worldToLocal.MultiplyPoint3x4(p.position+a-b);
                vertices[j+2]=worldToLocal.MultiplyPoint3x4(p.position+a+b);vertices[j+3]=worldToLocal.MultiplyPoint3x4(p.position-a+b);
                colors[j]=colors[j+1]=colors[j+2]=colors[j+3]=c;motes[i]=p;alive++;
            }
            ActiveParticles=alive;cloud.enabled=alive>0;if(alive>0){mesh.vertices=vertices;mesh.colors=colors;}
        }
        void ClearParticles(){for(int i=0;i<Capacity;i++)motes[i].life=0;foreach(var r in rings)r.renderer.enabled=false;foreach(var card in cards){card.active=false;card.root.gameObject.SetActive(false);}}
        void OnDisable(){StopHaptics();if(heartbeat!=null)heartbeat.Stop();if(edgeRenderer!=null)edgeRenderer.enabled=false;}
        void OnDestroy(){if(Instance==this)Instance=null;if(mesh!=null)Destroy(mesh);if(glow!=null)Destroy(glow);if(spark!=null)Destroy(spark);if(peripheral!=null)Destroy(peripheral);if(edge!=null)Destroy(edge.gameObject);if(banner!=null)Destroy(banner.gameObject);if(badge!=null)Destroy(badge.gameObject);if(badgeMaterial!=null)Destroy(badgeMaterial);}
    }
}
