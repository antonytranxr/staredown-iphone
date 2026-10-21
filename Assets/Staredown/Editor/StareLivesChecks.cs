using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Staredown;

public static class StareLivesChecks
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception("Lives/scoreboard: "+label);checks++;}
    static Tournament Room(int count,bool bots=false)
    {
        var room=new Tournament();for(int i=0;i<count;i++){room.Add(i,"Player "+i,bots);if(!bots)room.Input(i,0,0,true,true,0,0,0);}
        Check(room.Start(),count+" calibrated contestants start");return room;
    }
    static void Step(Tournament room,float duration,int missing=-1)
    {
        for(float time=0;time<duration-.00001f;)
        {
            float dt=Mathf.Min(.025f,duration-time);
            foreach(var fighter in room.Fighters)if(!fighter.bot&&!fighter.disconnected)room.Input(fighter.id,0,0,fighter.id!=missing,true,fighter.blinkSequence,0,0,false,fighter.phoneTrackingRequired);
            room.Tick(dt);time+=dt;
        }
    }
    static void Duel(Tournament room)
    {for(int i=0;i<420&&room.Phase!=MatchPhase.Duel;i++)Step(room,.025f);Check(room.Phase==MatchPhase.Duel,"countdown reaches Go");}
    static string Key(Pairing pair)=>Mathf.Min(pair.a,pair.b)+":"+Mathf.Max(pair.a,pair.b);
    static void Lose(Tournament room,int loser)
    {room.Blink(loser);Step(room,.125f);}

    public static string Rules()
    {
        checks=0;
        foreach(int count in new[]{2,3,4,5,8})
        {
            var room=Room(count);Check(room.LivesMode&&room.StartingLives==3&&room.Fighters.All(f=>f.lives==3),count+" uses three lives");
            var copy=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(room.Snapshot()));Check(copy.livesMode&&copy.startingLives==3&&copy.fighters.All(f=>f.lives==3),"lives format travels in authoritative snapshot");
        }
        foreach(int count in new[]{9,16})
        {
            var room=Room(count);Check(!room.LivesMode&&room.Fighters.All(f=>f.lives==1),count+" keeps knockout");Duel(room);var loser=room.Pairs[0].a;Lose(room,loser);Check(!room.Find(loser).alive&&room.Find(loser).lives==0,"large-room blink still eliminates immediately");
        }
        var filled=new Tournament();filled.Add(0,"Human");filled.EnableBotFill();filled.Input(0,0,0,true,true,0,0,0);Check(filled.Start()&&!filled.LivesMode&&filled.Fighters.Count==16,"bot-filled16 preserves knockout");
        var four=Room(4);var encountered=new HashSet<string>();
        for(int round=0;round<3;round++)
        {
            Duel(four);Check(four.Pairs.Count==2,"four contestants remain paired in parallel");
            foreach(var pair in four.Pairs)Check(encountered.Add(Key(pair)),"four-player room meets every opponent before repeating");
            foreach(var pair in four.Pairs)four.Blink(pair.a);Step(four,.125f);
            Check(four.Phase==MatchPhase.Intermission&&four.Fighters.Sum(f=>f.lives)==12-2*(round+1),"each resolved stare costs exactly one life");
            Check(four.Pairs.All(p=>p.winner>=0&&p.loser>=0&&four.Find(p.winner).roundWins>=1),"host serializes each resolved winner, loser and score");
            if(round<2){Check(four.Fighters.All(f=>f.alive),"early life loss keeps all four in the match");Step(four,4.025f);}
        }
        var odd=Room(5);var byeIds=new HashSet<int>();
        for(int round=0;round<3;round++)
        {
            Duel(odd);var byes=odd.Fighters.Where(f=>f.alive&&f.pair<0).ToArray();Check(byes.Length==1&&byeIds.Add(byes[0].id),"odd-room byes rotate fairly");int lives=byes[0].lives;
            foreach(var pair in odd.Pairs){var a=odd.Find(pair.a);var b=odd.Find(pair.b);odd.Blink(a.lives>=b.lives?a.id:b.id);}Step(odd,.125f);
            Check(byes[0].lives==lives,"bye costs no life and awards no phantom win");if(round<2)Step(odd,4.025f);
        }
        var final=Room(2);Check(final.FinalRound,"last two take center-stage immediately");
        for(int loss=1;loss<=3;loss++)
        {
            Duel(final);Lose(final,1);Check(final.Find(1).lives==3-loss&&final.Find(1).alive==(loss<3),"final showdown consumes remaining lives");
            if(loss<3)Check(final.Find(1).status.Contains("LIFE LOST")&&final.Find(1).status.Contains((3-loss)+" LEFT"),"life loss is clearly distinct from elimination");
            Step(final,4.025f);
            if(loss<3)Check(final.Phase==MatchPhase.Countdown&&final.Winner<0,"final is not over after only one blink");
        }
        Check(final.Phase==MatchPhase.Champion&&final.Winner==0&&final.Find(0).roundWins==3,"champion crowned only after opponent's last life");
        var champion=final.Snapshot();Check(champion.championName=="Player 0"&&StareScoreboard.ShowChampion(champion)&&!StareScoreboard.ShowEyes(champion),"winner replaces finalist eyes on the main screen");
        Step(final,8.025f);Check(final.Phase==MatchPhase.Lobby&&final.Winner==0&&final.ChampionName=="Player 0","last winner retained in social arena");
        final.Remove(0);Check(final.Snapshot().championName=="Player 0"&&StareScoreboard.ShowChampion(final.Snapshot()),"winner still displayed after they leave the room");
        final.Add(2,"Newcomer");final.Input(1,0,0,true,true,0,0,0);final.Input(2,0,0,true,true,0,0,0);Check(final.Start()&&final.Winner<0&&string.IsNullOrEmpty(final.ChampionName),"new match clears previous winner presentation");

        var phone=Room(4);phone.Find(0).phoneTrackingRequired=true;phone.Find(0).phoneCamera=false;Duel(phone);Step(phone,Tournament.PhoneFaceLossGrace+.2f);
        Check(phone.Find(0).alive&&phone.Find(0).lives==3&&phone.Find(0).faceMissingFor==0,"private eye tracking works with video sharing off");
        Step(phone,Tournament.PhoneFaceLossGrace+.2f,0);Check(phone.Find(0).alive&&phone.Find(0).lives==2&&phone.Find(0).status.StartsWith("FACE LOST"),"missing phone face forfeits one life without global reset");
        Check(!phone.TrackingPaused&&phone.Phase==MatchPhase.Duel,"other simultaneous pair keeps going during phone life loss");
        var captured=Room(2);captured.Find(0).phoneTrackingRequired=true;Duel(captured);
        var evidence=new StareCapturedBlink{matchToken=captured.MatchToken,round=captured.Round,duel=captured.Duel,sequence=1,valid=true,ready=true,age=.1f};
        Check(captured.AcceptCapturedBlink(0,evidence)=="confirmed","captured phone event accepted with private video");Step(captured,.125f);Check(captured.Find(0).lives==2,"captured blink consumes one life");Check(captured.AcceptCapturedBlink(0,evidence)=="confirmed"&&captured.Find(0).lives==2,"duplicate acknowledgement never consumes another life");
        var disconnect=Room(4);Duel(disconnect);var affected=disconnect.Pairs.First(p=>p.a==0||p.b==0);disconnect.Remove(0);Check(!disconnect.Find(0).alive&&disconnect.Find(0).lives==0&&affected.winner!=0&&affected.winner>=0,"disconnect forfeits all lives immediately");
        var tie=Room(2);Duel(tie);tie.Blink(0);tie.Blink(1);Step(tie,.125f);Check(tie.Fighters.All(f=>f.lives==3)&&tie.Pairs[0].winner<0&&tie.Pairs[0].cooldown>0,"double blink rematch never consumes a life");
        var allGone=Room(2);allGone.Remove(0);allGone.Remove(1);Step(allGone,10);Check(allGone.Winner<0,"disconnecting both contestants cannot invent a champion");
        var timeout=Room(2);Duel(timeout);timeout.Remaining=.01f;Step(timeout,.025f);Check(timeout.Phase==MatchPhase.Countdown&&timeout.Fighters.All(f=>f.lives==3),"timeout rematch costs no lives");
        foreach(int count in new[]{2,3,4,5,6,7,8,9,16})
        {
            var room=Room(count);int completed=0;
            for(int iteration=0;iteration<700&&room.Phase!=MatchPhase.Champion;iteration++)
            {
                if(room.Phase==MatchPhase.Countdown)Step(room,5.025f);
                else if(room.Phase==MatchPhase.Duel){foreach(var pair in room.Pairs.Where(p=>p.winner<0))room.Blink(pair.a);Step(room,.125f);completed++;}
                else Step(room,4.025f);
            }
            Check(room.Phase==MatchPhase.Champion&&room.Fighters.Count(f=>f.alive)==1,"complete "+count+"-player tournament converges");Check(completed>=1&&completed<=count*3,"life rounds always make bounded progress");
        }
        Check(StareScoreboard.Status(four.Snapshot(),four.Fighters.First(f=>f.alive)).Contains("LIFE"),"scoreboard displays remaining lives");
        Check(StareScoreboard.Title(four.Snapshot()).Contains("LIVES & SCORE"),"non-final screen shows score/status");
        return checks+" lives, opponent rotation, championship persistence, phone consent and scoreboard rule checks passed";
    }
    public static string Presentation()
    {
        checks=0;var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();if(view==null)throw new Exception("Start the arena Play scene first");
        var room=Room(4);var state=room.Snapshot();view.UpdateState(state,-99,false);
        Check(view.ScoreboardVisible&&!view.ChampionShowcaseVisible&&view.ScoreboardHeading.Contains("LIVES & SCORE"),"normal rounds display scoreboard");Check(view.ArenaFeedMode,"live arena remains behind scores");
        Duel(room);int loser=room.Pairs[0].a,winner=room.Pairs[0].b;Lose(room,loser);view.UpdateState(room.Snapshot(),loser,false);
        Check(view.roundText.text=="LIFE LOST / 2 LIVES LEFT","life loser never keeps seeing active stare instructions");view.UpdateState(room.Snapshot(),winner,false);Check(view.roundText.text=="ROUND WON / 3 LIVES LEFT","resolved winner gets clear waiting status");
        var odd=Room(3);int bye=odd.Fighters.First(f=>f.pair<0).id;view.UpdateState(odd.Snapshot(),bye,false);Check(view.roundText.text.Contains("BYE")&&!view.roundText.text.Contains("STARTS IN"),"unpaired bye never receives a competing countdown");
        var final=Room(2);view.UpdateState(final.Snapshot(),-99,false);Check(!view.ScoreboardVisible&&!view.ArenaFeedMode,"final countdown keeps actual finalist eye feed");
        for(int loss=0;loss<3;loss++){Duel(final);Lose(final,1);Step(final,4.025f);}view.UpdateState(final.Snapshot(),-99,false);
        Check(view.ScoreboardVisible&&view.ChampionShowcaseVisible&&view.ScoreboardHeading=="UNBLINKING CHAMPION","champion avatar takes over jumbotron after showdown");
        Step(final,8.025f);view.UpdateState(final.Snapshot(),-99,false);Check(view.ChampionShowcaseVisible&&view.ScoreboardHeading=="LAST WINNER","last champion remains on screen during social return");
        final.Remove(0);view.UpdateState(final.Snapshot(),-99,false);Check(view.ChampionShowcaseVisible,"last winner appearance survives leaving roster");return checks+" rendered arena scoreboard/champion/finalist presentation checks passed";
    }
}
