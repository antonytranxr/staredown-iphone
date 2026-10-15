using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Staredown
{
    public sealed class EyePortrait
    {
        public Transform left, right, pupilL, pupilR;
        Vector3 baseLeft=Vector3.one, baseRight=Vector3.one, basePupilL, basePupilR;
        Vector3 pupilUnitsL=Vector3.one,pupilUnitsR=Vector3.one;
        bool bound;
        public void Bind()
        {
            baseLeft=left.localScale;baseRight=right.localScale;
            if(pupilL!=null){basePupilL=pupilL.localPosition;pupilUnitsL=LocalUnits(pupilL);}
            if(pupilR!=null){basePupilR=pupilR.localPosition;pupilUnitsR=LocalUnits(pupilR);}
            bound=true;
        }
        static Vector3 LocalUnits(Transform pupil)
        {
            // FBX imports may retain a 100x centimeter conversion on the root.
            // Store meter-to-local conversion before the preview adds its scale.
            var scale=pupil.parent.lossyScale;
            return new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
        }
        public void Set(float l, float r, float yaw, float pitch)
        {
            if(left==null||right==null)return;
            if(bound)
            {
                left.localScale=Vector3.Scale(baseLeft,new Vector3(1,Mathf.Max(.035f,1-l),1));
                right.localScale=Vector3.Scale(baseRight,new Vector3(1,Mathf.Max(.035f,1-r),1));
                var shift=new Vector3(Mathf.Clamp(yaw/180,-.14f,.14f)*.045f,-Mathf.Clamp(pitch,-60,60)/300*.035f,0);
                if(pupilL!=null)pupilL.localPosition=basePupilL+Vector3.Scale(shift,pupilUnitsL);
                if(pupilR!=null)pupilR.localPosition=basePupilR+Vector3.Scale(shift,pupilUnitsR);
                return;
            }
            left.localScale = new Vector3(1, Mathf.Max(.035f, 1 - l), 1);
            right.localScale = new Vector3(1, Mathf.Max(.035f, 1 - r), 1);
            pupilL.localPosition = new Vector3(Mathf.Clamp(yaw / 180, -.14f, .14f), -.01f - pitch / 300, -.105f);
            pupilR.localPosition = pupilL.localPosition;
        }
    }
    public sealed class CreatureView
    {
        public Transform root; public EyePortrait eyes; public TextMesh label; public Renderer body;
        public string appearanceKey;
        public Transform humanMarker;
        public Transform preset; public StareMetaAvatarView meta; public StareContestantMotion motion;
        public StareAvatarPresence presence;
        public StareCameraHead cameraHead; public StareSelfAvatar self;
    }
    public sealed partial class StareVisuals : MonoBehaviour
    {
        public static readonly Color Cyan = new Color(.12f, .88f, 1), Coral = new Color(1, .31f, .13f), Violet = new Color(.65f, .32f, 1), Gold = new Color(1, .76f, .25f);
        readonly Dictionary<int, CreatureView> avatars = new Dictionary<int, CreatureView>();
        readonly Dictionary<int, GameObject> balls = new Dictionary<int, GameObject>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public TextMesh headline, subline, boardLeft, boardRight, lobbyStatus, ipText, hud, roundText;
        public Transform menu, portraitRoot; public EyePortrait portraitL, portraitR;
        public Transform LocalViewer {get;private set;}
        public CreatureView LocalAvatar=>avatars.Values.FirstOrDefault(a=>a.self!=null&&a.self.Viewing);
        public void SetLocalViewer(Transform viewer){LocalViewer=viewer;}
        public void SetLocalMetaCapture(bool capture){Shader.SetGlobalFloat("_StareExternalView",capture?1:0);foreach(var a in avatars.Values)if(a.self!=null&&a.meta!=null)a.meta.SetLocalView(!capture&&a.self.Viewing);}
        public Camera overview; Font arenaFont; Material worldText;
        public Material Mat(string name, Color color, bool glow = false)
        {
            if (materials.TryGetValue(name, out var value)) return value;
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .62f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", glow ? .1f : .32f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.8f); }
            materials[name] = m; return m;
        }
        public GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Destroy(g.GetComponent<Collider>());
            return g;
        }
        public TextMesh Text(string text, Transform parent, Vector3 pos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var g = new GameObject(text); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
            if(arenaFont==null){arenaFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");arenaFont.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /:.!-",80);worldText=new Material(Resources.Load<Shader>("StareWorldText"));worldText.mainTexture=arenaFont.material.mainTexture;Font.textureRebuilt+=RefreshFont;}
            var t = g.AddComponent<TextMesh>(); t.font=arenaFont;t.GetComponent<Renderer>().sharedMaterial=worldText; t.text = text; t.fontSize = 80; t.characterSize = size * .42f; t.anchor = anchor; t.alignment = TextAlignment.Center; t.color = color;
            return t;
        }
        void RefreshFont(Font f){if(f==arenaFont&&worldText!=null)worldText.mainTexture=f.material.mainTexture;}
        public void Ring(string name, Transform parent, Vector3 pos, float radius, float width, Color color, int segments = 96)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
            var line = g.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = segments; line.widthMultiplier = width;
            line.sharedMaterial = Mat("ring" + color, color, true);
            for (int i = 0; i < segments; i++) { float a = i * Mathf.PI * 2 / segments; line.SetPosition(i, new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius)); }
        }
        public void Build()
        {
            BuildReferenceArena();
            var balcony=Group("Spectator seating walkway",transform,Vector3.zero);
            var floor=Band("Spectator walkway floor",balcony,13.95f,1.5f,StareSpectatorArea.Floor,Mat("Spectator walkway",new Color(.40f,.46f,.51f)));
            floor.gameObject.AddComponent<MeshCollider>().sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;
            GlowRing(balcony,StareSpectatorArea.InnerRadius,.06f,StareSpectatorArea.Floor+.02f,Cyan,.45f);
            GlowRing(balcony,StareSpectatorArea.OuterRadius,.06f,StareSpectatorArea.Floor+.02f,Gold,.45f);
        }
        public EyePortrait Eyes(Transform parent, Vector3 pos, float scale, Color iris)
        {
            var root=new GameObject("Live eyelids").transform;root.SetParent(parent,false);root.localPosition=pos;root.localScale=Vector3.one*scale;
            var ep=new EyePortrait();
            for(int i=0;i<2;i++)
            {
                var eye=new GameObject(i==0?"Left eyelid":"Right eyelid").transform;eye.SetParent(root,false);eye.localPosition=new Vector3(i==0?-.24f:.24f,0,0);
                Shape(PrimitiveType.Sphere,"Eye glow",eye,Vector3.zero,new Vector3(.38f,.25f,.16f),Mat("iris"+iris,iris,true));
                var p=Shape(PrimitiveType.Sphere,"Slit pupil",eye,new Vector3(0,-.01f,-.105f),new Vector3(.07f,.21f,.04f),Mat("obsidian",new Color(.005f,.008f,.014f))).transform;
                Shape(PrimitiveType.Sphere,"Catch light",eye,new Vector3(-.06f,.06f,-.11f),Vector3.one*.035f,Mat("white neon",Color.white,true));
                if(i==0){ep.left=eye;ep.pupilL=p;}else{ep.right=eye;ep.pupilR=p;}
            }
            return ep;
        }
        public CreatureView Creature(int id, Transform parent) => CreateAvatar(StareAvatarCatalog.PresetId(id), null, parent);
        public CreatureView AvatarFor(int id){CreatureView avatar;return avatars.TryGetValue(id,out avatar)?avatar:null;}
        public void UpdateState(ArenaState s,int localId,bool broadcast)
        {
            UpdatePads(s);
            foreach(var f in s.fighters)
            {
                string key=StareAvatarCatalog.AppearanceKey(f.avatar,f.avatarUserId)+":"+f.skin;
                if(!avatars.TryGetValue(f.id,out var a)||a.appearanceKey!=key)
                {
                    if(a!=null)Destroy(a.root.gameObject);
                    a=CreateAvatar(f.avatar,f.avatarUserId,transform,f.id==localId);ApplySkin(a,f.skin);a.appearanceKey=key;
                    a.motion=a.root.gameObject.AddComponent<StareContestantMotion>();a.motion.Bind(a.preset,a.meta!=null);avatars[f.id]=a;
                }
                bool self=f.id==localId&&LocalViewer!=null&&!broadcast;
                a.root.gameObject.SetActive(f.id!=localId || broadcast || a.meta!=null || self);
                if(self&&a.self==null){a.self=a.root.gameObject.AddComponent<StareSelfAvatar>();a.self.Initialize(a);}
                if(a.self!=null)a.self.SetViewer(self?LocalViewer:null);
                a.motion.Present(f,s,ArenaLayout.Seat(s,f.id),Quaternion.LookRotation(-ArenaLayout.Facing(s,f.id)));
                a.presence.Observe(f,s,a.motion.Eliminating);
                if(a.humanMarker==null){a.humanMarker=BuildHumanMarker(a.root);}a.humanMarker.gameObject.SetActive(!f.bot&&s.fighters.Any(other=>other.bot));a.humanMarker.localPosition=new Vector3(0,2.25f+.035f*Mathf.Sin(s.clock*2+f.id),0);a.humanMarker.localRotation=Quaternion.Euler(0,s.clock*35,0);
                int reflectionLayer=f.alive||self?StareFloorReflection.ReflectionLayer:0;
                if(a.root.gameObject.layer!=reflectionLayer)StareFloorReflection.Mark(a.root,reflectionLayer);
                
                var labelCam=broadcast?overview:Camera.main; if(labelCam!=null){a.label.transform.rotation=labelCam.transform.rotation;a.label.gameObject.SetActive(!broadcast && f.id!=localId && Vector3.Distance(labelCam.transform.position,a.root.position)<5.5f);}
                float closure=f.bot&&a.motion.Eliminating?1:-1;
                a.eyes.Set(closure>=0?closure:f.left,closure>=0?closure:f.right,f.yaw,f.pitch);string label=f.DisplayName+ (!f.bot&&f.demo?" / DEMO":"")+"\n"+(f.alive?f.status:"SPECTATOR");if(a.label.text!=label)a.label.text=label;a.label.color=f.bot?Gold:Cyan;
                if(a.meta!=null)
                {
                    a.meta.SetEyes(f.left,f.right,f.yaw,f.pitch);
                    a.meta.SetMouth(f.presence.mouth);
                    // Use the SDK first-person mesh in the HMD. Scene captures
                    // temporarily request the complete third-person mesh.
                    a.meta.SetLocalView(self);
                    a.meta.SetDanceSource(a.presence);
                    if(a.preset!=a.root)
                    {
                        a.preset.gameObject.SetActive((!a.meta.Loaded||f.dancing)&&(f.id!=localId||broadcast||self));
                        foreach(var renderer in a.preset.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=a.meta.Loaded;
                    }
                }
            }
            foreach(var kv in avatars) if(!s.fighters.Any(f=>f.id==kv.Key)) kv.Value.root.gameObject.SetActive(false);
            var lastChampion=s.phase==(int)MatchPhase.Lobby?System.Array.Find(s.fighters,f=>f.id==s.winner):null;
            headline.text=s.phase==(int)MatchPhase.Lobby?(lastChampion==null?"S T A R E D O W N":lastChampion.DisplayName+" / CHAMPION"):s.announcement;
            if(headline.text.Length>35) headline.characterSize=.072f;else headline.characterSize=.13f;
            int alive=s.fighters.Count(f=>f.alive);
            subline.text=s.trackingPaused?"TRACKING LOST / ROUND PAUSED":s.phase==(int)MatchPhase.Lobby?(s.fighters.Length>0?"FREE ROAM     /     WALK AROUND AND CHAT":"16 CONTENDERS     /     ONE UNBLINKING CHAMPION"):$"ROUND {s.round:00}     /     {alive:00} STILL STANDING";
            roundText.text=s.phase==(int)MatchPhase.Duel?$"HOLD YOUR STARE     {Mathf.CeilToInt(s.remaining):00}s":s.phase==(int)MatchPhase.Countdown?$"BLINK NOW / STARTS IN {Mathf.CeilToInt(s.remaining)}":s.phase==(int)MatchPhase.Champion?"CHAMPION":s.phase==(int)MatchPhase.Lobby?"HOST STARTS THE MATCH":"THE FINAL STARE";
            var local=System.Array.Find(s.fighters,f=>f.id==localId);
            if(!broadcast&&local!=null&&!local.alive&&s.phase!=(int)MatchPhase.Lobby&&s.phase!=(int)MatchPhase.Champion)roundText.text=$"ROUND {s.round:00}     /     SPECTATING";
            Fighter left=null,right=null;
            if(s.finalRound && s.pairs.Length>0){left=System.Array.Find(s.fighters,f=>f.id==s.pairs[0].a);right=System.Array.Find(s.fighters,f=>f.id==s.pairs[0].b);}
            if(left==null){left=s.fighters.FirstOrDefault(f=>f.alive);right=s.fighters.LastOrDefault(f=>f.alive);}
            if(left!=null){portraitL.Set(left.left,left.right,left.yaw,left.pitch);boardLeft.text=left.DisplayName;boardLeft.color=left.bot?Gold:Cyan;}
            if(right!=null){portraitR.Set(right.left,right.right,right.yaw,right.pitch);boardRight.text=right.DisplayName;boardRight.color=right.bot?Gold:Cyan;}
            bool exploring=s.phase==(int)MatchPhase.Lobby;
            if(exploring){boardLeft.text="ARENA LIVE";boardRight.text="EXPLORE & CHAT";UpdateArenaFeed();}
            eyeMural?.SetFloat("_ArenaMode",exploring?1:0);
            if(!exploring)UpdateEyeMural(left,right);
            FitBoardLabel(boardLeft,7.8f,.052f);FitBoardLabel(boardRight,7.8f,.052f);
            UpdateReferencePresentation(s,broadcast);
            foreach(var toss in s.tosses)
            {
                if(!balls.TryGetValue(toss.id,out var ball))
                {
                    ball=Shape(toss.kind==1?PrimitiveType.Cube:PrimitiveType.Sphere,"Spectator distraction",transform,Vector3.zero,Vector3.one*(toss.kind==2? .25f:.18f),Mat("prop"+toss.kind,toss.kind==0?Coral:toss.kind==1?Cyan:Gold,true));balls[toss.id]=ball;
                }
                ball.transform.position=StareProjectile.Position(toss,s.clock);ball.transform.Rotate(90*Time.deltaTime,120*Time.deltaTime,0);ball.SetActive(!toss.exploded&&ball.transform.position.y>.05f);
            }
            foreach(int id in balls.Keys.Where(k=>!s.tosses.Any(t=>t.id==k)).ToArray()){Destroy(balls[id]);balls.Remove(id);}
        }
    }
}
