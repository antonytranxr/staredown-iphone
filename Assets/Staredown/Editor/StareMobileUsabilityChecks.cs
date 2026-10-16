using System;
using System.Linq;
using UnityEngine;
using Staredown;

public static class StareMobileUsabilityChecks
{
 public static string ProfilesAndSettings()
 {
  var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();
  if(session==null)throw new Exception("Run the phone scene in Play first");
  string[] keys={"Staredown.PlayerName","Staredown.PlayerNameConfirmed","Staredown.MobileMusicEnabled","Staredown.MobileMusicVolume"};
  bool[] had=keys.Select(PlayerPrefs.HasKey).ToArray();
  string oldName=PlayerPrefs.GetString(keys[0],"");int oldConfirmed=PlayerPrefs.GetInt(keys[1],0),oldEnabled=PlayerPrefs.GetInt(keys[2],1);float oldVolume=PlayerPrefs.GetFloat(keys[3],.2f);
  var music=session.GetComponents<AudioSource>().FirstOrDefault(a=>a.clip!=null&&a.clip.name=="ArenaPulse");
  if(music==null)throw new Exception("Phone music source missing");
  bool mute=music.mute,landscape=session.LandscapeMode;float volume=music.volume;
  int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mobile usability: "+label);count++;};
  try
  {
   session.Leave();foreach(string key in keys)PlayerPrefs.DeleteKey(key);
   check(!session.HasPlayerName,"first launch has no chosen name");
   session.Practice();check(!session.Connected&&!session.Hosting,"practice is gated before a name is chosen");
   session.Host();check(!session.Connected&&!session.Busy,"room creation cannot bypass the name gate");
   session.Join("1234");check(!session.Connected&&!session.Busy,"room join cannot bypass the name gate");
   foreach(string value in new[]{"","   ","<>","---"}){session.SetPlayerName(value);check(!session.HasPlayerName,"invalid draft cannot become a placeholder entry: "+value);}
   PlayerPrefs.SetString(keys[0],"Player");check(!session.HasPlayerName,"old automatically saved Player placeholder requires confirmation");
   session.SetPlayerName("Player");check(session.HasPlayerName&&session.PlayerName=="Player","explicitly confirmed Player is a valid chosen name");
   PlayerPrefs.DeleteKey(keys[1]);PlayerPrefs.SetString(keys[0],"Legacy Ava");check(session.HasPlayerName,"an older real name remains available");
   session.SetPlayerName(" Ava Phone ");check(session.HasPlayerName&&session.PlayerName=="Ava Phone","confirmed name is sanitized and saved");
   session.Practice();check(session.Connected&&session.Hosting,"named player can enter practice");session.Leave();
   session.SetMusicEnabled(false);check(!session.MusicEnabled&&music.mute,"music off reaches actual AudioSource");
   session.SetMusicVolume(.37f);check(Mathf.Abs(session.MusicVolume-.37f)<.001f&&Mathf.Abs(music.volume-.37f)<.001f,"volume preference reaches actual AudioSource");
   check(music.mute,"changing volume does not silently unmute music");
   session.SetMusicEnabled(true);check(session.MusicEnabled&&!music.mute,"music can be restored");
   session.SetMusicVolume(2);check(session.MusicVolume==1&&music.volume==1,"volume is bounded");
   session.SetMusicVolume(float.NaN);check(session.MusicVolume==1,"invalid volume cannot poison audio settings");
   session.MoveInput=Vector2.one;session.LookInput=Vector2.one;session.SetLandscapeMode(true);
   check(session.LandscapeMode&&session.MoveInput==Vector2.zero&&session.LookInput==Vector2.zero,"orientation changes release pending movement");
   session.SetLandscapeMode(false);check(!session.LandscapeMode,"portrait mode can be selected again");
   return count+" mobile profile/settings/entry checks passed";
  }
  finally
  {
   session.Leave();session.SetLandscapeMode(landscape);music.mute=mute;music.volume=volume;
   PlayerPrefs.SetString(keys[0],oldName);PlayerPrefs.SetInt(keys[1],oldConfirmed);PlayerPrefs.SetInt(keys[2],oldEnabled);PlayerPrefs.SetFloat(keys[3],oldVolume);
   for(int i=0;i<keys.Length;i++)if(!had[i])PlayerPrefs.DeleteKey(keys[i]);PlayerPrefs.Save();
  }
 }
}
