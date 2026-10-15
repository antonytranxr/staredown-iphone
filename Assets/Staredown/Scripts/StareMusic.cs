using UnityEngine;
namespace Staredown
{
    public sealed class StareMusic : MonoBehaviour
    {
        AudioSource source,battle,final,victory;StareGame game;StareAnnouncer announcer;int previousPhase=-1;
        public bool IsPlaying=>source!=null&&source.isPlaying;
        public bool Muted {get;private set;}
        public float Volume {get;private set;}=1;
        void Start()
        {
            game=GetComponent<StareGame>();announcer=GetComponent<StareAnnouncer>();
            Muted=PlayerPrefs.GetInt("Staredown.MusicMuted",0)!=0;
            Volume=Mathf.Clamp01(PlayerPrefs.GetFloat("Staredown.MusicVolume",1));
            source=Create("ArenaPulse",true);battle=Create("ArenaPulse",true);final=Create("ArenaFinal",true);victory=Create("ArenaVictory",false);
            // The lobby and battle share one theme; the championship is a separate composition.
            // Crossfade complete tracks rather than layering unrelated melodies.
            double start=AudioSettings.dspTime+.15;
            foreach(var stem in new[]{source,battle,final})if(stem.clip!=null)stem.PlayScheduled(start);
        }
        AudioSource Create(string clip,bool loop)
        {
            var audio=gameObject.AddComponent<AudioSource>();audio.clip=Resources.Load<AudioClip>("Audio/"+clip);
            audio.loop=loop;audio.playOnAwake=false;audio.spatialBlend=0;audio.volume=0;audio.priority=loop?160:64;return audio;
        }
        public void Toggle(){Muted=!Muted;PlayerPrefs.SetInt("Staredown.MusicMuted",Muted?1:0);PlayerPrefs.Save();}
        public void SetVolume(float volume)
        {Volume=Mathf.Clamp01(volume);Muted=false;PlayerPrefs.SetFloat("Staredown.MusicVolume",Volume);PlayerPrefs.SetInt("Staredown.MusicMuted",0);PlayerPrefs.Save();}
        void Update()
        {
            if(source==null||game==null)return;
            var state=game.State;int phase=state.phase;
            if(phase!=previousPhase)
            {
                if(phase==(int)MatchPhase.Champion&&victory.clip!=null)victory.Play();
                else if(phase==(int)MatchPhase.Lobby)victory.Stop();
                previousPhase=phase;
            }
            float gain=Muted||StareTutorialVideo.Presenting?0:Volume*(announcer!=null&&announcer.IsSpeaking?.22f:1);
            bool duel=phase==(int)MatchPhase.Duel, countdown=phase==(int)MatchPhase.Countdown;
            bool competition=(duel||countdown)&&!state.trackingPaused;
            bool championship=competition&&state.finalRound;
            bool celebrating=phase==(int)MatchPhase.Champion&&victory.isPlaying;
            Fade(source,(celebrating?0:state.trackingPaused?.07f:competition?0:phase==(int)MatchPhase.Intermission?.14f:.23f)*gain);
            Fade(battle,(competition&&!championship?(duel?.34f:.24f):0)*gain);
            Fade(final,(championship?(duel?.36f:.27f):0)*gain);
            Fade(victory,.42f*gain);
        }
        void Fade(AudioSource audio,float target)
        {
            // Fast duck for consonants, slower rise to avoid pumping between spoken numbers.
            if(StareTutorialVideo.Presenting){audio.volume=0;return;}
            audio.volume=Mathf.MoveTowards(audio.volume,target,Time.unscaledDeltaTime*(Muted?4:announcer!=null&&announcer.IsSpeaking?2.8f:.48f));
        }
    }
}
