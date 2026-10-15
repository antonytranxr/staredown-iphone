#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System.Collections;
using UnityEngine;
namespace Staredown
{
 // USB-authorized development test hook. Absent from non-development builds.
 public sealed class StareDevelopmentLaunch : MonoBehaviour
 {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Initialize(){new GameObject("Development room test").AddComponent<StareDevelopmentLaunch>();}
  IEnumerator Start()
  {
   string code=null;bool blink=false;
   using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
   using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
   using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
   {code=intent.Call<string>("getStringExtra","stare_join_room");blink=intent.Call<bool>("getBooleanExtra","stare_probe_blink",false);}
   if(!StareRelayNetwork.ValidCode(code)){Destroy(gameObject);yield break;}
   yield return null;yield return null;
   var game=FindFirstObjectByType<StareGame>();if(game==null){Destroy(gameObject);yield break;}
   game.eyes.UseSimulation(true);game.Act("character-done");game.Act("key:CLEAR");
   foreach(char key in StareRelayNetwork.NormalizeCode(code))game.Act("key:"+key);
   game.Act("join");float until=Time.unscaledTime+75;
   while(Time.unscaledTime<until)
   {
    if(game.State.phase==(int)MatchPhase.Duel)
    {
     Debug.Log("[STARE ROOM PROBE] countdown and duel replicated");
     if(blink){game.eyes.RequestDemoBlink();Debug.Log("[STARE ROOM PROBE] demo blink requested");}
     break;
    }
    yield return null;
   }
   if(blink)
   {
    until=Time.unscaledTime+12;
    while(Time.unscaledTime<until)
    {foreach(var fighter in game.State.fighters)if(fighter.id==game.LocalId&&!fighter.alive){Debug.Log("[STARE ROOM PROBE] elimination replicated after demo blink");Destroy(gameObject);yield break;}yield return null;}
   }
   Destroy(gameObject);
  }
 }
}
#endif
