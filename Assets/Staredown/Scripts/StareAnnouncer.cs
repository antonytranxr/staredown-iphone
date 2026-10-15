using System.Collections.Generic;
using UnityEngine;
namespace Staredown
{
    // State snapshots also work for network clients; never queue stale countdown speech.
    public sealed class StareAnnouncerCues
    {
        int phase=-1, round=-1, second=-1, alive=-1; bool paused;
        public string Next(ArenaState state)
        {
            if(state==null)return null;
            int count=0;foreach(var fighter in state.fighters)if(fighter.alive)count++;
            int tick=Mathf.CeilToInt(state.remaining);
            bool changed=phase!=state.phase||round!=state.round, lost=alive>=0&&count<alive;
            bool wasPaused=paused; int oldSecond=second;
            phase=state.phase;round=state.round;second=tick;alive=count;paused=state.trackingPaused;
            if(paused)return !wasPaused?"Pause":null;
            if(changed)
            {
                if(phase==(int)MatchPhase.Lobby)return "Welcome";
                if(phase==(int)MatchPhase.Countdown)return state.finalRound?"Final":"Round";
                if(phase==(int)MatchPhase.Duel)return "Fight";
                if(phase==(int)MatchPhase.Intermission)return "Complete";
                if(phase==(int)MatchPhase.Champion)return "Champion";
            }
            if(phase==(int)MatchPhase.Countdown)
            {
                if(tick!=oldSecond&&tick>=1&&tick<=3)return tick==3?"Three":tick==2?"Two":"One";
                // An early blink can reset the countdown. Cancel the previous number.
                if(tick>oldSecond)return "Round";
            }
            if(wasPaused)return "Resume";
            if(lost&&phase==(int)MatchPhase.Duel)return "Eliminated";
            return null;
        }
    }
    public sealed class StareAnnouncer : MonoBehaviour
    {
        readonly StareAnnouncerCues cues=new StareAnnouncerCues();
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        AudioSource voice; StareGame game;float eliminationAt;
        public bool IsSpeaking=>voice!=null&&voice.isPlaying;
        public string LastCue {get;private set;}="";
        void Start()
        {
            game=GetComponent<StareGame>();
            foreach(var clip in Resources.LoadAll<AudioClip>("Audio/Announcer"))clips[clip.name]=clip;
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;
            voice.volume=.82f;voice.priority=16;
        }
        void Update()
        {
            string cue=cues.Next(game!=null?game.State:null);if(StareTutorialVideo.Presenting){if(voice!=null&&voice.isPlaying)voice.Stop();return;}if(cue==null)return;
            if(cue=="Eliminated")
            {
                if(IsSpeaking||Time.unscaledTime<eliminationAt)return;
                eliminationAt=Time.unscaledTime+3;
            }
            Play(cue);
        }
        void Play(string cue)
        {
            if(!clips.TryGetValue(cue,out var clip)){Debug.LogWarning("[STARE AUDIO] Missing announcer cue: "+cue);return;}
            voice.Stop();voice.clip=clip;voice.Play();LastCue=cue;
            Debug.Log("[STARE AUDIO] announcer="+cue+" duration="+clip.length.ToString("F2"));
        }
        void OnDisable(){if(voice!=null)voice.Stop();}
    }
}
