#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Staredown;

/// <summary>Run in a disconnected initialized mobile Play scene, without a running camera.</summary>
public static class StareMobileAvatarPreviewChecks
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Tick(object target,string method="Update")
    {try{target.GetType().GetMethod(method,Private).Invoke(target,null);}catch(TargetInvocationException error){throw error.InnerException??error;}}
    static Rect In(RectTransform area,RectTransform child)
    {
        var corners=new Vector3[4];child.GetWorldCorners(corners);Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
        foreach(var corner in corners){Vector2 p=area.InverseTransformPoint(corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    static void Tap(Button button,GraphicRaycaster raycaster,Camera camera)
    {
        Canvas.ForceUpdateCanvases();camera.Render();
        var rect=button.GetComponent<RectTransform>();var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);
        if(hits.Count==0||hits[0].gameObject!=button.gameObject)
            throw new Exception("Character picker touch blocked: "+button.name+" by "+(hits.Count>0?hits[0].gameObject.name:"no hit"));
        data.pointerCurrentRaycast=data.pointerPressRaycast=hits[0];ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static void Capture(Camera camera,RenderTexture target,string path)
    {
        var active=RenderTexture.active;Texture2D image=null;
        var resolved=new RenderTexture(target.width,target.height,0,RenderTextureFormat.ARGB32){antiAliasing=1};
        try
        {
            resolved.Create();Canvas.ForceUpdateCanvases();camera.Render();target.ResolveAntiAliasedSurface(resolved);
            RenderTexture.active=resolved;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally {RenderTexture.active=active;if(image!=null)UnityEngine.Object.DestroyImmediate(image);resolved.Release();UnityEngine.Object.DestroyImmediate(resolved);}
    }
    static void VerifyModel(StareMobileAvatarPreview studio,int id,string outputFolder,Action<bool,string> check)
    {
        var active=RenderTexture.active;Texture2D image=null;
        var resolved=new RenderTexture(studio.Texture.width,studio.Texture.height,0,RenderTextureFormat.ARGB32){antiAliasing=1};
        try
        {
            resolved.Create();studio.Texture.ResolveAntiAliasedSurface(resolved);RenderTexture.active=resolved;
            image=new Texture2D(resolved.width,resolved.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,resolved.width,resolved.height),0,0);image.Apply();
            var pixels=image.GetPixels32();Color32 background=pixels[0];int foreground=0,minX=image.width,minY=image.height,maxX=0,maxY=0;
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
            {
                var p=pixels[y*image.width+x];if(Mathf.Abs(p.r-background.r)+Mathf.Abs(p.g-background.g)+Mathf.Abs(p.b-background.b)<45)continue;
                foreground++;minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
            check(foreground>pixels.Length/100,"model renders visible foreground pixels for "+id+": "+foreground);
            check(maxY-minY>image.height*.45f&&maxX-minX>image.width*.12f,"model has useful on-screen size for "+id);
            check(minY>2&&maxY<image.height-3,"head and feet remain within frame for "+id);
            File.WriteAllBytes(Path.Combine(outputFolder,"model-"+StareAvatarCatalog.Assets[id]+".png"),image.EncodeToPNG());
        }
        finally{RenderTexture.active=active;if(image!=null)UnityEngine.Object.DestroyImmediate(image);resolved.Release();UnityEngine.Object.DestroyImmediate(resolved);}
    }
    public static string Run(string outputFolder=null)
    {
        var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();var hud=session!=null?session.GetComponent<StareMobileHud>():null;
        if(!Application.isPlaying||hud==null||session.Connected||session.Busy||session.MobileCamera.Running)
            throw new Exception("Run avatar checks in a disconnected phone Play scene with camera off");
        bool hadAvatar=PlayerPrefs.HasKey("Staredown.MobileAvatar"),hadName=PlayerPrefs.HasKey("Staredown.PlayerName"),hadConfirmed=PlayerPrefs.HasKey("Staredown.PlayerNameConfirmed");
        int original=PlayerPrefs.GetInt("Staredown.MobileAvatar"),confirmed=PlayerPrefs.GetInt("Staredown.PlayerNameConfirmed");string name=PlayerPrefs.GetString("Staredown.PlayerName");
        string draft=hud.PlayerNameInput.text;bool settings=hud.SettingsVisible;
        var canvas=hud.GetComponentInChildren<Canvas>();var raycaster=canvas.GetComponent<GraphicRaycaster>();var camera=session.ViewCamera;
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance;
        var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;var oldRect=camera.rect;RenderTexture target=null;
        outputFolder=outputFolder??Path.Combine(Path.GetTempPath(),"Staredown-avatar-preview");Directory.CreateDirectory(outputFolder);
        Action restoreViewport=()=>{camera.targetTexture=oldTarget;camera.aspect=oldAspect;camera.rect=oldRect;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;hud.ClearReviewViewport();};
        int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mobile character: "+label);count++;};
        try
        {
            PlayerPrefs.SetInt("Staredown.MobileAvatar",StareAvatarCatalog.Meta);check(session.SelectedAvatar==2,"invalid mobile Meta choice falls back to preset");
            session.SelectAvatar(0);hud.ShowSettings(false);hud.ShowNameEditor(false);
            var sizes=new[]{new Vector2(900,1600),new Vector2(1600,900),new Vector2(768,1024)};
            foreach(var size in sizes)
            {
                target=new RenderTexture((int)size.x,(int)size.y,24,RenderTextureFormat.ARGB32){antiAliasing=2,name="Phone character selection fixture"};target.Create();
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=size.x/size.y;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.1f;
                var inset=new Rect(0,0,size.x,size.y);
                // Override the real canvas viewport too: a portrait layout on
                // the editor's landscape overlay is not a phone-sized fixture.
                hud.SetReviewViewport(size,inset);Canvas.ForceUpdateCanvases();camera.Render();
                hud.SetReviewViewport(size,inset);hud.ShowAvatarPicker(true);Canvas.ForceUpdateCanvases();Tick(hud.AvatarStudio,"LateUpdate");camera.Render();
                var safe=hud.SafeAreaRect;var panel=In(safe,hud.AvatarPanelRect);
                check(safe.rect.Contains(panel.min+Vector2.one)&&safe.rect.Contains(panel.max-Vector2.one),"modal contained at "+size);
                foreach(var button in new[]{hud.AvatarPreviousButton,hud.AvatarNextButton,hud.AvatarUseButton,hud.AvatarCancelButton})
                {
                    var b=In(hud.AvatarPanelRect,button.GetComponent<RectTransform>());
                    check(hud.AvatarPanelRect.rect.Contains(b.min+Vector2.one)&&hud.AvatarPanelRect.rect.Contains(b.max-Vector2.one),button.name+" contained at "+size);
                    check(b.width>=44&&b.height>=44,button.name+" has a usable touch target");
                    check(!b.Overlaps(In(hud.AvatarPanelRect,hud.AvatarPreview.transform.parent as RectTransform)),button.name+" separate from model");
                }
                int before=hud.PreviewAvatar;Tap(hud.AvatarNextButton,raycaster,camera);check(hud.PreviewAvatar!=before,"next receives an actual touch at "+size);
                Tap(hud.AvatarPreviousButton,raycaster,camera);check(hud.PreviewAvatar==before,"previous receives an actual touch at "+size);
                Tick(hud.AvatarStudio,"LateUpdate");
                Capture(camera,target,Path.Combine(outputFolder,"avatar-"+(size.x>size.y?"landscape":size.x==768?"tablet":"portrait")+".png"));
                Tap(hud.AvatarCancelButton,raycaster,camera);check(!hud.AvatarPickerVisible,"cancel receives an actual touch at "+size);
                camera.targetTexture=oldTarget;target.Release();UnityEngine.Object.DestroyImmediate(target);target=null;
            }
            restoreViewport();hud.ShowAvatarPicker(true);
            var studio=hud.AvatarStudio;var seen=new HashSet<int>();
            for(int slot=0;slot<StareAvatarCatalog.PresetCount;slot++)
            {
                int id=hud.PreviewAvatar;seen.Add(id);check(studio.Visible&&studio.AvatarId==id,"preview follows choice "+id);
                check(studio.AvatarRoot!=null&&studio.AvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0,"actual rigged model for "+id);
                check(!studio.CaptureCamera.enabled&&studio.CaptureCamera.stereoTargetEye==StereoTargetEyeMask.None,"manual ordinary preview camera");
                check(studio.Texture.width==512&&studio.Texture.height==512,"preview has bounded render resolution");
                check(session.SelectedAvatar==0,"browsing does not commit "+id);
                Tick(studio,"LateUpdate");check(studio.RenderCount>slot,"visible choice renders");
                VerifyModel(studio,id,outputFolder,check);
                hud.CycleAvatar(1);
            }
            check(seen.SetEquals(StareAvatarCatalog.PresetIds),"all eight actual models selectable");check(hud.PreviewAvatar==0,"next wraps");
            hud.CycleAvatar(-1);check(hud.PreviewAvatar==8,"previous wraps");hud.AvatarCancelButton.onClick.Invoke();
            int frames=studio.RenderCount;Tick(studio,"LateUpdate");
            check(!studio.Visible&&!studio.AvatarRoot.gameObject.activeInHierarchy&&studio.RenderCount==frames,"closing stops animation and rendering");
            check(session.SelectedAvatar==0,"cancel preserves previous choice");
            hud.ShowAvatarPicker(true);hud.CycleAvatar(2);hud.AvatarUseButton.onClick.Invoke();
            check(session.SelectedAvatar==2&&!hud.AvatarPickerVisible,"use saves and closes");
            StarePlayerName.Save("Avatar Check");session.Practice();Tick(session);Tick(hud);
            check(session.State.fighters.First(f=>f.id==session.LocalId).avatar==2,"saved selection enters practice");
            hud.ShowSettings(true);hud.SettingsAvatarButton.onClick.Invoke();check(hud.AvatarPickerVisible&&!hud.SettingsVisible,"lobby settings opens picker");
            hud.CycleAvatar(1);hud.AvatarUseButton.onClick.Invoke();Tick(session);
            check(session.State.fighters.First(f=>f.id==session.LocalId).avatar==3,"lobby selection reaches authoritative roster");
            session.State.phase=(int)MatchPhase.Duel;hud.ShowAvatarPicker(true);
            check(!hud.AvatarPickerVisible&&!session.CanSelectAvatar,"active competition prevents character changes");
            session.Leave();
            return count+" mobile avatar preview, choice, layout and lifecycle checks passed";
        }
        finally
        {
            hud.ShowAvatarPicker(false);session.Leave();restoreViewport();if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}hud.ShowSettings(settings);hud.PlayerNameInput.SetTextWithoutNotify(draft);
            if(hadAvatar)PlayerPrefs.SetInt("Staredown.MobileAvatar",original);else PlayerPrefs.DeleteKey("Staredown.MobileAvatar");
            if(hadName)PlayerPrefs.SetString("Staredown.PlayerName",name);else PlayerPrefs.DeleteKey("Staredown.PlayerName");
            if(hadConfirmed)PlayerPrefs.SetInt("Staredown.PlayerNameConfirmed",confirmed);else PlayerPrefs.DeleteKey("Staredown.PlayerNameConfirmed");PlayerPrefs.Save();Tick(hud);
        }
    }
}
#endif
