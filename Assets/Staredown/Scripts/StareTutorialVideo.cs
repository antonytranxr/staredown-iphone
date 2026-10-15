using UnityEngine;
using UnityEngine.Video;
namespace Staredown
{
 // Only the opening/help panel decodes video; gameplay pays no decoder cost.
 public sealed class StareTutorialVideo : MonoBehaviour
 {
  static StareTutorialVideo current;bool failed;
  public static bool Presenting=>current!=null&&current.isActiveAndEnabled&&!current.failed&&current.player!=null&&current.player.clip!=null;
  VideoPlayer player;AudioSource audio;RenderTexture target;Material material;Texture poster;Renderer surface;
  public void Initialize(Renderer display)
  {
   surface=display;poster=Resources.Load<Texture2D>("Tutorial/InstructionPoster");material=new Material(Resources.Load<Shader>("StareVideoSurface"));material.mainTexture=poster;surface.sharedMaterial=material;
   target=new RenderTexture(1024,576,0){name="Instruction video screen"};
   player=gameObject.AddComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;player.waitForFirstFrame=true;player.skipOnDrop=true;player.audioOutputMode=VideoAudioOutputMode.AudioSource;
   audio=gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;audio.volume=1;audio.priority=8;
   player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=target;player.aspectRatio=VideoAspectRatio.FitInside;player.clip=Resources.Load<VideoClip>("Tutorial/StaredownInstructions");
   player.controlledAudioTrackCount=1;player.EnableAudioTrack(0,true);player.SetTargetAudioSource(0,audio);
   current=this;player.prepareCompleted+=Prepared;player.errorReceived+=Failed;if(isActiveAndEnabled)Prepare();
  }
  void Prepare(){if(player!=null&&player.clip!=null){failed=false;material.mainTexture=poster;player.Prepare();}}
  void Prepared(VideoPlayer source){if(!isActiveAndEnabled)return;material.mainTexture=target;if(audio!=null)audio.mute=false;source.Play();Debug.Log("[STARE TUTORIAL] clean narrated tutorial ready / "+source.length.ToString("F2")+"s / audio tracks="+source.audioTrackCount);}
  void Failed(VideoPlayer source,string error){failed=true;if(audio!=null){audio.mute=true;audio.Stop();}material.mainTexture=poster;Debug.LogWarning("[STARE TUTORIAL] Video unavailable; instruction poster remains visible: "+error);}
  void OnEnable(){Prepare();}
  void OnDisable(){if(player!=null)player.Stop();if(audio!=null){audio.mute=true;audio.Stop();}if(material!=null)material.mainTexture=poster;}
  void OnDestroy(){if(current==this)current=null;if(player!=null){player.prepareCompleted-=Prepared;player.errorReceived-=Failed;}if(target!=null){target.Release();Destroy(target);}if(material!=null)Destroy(material);}
 }
}
