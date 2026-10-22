using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Staredown;

public static class StareArenaRosterChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(StareVisuals view,string name)=>(T)typeof(StareVisuals).GetField(name,Private).GetValue(view);
    public static ArenaState Fixture()
    {
        var state=new ArenaState{phase=(int)MatchPhase.Duel,round=2,clock=8,remaining=22,startingLives=1};
        string[] names={"ANT","KAI","MIDNIGHT KNIGHT","NOVA","OBSIDIAN","EMBER","FROST","MILO","VERDANT","COSMO","JUNO","ORBIT","LUNA","ATLAS","PIXEL","SAGE"};
        state.fighters=Enumerable.Range(0,16).Select(i=>new Fighter{id=i,name=names[i],bot=i>=4,avatar=i%9,alive=i%4!=3,lives=i%4==3?0:1,ready=true,valid=true,pair=i/2,status=i%4==3?"ELIMINATED":"STARING",roundWins=i%2}).ToArray();
        state.pairs=Enumerable.Range(0,8).Select(i=>new Pairing{a=i*2,b=i*2+1}).ToArray();return state;
    }
    public static string Run()
    {
        var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();if(view==null)throw new Exception("Play the arena first");
        int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception("Arena roster: "+label);checks++;}
        var state=Fixture();view.UpdateState(state,0,false);
        Check(view.ScoreboardMode&&view.ScoreboardVisible&&!view.ArenaFeedMode&&view.ArenaFeed==null,"physical screen shows roster without a render camera");
        Check(!view.GetComponentsInChildren<Camera>(true).Any(c=>c.name=="Jumbotron arena camera"),"no hidden arena camera allocated");
        var cards=Field<Transform[]>(view,"scoreCards");var statuses=Field<TextMesh[]>(view,"scoreStatuses");var names=Field<TextMesh[]>(view,"scoreNames");var details=Field<TextMesh[]>(view,"scoreValues");var hearts=Field<Transform[]>(view,"scoreHearts");
        Check(cards.Count(c=>c.gameObject.activeSelf)==16,"all16 contestants fit on the actual screen");
        Check(Field<TextMesh>(view,"scoreboardCount").text=="12 IN  /  04 OUT","remaining and eliminated totals agree with authoritative state");
        Check(details[0].text.StartsWith("YOU / HUMAN"),"own life is identifiable from the floor");
        for(int i=0;i<16;i++)
        {
            Check(statuses[i].text==(state.fighters[i].alive?"IN":"OUT"),"correct badge "+i);
            Check(Mathf.Abs(cards[i].localPosition.x)+2.1f<10&&Mathf.Abs(cards[i].localPosition.y)+.39f<1.9f,"card fits physical screen "+i);
            Check(hearts[i].GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.3f,"life is volumetric geometry "+i);
            Check(names[i].GetComponent<Renderer>().bounds.size.x<=2.36f,"long names stay within card "+i);
            var avatar=view.AvatarFor(i);Check(avatar.lifeToken!=null&&avatar.lifeToken.gameObject.activeSelf==state.fighters[i].alive,"only live contestants keep world life "+i);
            Check(avatar.lifeToken.localPosition.y>avatar.label.transform.localPosition.y+.25f,"heart stays above face and nameplate "+i);
        }
        var stable=cards[0].localPosition;state.fighters[0].alive=false;state.fighters[0].lives=0;view.UpdateState(state,0,false);
        Check(cards[0].localPosition==stable&&statuses[0].text=="OUT","elimination updates in place without reshuffling names");
        state.clock+=.6f;view.UpdateState(state,0,false);Check(!view.AvatarFor(0).lifeToken.gameObject.activeSelf,"depleted world heart finishes brief departure");
        state.finalRound=true;view.UpdateState(state,0,false);Check(!view.ScoreboardVisible&&!view.ScoreboardMode,"final showdown hands screen back to closeup eyes");
        state.phase=(int)MatchPhase.Champion;state.winner=1;state.championName="KAI";state.championAvatar=1;view.UpdateState(state,0,false);Check(view.ChampionShowcaseVisible&&view.ScoreboardMode,"winner replaces final eyes");
        state.phase=(int)MatchPhase.Lobby;view.UpdateState(state,0,false);Check(view.ChampionShowcaseVisible&&view.ScoreboardHeading=="LAST WINNER","last winner survives social return");
        var mirror=view.GetComponentInChildren<StareArenaMirror>();Check(mirror!=null&&Mathf.Abs(mirror.transform.localScale.x-4.8f)<.001f&&Mathf.Abs(mirror.transform.localScale.y-2.88f)<.001f,"mirror pane twice previous width and same height");
        var backing=mirror.transform.parent.Find("Mirror backing");Check(Mathf.Abs(backing.localScale.x-5.1f)<.001f,"frame doubles with mirror surface");
        return checks+" arena roster /3D life /wide mirror checks passed";
    }
    public static string Render(string path)
    {
        var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();view.UpdateState(Fixture(),0,false);
        return StareExperienceChecks.Render(path,new Vector3(0,6.6f,-6),new Vector3(0,6.6f,12));
    }
}
