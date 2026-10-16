using System.Collections.Generic;
using UnityEngine;

namespace Staredown
{
    public sealed partial class StareVisuals
    {
        Camera eyeCapture;
        readonly RenderTexture[] eyeFeeds=new RenderTexture[2];
        float nextEyeCapture; int nextEyeFeed; readonly int[] capturedIds={-1,-1};

        // Render the selected character's actual eyes so every preset appears
        // correctly on the finals screen. Only the chosen avatar is captured.
        void CaptureSelectedEyes(Fighter left,Fighter right)
        {
            if(!referenceBuilt||referenceEyeWall==null||!referenceEyeWall.gameObject.activeInHierarchy||left==null||right==null)
            {eyeMural.SetFloat("_LiveFeedReady",0);return;}
            if(Time.unscaledTime<nextEyeCapture)return;
            nextEyeCapture=Time.unscaledTime+(PhoneView?.083333f:Application.isMobilePlatform?.033f:.066f);
            if(eyeCapture==null)
            {
                eyeCapture=Group("Finalist eye camera",transform,Vector3.zero).gameObject.AddComponent<Camera>();
                eyeCapture.enabled=false;eyeCapture.stereoTargetEye=StereoTargetEyeMask.None;
                eyeCapture.nearClipPlane=.015f;eyeCapture.farClipPlane=3;eyeCapture.fieldOfView=24;eyeCapture.aspect=2;
                eyeCapture.clearFlags=CameraClearFlags.SolidColor;eyeCapture.backgroundColor=new Color(.012f,.022f,.035f);
                eyeCapture.cullingMask=1<<29;eyeCapture.allowHDR=false;eyeCapture.allowMSAA=!PhoneView;
                // Preserve the new facial detail on the large finals screen.
                // The feeds still update at 15 Hz, independently of headset frames.
                for(int i=0;i<2;i++){eyeFeeds[i]=new RenderTexture(PhoneView?512:1024,PhoneView?256:512,16){name="Finalist "+i+" eye feed",antiAliasing=PhoneView?1:2,filterMode=FilterMode.Trilinear,useMipMap=true,autoGenerateMips=true,anisoLevel=PhoneView?2:4};eyeFeeds[i].Create();}
                eyeMural.SetTexture("_LiveLeftTex",eyeFeeds[0]);eyeMural.SetTexture("_LiveRightTex",eyeFeeds[1]);
            }
            if(Application.isMobilePlatform||PhoneView)
            {
                // Spread the two feeds across frames; each still updates at 15 Hz.
                int index=nextEyeFeed;nextEyeFeed=1-nextEyeFeed;
                int id=index==0?left.id:right.id;
                capturedIds[index]=CaptureEyesFor(id,index)?id:-1;
            }
            else
            {capturedIds[0]=CaptureEyesFor(left.id,0)?left.id:-1;capturedIds[1]=CaptureEyesFor(right.id,1)?right.id:-1;}
            eyeMural.SetFloat("_LiveFeedReady",capturedIds[0]==left.id&&capturedIds[1]==right.id?1:0);
        }

        bool CaptureEyesFor(int id,int index)
        {
            if(!avatars.TryGetValue(id,out var avatar))return false;
            bool cameraHead=avatar.cameraHead!=null&&avatar.cameraHead.Live;
            eyeCapture.fieldOfView=cameraHead?49:24;
            Vector3 focus;
            if(cameraHead)focus=avatar.cameraHead.PortraitFocus;
            else if(avatar.meta!=null&&avatar.meta.Loaded)
            {if(!avatar.meta.TryGetPortraitFocus(out focus))return false;}
            else if(avatar.eyes.left!=null&&avatar.eyes.right!=null)
                focus=(avatar.eyes.left.position+avatar.eyes.right.position)*.5f;
            else return false;
            bool active=avatar.root.gameObject.activeSelf;bool firstPerson=avatar.meta!=null&&avatar.meta.IsFirstPerson;
            var detail=avatar.preset.GetComponent<LODGroup>();
            var objects=new List<GameObject>();var layers=new List<int>();var seen=new HashSet<GameObject>();
            SetLocalMetaCapture(true);
            try
            {
                avatar.root.gameObject.SetActive(true);if(firstPerson)avatar.meta.SetLocalView(false);
                if(detail!=null)detail.ForceLOD(0);
                foreach(var renderer in avatar.root.GetComponentsInChildren<Renderer>(true))
                {if(!seen.Add(renderer.gameObject))continue;objects.Add(renderer.gameObject);layers.Add(renderer.gameObject.layer);renderer.gameObject.layer=29;}
                eyeCapture.transform.position=focus-avatar.root.forward*.62f;
                eyeCapture.transform.LookAt(focus,avatar.root.up);eyeCapture.targetTexture=eyeFeeds[index];eyeCapture.Render();
                return true;
            }
            finally
            {
                for(int i=0;i<objects.Count;i++)if(objects[i]!=null)objects[i].layer=layers[i];
                if(detail!=null)detail.ForceLOD(-1);
                avatar.root.gameObject.SetActive(active);if(firstPerson)avatar.meta.SetLocalView(true);
                SetLocalMetaCapture(false);
            }
        }

        void DisposeEyeFeeds()
        {
            foreach(var feed in eyeFeeds)if(feed!=null){feed.Release();Destroy(feed);}
        }
    }
}
