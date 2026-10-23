using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Staredown
{
    // These strings come only from the host snapshot. Every platform shows the
    // same round status and result, even with local camera sharing off.
    public static class StareScoreboard
    {
        public static bool ShowChampion(ArenaState state)
        {return state.winner>=0&&!string.IsNullOrEmpty(state.championName)&&(state.phase==(int)MatchPhase.Champion||state.phase==(int)MatchPhase.Lobby);}
        public static bool ShowEyes(ArenaState state)
        {return state.finalRound&&!ShowChampion(state)&&state.phase!=(int)MatchPhase.Lobby;}
        public static string Title(ArenaState state)
        {
            if(ShowChampion(state))return state.phase==(int)MatchPhase.Champion?"UNBLINKING CHAMPION":"LAST WINNER";
            if(state.phase==(int)MatchPhase.Lobby)return "ARENA LOBBY";
            return "ROUND "+state.round.ToString("00")+" / TOURNAMENT STATUS";
        }
        public static string Status(ArenaState state,Fighter fighter)
        {
            if(!fighter.alive)return fighter.status=="DISCONNECTED"?"DISCONNECTED":fighter.status=="SPECTATOR / NEXT MATCH"?"NEXT MATCH":"ELIMINATED";
            if(state.phase==(int)MatchPhase.Lobby)return fighter.ready&&fighter.valid?"READY":"GETTING READY";
            if(fighter.pair<0)return "NEXT ROUND";
            if(fighter.pair<state.pairs.Length&&state.pairs[fighter.pair].winner>=0)return state.pairs[fighter.pair].loser==fighter.id?"HEART USED":"ROUND WON";
            return state.phase==(int)MatchPhase.Countdown?"STARTING SOON":state.phase==(int)MatchPhase.Intermission?"NEXT ROUND":"STARING";
        }

        public static string Summary(ArenaState state)
        {
            if(state.trackingPaused)return "HEADSET TRACKING LOST / PAUSED";
            if(!state.livesMode||state.startingLives<=1)return "NEXT LOSS = OUT";
            return state.phase==(int)MatchPhase.Lobby?"ONE SPARE HEART / LOSE TWICE AND YOU’RE OUT":"ONE SPARE HEART  /  SECOND LOSS = OUT";
        }
    }

    public sealed partial class StareVisuals
    {
        Transform scoreboardRoot,scoreRows,championDisplay;
        TextMesh scoreboardTitle,scoreboardSummary,scoreboardCount,championLabel;
        readonly TextMesh[] scoreNames=new TextMesh[Tournament.Capacity],scoreValues=new TextMesh[Tournament.Capacity];
        readonly Transform[] scoreCards=new Transform[Tournament.Capacity],scoreHearts=new Transform[Tournament.Capacity];
        readonly TextMesh[] scoreStatuses=new TextMesh[Tournament.Capacity];
        readonly Renderer[] scoreSurfaces=new Renderer[Tournament.Capacity],scoreBadges=new Renderer[Tournament.Capacity],scoreHeartRenderers=new Renderer[Tournament.Capacity];
        readonly List<Fighter> scoreOrder=new List<Fighter>(Tournament.Capacity);
        static readonly Color ScoreIn=new Color(.20f,1,.76f),ScoreOut=new Color(.55f,.60f,.69f);
        CreatureView championShowcase;string championShowcaseKey;
        public bool ScoreboardVisible=>scoreboardRoot!=null&&scoreboardRoot.gameObject.activeSelf;
        public bool ChampionShowcaseVisible=>championDisplay!=null&&championDisplay.gameObject.activeSelf;
        public string ScoreboardHeading=>scoreboardTitle==null?"":scoreboardTitle.text;
        static void ScoreText(TextMesh text,string value,float width,float size)
        {if(text.text==value)return;text.text=value;FitBoardLabel(text,width,size);}
        void BuildScoreboard()
        {
            if(portraitRoot==null)return;
            scoreboardRoot=Group("Live tournament scoreboard",portraitRoot,new Vector3(0,0,-1.85f));
            scoreboardTitle=UILabel("ARENA LOBBY",scoreboardRoot,new Vector3(-8.75f,2.26f,-.16f),.17f,Color.white,true);
            scoreboardCount=UILabel("",scoreboardRoot,new Vector3(8.75f,2.26f,-.16f),.12f,ScoreIn,true,TextAnchor.MiddleRight);
            scoreboardSummary=UILabel("",scoreboardRoot,new Vector3(0,-2.27f,-.15f),.08f,Cyan,false,TextAnchor.MiddleCenter);
            Panel("Tournament header rule",scoreboardRoot,new Vector3(0,1.91f,-.10f),new Vector2(17.5f,.014f),Cyan,Cyan,.002f,true);
            scoreRows=Group("Contestant scores",scoreboardRoot,Vector3.zero);
            for(int i=0;i<scoreCards.Length;i++)
            {
                scoreCards[i]=Group("Contestant score "+i,scoreRows,Vector3.zero);
                scoreSurfaces[i]=Panel("Roster card",scoreCards[i],new Vector3(0,0,.01f),new Vector2(4.2f,.78f),new Color(.018f,.04f,.065f),Cyan,.08f);
                scoreNames[i]=UILabel("",scoreCards[i],new Vector3(-1.29f,.14f,-.06f),.10f,Color.white,true);
                scoreValues[i]=UILabel("",scoreCards[i],new Vector3(-1.29f,-.17f,-.06f),.063f,Cyan);
                scoreBadges[i]=Panel("In or out badge",scoreCards[i],new Vector3(1.60f,0,-.035f),new Vector2(.73f,.52f),new Color(.015f,.10f,.10f),ScoreIn,.06f);
                scoreStatuses[i]=UILabel("IN",scoreCards[i],new Vector3(1.60f,0,-.065f),.15f,ScoreIn,true,TextAnchor.MiddleCenter);
                scoreHearts[i]=BuildLifeToken(scoreCards[i],"Roster 3D life",.37f);scoreHearts[i].localPosition=new Vector3(-1.75f,0,-.19f);
                scoreHeartRenderers[i]=scoreHearts[i].GetComponent<Renderer>();
            }
            championDisplay=Group("Last winner presentation",scoreboardRoot,Vector3.zero);
            championLabel=Text("",championDisplay,new Vector3(0,1.48f,-.25f),.15f,Gold);
            StareFloorReflection.Mark(scoreboardRoot,StareArenaMirror.CaptureSurfaceLayer);
        }
        void UpdateScoreboard(ArenaState state,int localId)
        {
            if(scoreboardRoot==null)BuildScoreboard();if(scoreboardRoot==null)return;
            bool visible=!StareScoreboard.ShowEyes(state);scoreboardRoot.gameObject.SetActive(visible);if(!visible)return;
            bool champion=StareScoreboard.ShowChampion(state);scoreRows.gameObject.SetActive(!champion);championDisplay.gameObject.SetActive(champion);
            ScoreText(scoreboardTitle,StareScoreboard.Title(state),12,.068f);
            int alive=state.fighters.Count(f=>f.alive);
            ScoreText(scoreboardCount,champion?"VICTORY":alive.ToString("00")+" IN  /  "+(state.fighters.Length-alive).ToString("00")+" OUT",5.1f,.045f);
            ScoreText(scoreboardSummary,champion?(state.phase==(int)MatchPhase.Lobby?"EXPLORE & CHAT / READY FOR A REMATCH?":"THE ARENA HAS A NEW CHAMPION"):StareScoreboard.Summary(state),18,.032f);
            if(champion)
            {
                ScoreText(championLabel,state.championName.ToUpperInvariant()+" / "+(state.championBot?"BOT":"HUMAN"),17,.075f);
                string key=StareAvatarCatalog.AppearanceKey(state.championAvatar,state.championAvatarUserId)+":"+state.championSkin;
                if(championShowcase==null||championShowcaseKey!=key)
                {
                    if(championShowcase!=null)Destroy(championShowcase.root.gameObject);
                    championShowcase=CreateAvatar(state.championAvatar,state.championAvatarUserId,championDisplay);
                    ApplySkin(championShowcase,state.championSkin);championShowcaseKey=key;
                    championShowcase.root.name="Champion avatar on jumbotron";championShowcase.root.localPosition=new Vector3(0,-2.05f,-.2f);championShowcase.root.localRotation=Quaternion.identity;championShowcase.root.localScale=Vector3.one*(state.championAvatar==6?1.05f:state.championAvatar>=5&&state.championAvatar<=8?1.35f:1.65f);
                    championShowcase.label.gameObject.SetActive(false);
                    var detail=championShowcase.preset.GetComponent<LODGroup>();if(detail!=null)detail.ForceLOD(0);
                    var animator=championShowcase.preset.GetComponent<Animator>();
                    if(animator!=null&&animator.runtimeAnimatorController!=null){animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Play("Dance",0,0);}
                    StareFloorReflection.Mark(championShowcase.root,StareArenaMirror.CaptureSurfaceLayer);
                }
                championShowcase.eyes.Set(.03f,.03f,0,0);
                if(championShowcase.meta!=null)
                {
                    championShowcase.meta.SetLocalView(false);championShowcase.meta.SetEyes(.03f,.03f,0,0);
                    foreach(var renderer in championShowcase.preset.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=championShowcase.meta.Loaded;
                }
                return;
            }
            // Stable slots make it easy to follow a friend as their IN badge
            // becomes OUT; no card reshuffling or per-frame sorted-array GC.
            scoreOrder.Clear();scoreOrder.AddRange(state.fighters);scoreOrder.Sort((a,b)=>a.id.CompareTo(b.id));
            int columns=scoreOrder.Count<=4?2:4,rows=Mathf.Max(1,Mathf.CeilToInt((float)scoreOrder.Count/columns));
            for(int i=0;i<scoreCards.Length;i++)
            {
                bool shown=i<scoreOrder.Count;scoreCards[i].gameObject.SetActive(shown);if(!shown)continue;
                var fighter=scoreOrder[i];scoreCards[i].localPosition=new Vector3((i%columns-(columns-1)*.5f)*4.4f,((rows-1)*.5f-i/columns)*.94f,0);
                bool active=fighter.alive,local=fighter.id==localId;Color accent=active?ScoreIn:ScoreOut;
                ScoreText(scoreNames[i],(fighter.name??"CONTENDER").ToUpperInvariant(),2.35f,.042f);scoreNames[i].color=active?Color.white:ScoreOut;
                ScoreText(scoreValues[i],(local?"YOU / ":"")+fighter.TypeLabel+" / "+StareScoreboard.Status(state,fighter),2.4f,.024f);scoreValues[i].color=active?Cyan:ScoreOut;
                ScoreText(scoreStatuses[i],active?"IN":"OUT",.63f,.055f);scoreStatuses[i].color=accent;
                scoreSurfaces[i].sharedMaterial.SetColor("_Accent",local?Gold:accent);scoreSurfaces[i].sharedMaterial.SetFloat("_Selected",local?.55f:.08f);
                scoreBadges[i].sharedMaterial.SetColor("_Accent",accent);scoreBadges[i].sharedMaterial.SetColor("_Color",active?new Color(.015f,.10f,.09f):new Color(.045f,.053f,.065f));
                scoreHearts[i].localRotation=Quaternion.Euler(0,active?Mathf.Sin(state.clock*.8f+i)*22:0,active?0:-16);
                scoreHeartRenderers[i].sharedMaterial=LifeTokenMaterial(active&&fighter.lives>1);
            }
        }
    }
}
