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
        var room=new Tournament(1);for(int i=0;i<count;i++){room.Add(i,"Player "+i,bots);if(!bots)room.Input(i,0,0,true,true,0,0,0);}
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
        foreach(int count in new[]{2,3,4,5,8,9,16})
        {
            var room=Room(count);Check(!room.LivesMode&&room.StartingLives==1&&room.Fighters.All(f=>f.lives==1),count+" always uses one life");
            var copy=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(room.Snapshot()));Check(!copy.livesMode&&copy.startingLives==1&&copy.fighters.All(f=>f.lives==1),"single life travels in authoritative snapshot");
            Duel(room);var loser=room.Pairs[0].a;Lose(room,loser);Check(!room.Find(loser).alive&&room.Find(loser).lives==0,"first losing blink eliminates immediately");
        }
        var filled=new Tournament(1);filled.Add(0,"Human");filled.EnableBotFill();filled.Input(0,0,0,true,true,0,0,0);Check(filled.Start()&&!filled.LivesMode&&filled.Fighters.Count==16,"bot-filled16 preserves knockout");
        var four=Room(4);Duel(four);foreach(var pair in four.Pairs)four.Blink(pair.a);Step(four,.125f);
        Check(four.Phase==MatchPhase.Intermission&&four.Fighters.Count(f=>f.alive)==2&&four.Fighters.Sum(f=>f.lives)==2,"four-player opening round eliminates both losers");
        Check(four.Pairs.All(p=>p.winner>=0&&p.loser>=0&&four.Find(p.winner).roundWins==1),"resolved results and score are authoritative");
        Step(four,4.025f);Check(four.FinalRound&&four.Pairs.Count==1,"four-player match reaches final after one round");
        var odd=Room(5);Duel(odd);var bye=odd.Fighters.Single(f=>f.pair<0);foreach(var pair in odd.Pairs)odd.Blink(pair.a);Step(odd,.125f);
        Check(bye.alive&&bye.lives==1&&bye.roundWins==0,"bye costs no life and awards no phantom win");
        var final=Room(2);Check(final.FinalRound,"last two take center-stage immediately");Duel(final);Lose(final,1);Step(final,4.025f);
        Check(final.Phase==MatchPhase.Champion&&final.Winner==0&&final.Find(0).roundWins==1&&!final.Find(1).alive,"one final losing blink crowns champion");
        var champion=final.Snapshot();Check(champion.championName=="Player 0"&&StareScoreboard.ShowChampion(champion)&&!StareScoreboard.ShowEyes(champion),"winner replaces finalist eyes on the main screen");
        Step(final,8.025f);Check(final.Phase==MatchPhase.Lobby&&final.Winner==0&&final.ChampionName=="Player 0","last winner retained in social arena");
        final.Remove(0);Check(final.Snapshot().championName=="Player 0"&&StareScoreboard.ShowChampion(final.Snapshot()),"winner still displayed after they leave the room");
        final.Add(2,"Newcomer");final.Input(1,0,0,true,true,0,0,0);final.Input(2,0,0,true,true,0,0,0);Check(final.Start()&&final.Winner<0&&string.IsNullOrEmpty(final.ChampionName),"new match clears previous winner presentation");

        var phone=Room(4);phone.Find(0).phoneTrackingRequired=true;phone.Find(0).phoneCamera=false;Duel(phone);Step(phone,Tournament.PhoneFaceLossGrace+.2f);
        Check(phone.Find(0).alive&&phone.Find(0).lives==1&&phone.Find(0).faceMissingFor==0,"private eye tracking works with video sharing off");
        Step(phone,Tournament.PhoneFaceLossGrace+.2f,0);Check(!phone.Find(0).alive&&phone.Find(0).lives==0&&phone.Find(0).status.StartsWith("FACE LOST"),"missing phone face eliminates without global reset");
        Check(!phone.TrackingPaused&&phone.Phase==MatchPhase.Duel,"other simultaneous pair keeps going during phone life loss");
        var captured=Room(2);captured.Find(0).phoneTrackingRequired=true;Duel(captured);
        var evidence=new StareCapturedBlink{matchToken=captured.MatchToken,round=captured.Round,duel=captured.Duel,sequence=1,valid=true,ready=true,age=.1f};
        Check(captured.AcceptCapturedBlink(0,evidence)=="confirmed","captured phone event accepted with private video");Step(captured,.125f);Check(captured.Find(0).lives==0,"captured blink consumes one life");Check(captured.AcceptCapturedBlink(0,evidence)=="confirmed"&&captured.Find(0).lives==0,"duplicate acknowledgement never consumes another life");
        var disconnect=Room(4);Duel(disconnect);var affected=disconnect.Pairs.First(p=>p.a==0||p.b==0);disconnect.Remove(0);Check(!disconnect.Find(0).alive&&disconnect.Find(0).lives==0&&affected.winner!=0&&affected.winner>=0,"disconnect forfeits all lives immediately");
        var tie=Room(2);Duel(tie);tie.Blink(0);tie.Blink(1);Step(tie,.125f);Check(tie.Fighters.All(f=>f.lives==1)&&tie.Pairs[0].winner<0&&tie.Pairs[0].cooldown>0,"double blink rematch never consumes a life");
        var allGone=Room(2);allGone.Remove(0);allGone.Remove(1);Step(allGone,10);Check(allGone.Winner<0,"disconnecting both contestants cannot invent a champion");
        var timeout=Room(2);Duel(timeout);timeout.Remaining=.01f;Step(timeout,.025f);Check(timeout.Phase==MatchPhase.Countdown&&timeout.Fighters.All(f=>f.lives==1),"timeout rematch costs no lives");
        foreach(int count in new[]{2,3,4,5,6,7,8,9,16})
        {
            var room=Room(count);int completed=0;
            for(int iteration=0;iteration<700&&room.Phase!=MatchPhase.Champion;iteration++)
            {
                if(room.Phase==MatchPhase.Countdown)Step(room,5.025f);
                else if(room.Phase==MatchPhase.Duel){foreach(var pair in room.Pairs.Where(p=>p.winner<0))room.Blink(pair.a);Step(room,.125f);completed++;}
                else Step(room,4.025f);
            }
            Check(room.Phase==MatchPhase.Champion&&room.Fighters.Count(f=>f.alive)==1,"complete "+count+"-player tournament converges");Check(completed>=1&&completed<=Mathf.CeilToInt(Mathf.Log(count,2)),"life rounds always make bounded progress");
        }
        var full=Room(16);Duel(full);var disconnectedPair=full.Pairs.First(p=>p.a==3||p.b==3);full.Remove(3);int survivor=disconnectedPair.winner;
        Check(full.CanAdmitHuman,"departed player's seat can be reclaimed mid-match");var returning=full.JoinHuman(40,"Returning");
        Check(returning!=null&&!returning.alive&&returning.lives==0&&returning.pair<0&&full.Fighters.Count==16,"rejoin becomes spectator without adding contestants or reviving a life");
        Check(full.Find(survivor).alive&&disconnectedPair.winner==survivor,"rejoin cannot undo already resolved duel");
        Check(full.SetName(40,"Returned")&&full.SelectAvatar(40,5),"returning spectator retains chosen name and avatar");
        Check(!full.CanAdmitHuman&&full.JoinHuman(41,"Extra")==null,"full match rejects extra guest without deleting active bots");
        Step(full,.2f);Check(!returning.alive,"spectator input never restores a life");
        full.Phase=MatchPhase.Champion;full.ReturnToLobby();Check(returning.alive&&returning.lives==1&&full.Phase==MatchPhase.Lobby,"returning spectator competes in next match");
        return checks+" single-life, elimination, championship, phone tracking and spectator rejoin checks passed";
    }
    public static string Presentation()
    {
        checks=0;var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();if(view==null)throw new Exception("Start the arena Play scene first");
        var room=Room(4);var state=room.Snapshot();view.UpdateState(state,-99,false);
        Check(view.ScoreboardVisible&&!view.ChampionShowcaseVisible&&view.ScoreboardHeading.Contains("ROUND"),"normal rounds display scoreboard");Check(!view.ArenaFeedMode,"scoreboard replaces expensive live arena feed");
        Duel(room);int loser=room.Pairs[0].a,winner=room.Pairs[0].b;Lose(room,loser);view.UpdateState(room.Snapshot(),loser,false);
        Check(!view.roundText.text.Contains("STARE")&&!view.roundText.text.Contains("STARTS IN"),"eliminated player never keeps seeing active stare instructions");view.UpdateState(room.Snapshot(),winner,false);Check(view.roundText.text.Contains("ROUND WON"),"resolved winner gets clear waiting status");
        var odd=Room(3);int bye=odd.Fighters.First(f=>f.pair<0).id;view.UpdateState(odd.Snapshot(),bye,false);Check(view.roundText.text.Contains("BYE")&&!view.roundText.text.Contains("STARTS IN"),"unpaired bye never receives a competing countdown");
        var final=Room(2);view.UpdateState(final.Snapshot(),-99,false);Check(!view.ScoreboardVisible&&!view.ArenaFeedMode,"final countdown keeps actual finalist eye feed");
        Duel(final);Lose(final,1);Step(final,4.025f);view.UpdateState(final.Snapshot(),-99,false);
        Check(view.ScoreboardVisible&&view.ChampionShowcaseVisible&&view.ScoreboardHeading=="UNBLINKING CHAMPION","champion avatar takes over jumbotron after showdown");
        Step(final,8.025f);view.UpdateState(final.Snapshot(),-99,false);Check(view.ChampionShowcaseVisible&&view.ScoreboardHeading=="LAST WINNER","last champion remains on screen during social return");
        final.Remove(0);view.UpdateState(final.Snapshot(),-99,false);Check(view.ChampionShowcaseVisible,"last winner appearance survives leaving roster");return checks+" rendered arena scoreboard/champion/finalist presentation checks passed";
    }
}
