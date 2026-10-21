using System;
using System.Linq;
using UnityEngine;

namespace Staredown
{
    // These strings come only from the host snapshot. Every platform shows the
    // same score, remaining lives and result, even with local camera sharing off.
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
            return "ROUND "+state.round.ToString("00")+" / "+(state.livesMode?"LIVES & SCORE":"TOURNAMENT STATUS");
        }
        public static string Status(ArenaState state,Fighter fighter)
        {
            if(state.phase==(int)MatchPhase.Lobby)return fighter.ready&&fighter.valid?"READY":"CALIBRATING";
            if(!fighter.alive)return fighter.status=="DISCONNECTED"?"DISCONNECTED":"ELIMINATED / "+fighter.roundWins+" WINS";
            string score=state.livesMode?fighter.lives+" "+(fighter.lives==1?"LIFE":"LIVES")+" / "+fighter.roundWins+" WINS":fighter.roundWins+" WINS";
            if(fighter.pair<0)return score+" / BYE";
            if(fighter.pair<state.pairs.Length&&state.pairs[fighter.pair].winner>=0)
                return score+(state.pairs[fighter.pair].winner==fighter.id?" / WON":" / LIFE LOST");
            return score+(state.phase==(int)MatchPhase.Countdown?" / READY":" / STARING");
        }
        public static string Summary(ArenaState state)
        {
            if(state.phase==(int)MatchPhase.Lobby)return "EXPLORE & CHAT / HOST STARTS THE MATCH";
            int alive=state.fighters.Count(f=>f.alive);
            return state.trackingPaused?"HEADSET TRACKING LOST / PAUSED":alive+" STILL IN"+(state.livesMode?" / LOSE A STARE, LOSE A LIFE / OPPONENTS SWITCH":" / ONE BLINK ELIMINATES");
        }
    }

    public sealed partial class StareVisuals
    {
        Transform scoreboardRoot,scoreRows,championDisplay;
        TextMesh scoreboardTitle,scoreboardSummary,championLabel;
        readonly TextMesh[] scoreNames=new TextMesh[Tournament.Capacity],scoreValues=new TextMesh[Tournament.Capacity];
        readonly Transform[] scoreCards=new Transform[Tournament.Capacity];
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
            scoreboardTitle=Text("ARENA LOBBY",scoreboardRoot,new Vector3(0,2.15f,-.15f),.16f,Color.white);
            scoreboardSummary=Text("",scoreboardRoot,new Vector3(0,-2.3f,-.15f),.07f,Cyan);
            scoreRows=Group("Contestant scores",scoreboardRoot,Vector3.zero);
            var backing=Mat("Score card navy",new Color(.018f,.04f,.065f));
            for(int i=0;i<scoreCards.Length;i++)
            {
                scoreCards[i]=Group("Contestant score "+i,scoreRows,Vector3.zero);
                Shape(PrimitiveType.Cube,"Score card",scoreCards[i],new Vector3(0,0,.04f),new Vector3(4.1f,.78f,.04f),backing);
                scoreNames[i]=Text("",scoreCards[i],new Vector3(0,.14f,-.04f),.09f,Color.white);
                scoreValues[i]=Text("",scoreCards[i],new Vector3(0,-.16f,-.04f),.06f,Cyan);
            }
            championDisplay=Group("Last winner presentation",scoreboardRoot,Vector3.zero);
            championLabel=Text("",championDisplay,new Vector3(0,1.48f,-.25f),.15f,Gold);
            StareFloorReflection.Mark(scoreboardRoot,StareArenaMirror.CaptureSurfaceLayer);
        }
        void UpdateScoreboard(ArenaState state)
        {
            if(scoreboardRoot==null)BuildScoreboard();if(scoreboardRoot==null)return;
            bool visible=!StareScoreboard.ShowEyes(state);scoreboardRoot.gameObject.SetActive(visible);if(!visible)return;
            bool champion=StareScoreboard.ShowChampion(state);scoreRows.gameObject.SetActive(!champion);championDisplay.gameObject.SetActive(champion);
            ScoreText(scoreboardTitle,StareScoreboard.Title(state),18,.08f);
            ScoreText(scoreboardSummary,champion?(state.phase==(int)MatchPhase.Lobby?"EXPLORE & CHAT / READY FOR A REMATCH?":"THE ARENA HAS A NEW CHAMPION"):StareScoreboard.Summary(state),18,.037f);
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
            var ordered=state.fighters.OrderByDescending(f=>f.alive).ThenByDescending(f=>state.phase==(int)MatchPhase.Lobby?0:f.lives).ThenByDescending(f=>f.roundWins).ThenBy(f=>f.id).ToArray();
            int columns=ordered.Length<=4?2:4,rows=Mathf.Max(1,Mathf.CeilToInt((float)ordered.Length/columns));
            for(int i=0;i<scoreCards.Length;i++)
            {
                bool shown=i<ordered.Length;scoreCards[i].gameObject.SetActive(shown);if(!shown)continue;
                var fighter=ordered[i];scoreCards[i].localPosition=new Vector3((i%columns-(columns-1)*.5f)*4.4f,((rows-1)*.5f-i/columns)*.95f,0);
                ScoreText(scoreNames[i],fighter.DisplayName,3.8f,.038f);scoreNames[i].color=fighter.bot?Gold:Color.white;
                ScoreText(scoreValues[i],StareScoreboard.Status(state,fighter),3.85f,.024f);scoreValues[i].color=!fighter.alive?new Color(.55f,.64f,.7f):state.livesMode&&fighter.lives==1?Gold:Cyan;
            }
        }
    }
}
