using UnityEngine;

namespace Staredown
{
    // A small world-space instrument above the left controller, outside its UI ray.
    public sealed class StareHeadsetDiagnostics : MonoBehaviour
    {
        OVRCameraRig rig; StareEyes eyes; Transform panel; TextMesh fpsText, details;
        public bool Visible {get;private set;}
        public void Toggle(){Visible=!Visible;PlayerPrefs.SetInt("Staredown.FpsVisible",Visible?1:0);PlayerPrefs.Save();if(panel!=null)panel.gameObject.SetActive(Visible);}
        int frames; float elapsed, worst, nextLog; float fps, milliseconds, worstMilliseconds;
        public void Initialize(OVRCameraRig cameraRig, StareEyes tracker, StareVisuals visuals)
        {
            rig=cameraRig;eyes=tracker;Visible=PlayerPrefs.GetInt("Staredown.FpsVisible",0)!=0;
            panel=new GameObject("Left controller diagnostics").transform;
            panel.SetParent(transform,false);
            visuals.BuildDiagnostics(panel,out fpsText,out details);panel.localScale=Vector3.one*.6f;panel.gameObject.SetActive(Visible);
        }
        void Update()
        {
            frames++;elapsed+=Time.unscaledDeltaTime;worst=Mathf.Max(worst,Time.unscaledDeltaTime);
            if(elapsed<.5f)return;
            fps=frames/elapsed;milliseconds=1000*elapsed/frames;worstMilliseconds=worst*1000;
            if(fpsText!=null)
            {
                fpsText.text=$"{fps:0} FPS  /  {milliseconds:0.0} ms";
                fpsText.color=fps>=70?new Color(.35f,1,.72f):fps>=60?new Color(1,.8f,.3f):new Color(1,.4f,.3f);
                string tracking=eyes.simulated?"BUTTON DEMO":eyes.Ready?"REAL EYES / READY":eyes.Valid?"CALIBRATING":"NO EYE DATA";
                details.text=$"{tracking}\nL {eyes.Left:0.00}  R {eyes.Right:0.00}  BLINKS {eyes.BlinkSequence}\n{GetComponent<StareGame>()?.InputStatus}";
            }
            if(Debug.isDebugBuild&&Application.isMobilePlatform&&Time.unscaledTime>=nextLog)
            {nextLog=Time.unscaledTime+5;Debug.Log($"[STARE PERF] appFPS={fps:0.0} avgMs={milliseconds:0.0} peakMs={worstMilliseconds:0.0} music={GetComponent<StareMusic>()?.IsPlaying} handL={StareHandAim.TryRead(true,rig.trackingSpace,out _,out _)} handR={StareHandAim.TryRead(false,rig.trackingSpace,out _,out _)} input={GetComponent<StareGame>()?.InputStatus}");}
            frames=0;elapsed=worst=0;
        }
        void LateUpdate()
        {
            if(!Visible||panel==null||rig==null||rig.centerEyeAnchor==null)return;
            var game=GetComponent<StareGame>();
            if(game!=null&&game.SlingshotAvailable){panel.position=rig.centerEyeAnchor.TransformPoint(new Vector3(.36f,-.28f,.75f));panel.rotation=rig.centerEyeAnchor.rotation;return;}
            var anchor=rig.leftControllerAnchor!=null?rig.leftControllerAnchor:rig.leftHandAnchor;
            panel.position=StareHandAim.TryRead(true,rig.trackingSpace,out var handRay,out _)?handRay.origin+Vector3.up*.15f+rig.centerEyeAnchor.right*.08f:anchor.TransformPoint(new Vector3(0,.14f,.035f));
            var away=panel.position-rig.centerEyeAnchor.position;
            if(away.sqrMagnitude>.0001f)panel.rotation=Quaternion.LookRotation(away,Vector3.up);
        }
        void OnDestroy(){if(panel!=null)Destroy(panel.gameObject);}
    }
    public sealed partial class StareVisuals
    {
        public void BuildDiagnostics(Transform parent,out TextMesh fps,out TextMesh details)
        {
            Panel("Diagnostics background",parent,Vector3.zero,new Vector2(.32f,.14f),new Color(.008f,.018f,.03f),new Color(.2f,.8f,.7f),.012f);
            fps=UILabel("MEASURING FPS",parent,new Vector3(-.145f,.04f,-.008f),.018f,Color.white,true);
            details=UILabel("WAITING FOR TRACKING",parent,new Vector3(-.145f,-.018f,-.008f),.0095f,new Color(.7f,.8f,.9f));
        }
    }
}
