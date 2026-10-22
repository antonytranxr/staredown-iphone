using UnityEngine;
using UnityEngine.UI;

namespace Staredown
{
    /// <summary>A single, isolated avatar studio, rendered only while its picker is open.</summary>
    public sealed class StareMobileAvatarPreview : MonoBehaviour
    {
        public const int TextureSize=512,PreviewLayer=29;
        public const float FramesPerSecond=20;
        StareVisuals visuals;RawImage image;Transform stage;Camera capture;RenderTexture texture;
        CreatureView avatar;Animator animator;float nextFrame;bool visible;
        public bool Visible=>visible;
        public int AvatarId {get;private set;}=-1;
        public int RenderCount {get;private set;}
        public RenderTexture Texture=>texture;
        public Camera CaptureCamera=>capture;
        public Transform AvatarRoot=>avatar!=null?avatar.root:null;

        public void Initialize(StareVisuals source,RawImage target)
        {visuals=source;image=target;}

        public void Show(int kind)
        {
            if(visuals==null||image==null||!StareAvatarCatalog.IsPreset(kind))return;
            EnsureStudio();visible=true;stage.gameObject.SetActive(true);
            if(avatar==null||AvatarId!=kind)
            {
                if(avatar!=null){avatar.root.gameObject.SetActive(false);Destroy(avatar.root.gameObject);}
                avatar=visuals.CreateAvatar(kind,null,stage);AvatarId=kind;
                avatar.root.localPosition=Vector3.zero;avatar.root.localRotation=Quaternion.Euler(0,-12,0);
                avatar.label.gameObject.SetActive(false);
                if(avatar.presence!=null)avatar.presence.enabled=false;
                foreach(var collider in avatar.root.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var body in avatar.root.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
                StareFloorReflection.Mark(avatar.root,PreviewLayer);
                var detail=avatar.preset.GetComponent<LODGroup>();if(detail!=null)detail.ForceLOD(0);
                animator=avatar.preset.GetComponent<Animator>();
                if(animator!=null){animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Play("Idle",0,0);animator.Update(0);}
                foreach(var skin in avatar.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                avatar.eyes.Set(0,0,0,0);FrameAvatar();
            }
            image.texture=texture;image.enabled=true;nextFrame=0;
        }

        public void Hide()
        {
            visible=false;
            if(stage!=null)stage.gameObject.SetActive(false);
            // Retain the small target for a quick reopen; no camera, lights,
            // animator or render work remains active while the picker is hidden.
        }

        void EnsureStudio()
        {
            if(stage!=null)return;
            stage=new GameObject("Mobile character preview studio").transform;stage.SetParent(transform,false);
            stage.position=new Vector3(0,-1000,0);
            texture=new RenderTexture(TextureSize,TextureSize,24,RenderTextureFormat.ARGB32)
            {name="Mobile character preview",antiAliasing=2,useMipMap=false,autoGenerateMips=false,filterMode=FilterMode.Bilinear};
            texture.antiAliasing=Mathf.Clamp(SystemInfo.GetRenderTextureSupportedMSAASampleCount(texture.descriptor),1,2);texture.Create();
            capture=new GameObject("Character preview camera").AddComponent<Camera>();capture.transform.SetParent(stage,false);
            capture.enabled=false;capture.stereoTargetEye=StereoTargetEyeMask.None;capture.targetTexture=texture;
            capture.clearFlags=CameraClearFlags.SolidColor;capture.backgroundColor=new Color(.035f,.065f,.095f,1);
            capture.cullingMask=1<<PreviewLayer;capture.orthographic=true;capture.aspect=1;capture.nearClipPlane=.05f;capture.farClipPlane=16;
            capture.allowHDR=false;capture.allowMSAA=true;capture.useOcclusionCulling=false;capture.renderingPath=RenderingPath.Forward;
            AddLight("Soft key",new Vector3(-2,3,-3),1.26f,new Color(.83f,.94f,1));
            AddLight("Warm fill",new Vector3(2,2,-2),.9f,new Color(1,.83f,.67f));
            AddLight("Blue rim",new Vector3(0,3,2),1.17f,new Color(.3f,.73f,1));
        }
        void AddLight(string name,Vector3 point,float intensity,Color color)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(stage,false);light.transform.localPosition=point;
            light.type=LightType.Point;light.range=9;light.intensity=intensity;light.color=color;light.shadows=LightShadows.None;
            light.cullingMask=1<<PreviewLayer;
        }
        void FrameAvatar()
        {
            bool any=false;Bounds bounds=default;var mesh=new Mesh();
            try
            {
                // Runtime skin bounds include a large dancing envelope. Fit the
                // actual posed vertices so hats, feet and hands stay in frame.
                // Include the renderer scale in BakeMesh so TransformPoint
                // applies imported FBX scale exactly once, not a second time.
                foreach(var renderer in avatar.preset.GetComponentsInChildren<Renderer>(true))
                {
                    bool lod=false;for(var node=renderer.transform;node!=null&&node!=avatar.preset;node=node.parent)
                        if(node.name.Contains(" / body LOD ")){lod=true;break;}
                    if(lod||renderer.GetComponent<TextMesh>()!=null)continue;
                    Bounds local;var skin=renderer as SkinnedMeshRenderer;
                    if(skin!=null){skin.BakeMesh(mesh,true);local=mesh.bounds;}
                    else {var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;local=filter.sharedMesh.bounds;}
                    for(int i=0;i<8;i++)
                    {
                        var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        var point=renderer.transform.TransformPoint(corner);
                        if(!any){bounds=new Bounds(point,Vector3.zero);any=true;}else bounds.Encapsulate(point);
                    }
                }
            }
            finally {Destroy(mesh);}
            if(!any)bounds=new Bounds(stage.position+Vector3.up*1.1f,new Vector3(1.6f,2.2f,1));
            capture.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.13f;
            capture.transform.position=bounds.center+Vector3.back*6;
            capture.transform.rotation=Quaternion.identity;
        }
        void LateUpdate()
        {
            if(!visible||capture==null||image==null||!image.gameObject.activeInHierarchy||Time.unscaledTime<nextFrame)return;
            nextFrame=Time.unscaledTime+1/FramesPerSecond;
            if(avatar!=null){float blink=Mathf.Repeat(Time.unscaledTime,5.5f)>5.37f?1:0;avatar.eyes.Set(blink,blink,0,0);}
            capture.Render();RenderCount++;
        }
        void OnDisable(){Hide();}
        void OnDestroy()
        {
            if(capture!=null)capture.targetTexture=null;
            if(image!=null)image.texture=null;
            if(texture!=null){texture.Release();Destroy(texture);}
            if(stage!=null)Destroy(stage.gameObject);
        }
    }
}
