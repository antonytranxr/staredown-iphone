#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Staredown;

/// <summary>Runs synchronously in an initialized, disconnected phone Play scene.</summary>
public static class StareMobileUiChecks
{
    sealed class Preferences : IDisposable
    {
        readonly string[] keys={"Staredown.PlayerName","Staredown.PlayerNameConfirmed","Staredown.MobileMusicEnabled","Staredown.MobileMusicVolume"};
        readonly bool[] present=new bool[4];
        readonly string name;
        readonly int confirmed,music;
        readonly float volume;
        public Preferences()
        {
            for(int i=0;i<keys.Length;i++)present[i]=PlayerPrefs.HasKey(keys[i]);
            name=PlayerPrefs.GetString(keys[0],"");confirmed=PlayerPrefs.GetInt(keys[1]);
            music=PlayerPrefs.GetInt(keys[2]);volume=PlayerPrefs.GetFloat(keys[3]);
        }
        public void Dispose()
        {
            if(present[0])PlayerPrefs.SetString(keys[0],name);else PlayerPrefs.DeleteKey(keys[0]);
            if(present[1])PlayerPrefs.SetInt(keys[1],confirmed);else PlayerPrefs.DeleteKey(keys[1]);
            if(present[2])PlayerPrefs.SetInt(keys[2],music);else PlayerPrefs.DeleteKey(keys[2]);
            if(present[3])PlayerPrefs.SetFloat(keys[3],volume);else PlayerPrefs.DeleteKey(keys[3]);
            PlayerPrefs.Save();
        }
    }

    static void Tick(object component)
    {
        var method=component.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        if(method==null)throw new Exception("UI fixture cannot refresh "+component.GetType().Name);
        try{method.Invoke(component,null);}
        catch(TargetInvocationException error){throw error.InnerException??error;}
    }

    static Rect ScreenRect(RectTransform rect,Camera camera)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        Vector2 low=RectTransformUtility.WorldToScreenPoint(camera,corners[0]),high=low;
        foreach(var corner in corners)
        {
            Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,corner);
            low=Vector2.Min(low,point);high=Vector2.Max(high,point);
        }
        return Rect.MinMaxRect(low.x,low.y,high.x,high.y);
    }

    static void Inside(RectTransform rect,Rect safe,Camera camera,Action<bool,string> check,string label)
    {
        var actual=ScreenRect(rect,camera);const float slack=3;
        check(actual.width>3&&actual.height>3&&actual.xMin>=safe.xMin-slack&&actual.yMin>=safe.yMin-slack&&actual.xMax<=safe.xMax+slack&&actual.yMax<=safe.yMax+slack,
            label+" stays in safe area: "+actual+" vs "+safe);
    }

    static PointerEventData Hit(GraphicRaycaster raycaster,Vector2 point,GameObject expected)
    {
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left,pointerId=-77};
        var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);
        if(hits.Count==0)throw new Exception("UI fixture: no graphic raycast hit on "+expected.name+" at "+point);
        var hit=hits[0];
        if(hit.gameObject!=expected&&!hit.gameObject.transform.IsChildOf(expected.transform))
            throw new Exception("UI fixture: "+expected.name+" blocked by "+hit.gameObject.name+" at "+point);
        data.pointerCurrentRaycast=data.pointerPressRaycast=hit;return data;
    }

    static void Tap(Button button,GraphicRaycaster raycaster,Camera camera)
    {
        if(!button.gameObject.activeInHierarchy)throw new Exception("UI fixture: hidden button "+button.name);
        var rect=button.GetComponent<RectTransform>();
        var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var data=Hit(raycaster,point,button.gameObject);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        Canvas.ForceUpdateCanvases();
        // Newly enabled graphics have depth=-1 until the canvas is rendered.
        // Simulate the rendered frame a human sees before the next touch.
        camera.Render();
    }

    static void Slide(Slider slider,float fraction,GraphicRaycaster raycaster,Camera camera)
    {
        var area=slider.handleRect.parent as RectTransform;
        var local=new Vector3(Mathf.Lerp(area.rect.xMin,area.rect.xMax,fraction),area.rect.center.y,0);
        var point=RectTransformUtility.WorldToScreenPoint(camera,area.TransformPoint(local));
        var data=Hit(raycaster,point,slider.gameObject);
        ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerUpHandler);
    }

    static Texture2D Capture(Camera camera,RenderTexture target,string path)
    {
        var old=RenderTexture.active;
        var resolved=new RenderTexture(target.width,target.height,0,RenderTextureFormat.ARGB32){antiAliasing=1,name="Phone UI resolved fixture"};
        Texture2D image=null;
        try
        {
            if(!resolved.Create())throw new Exception("UI fixture resolve texture creation failed");
            Canvas.ForceUpdateCanvases();camera.Render();target.ResolveAntiAliasedSurface(resolved);
            RenderTexture.active=resolved;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false,false);
            File.WriteAllBytes(path,image.EncodeToPNG());return image;
        }
        catch{if(image!=null)UnityEngine.Object.DestroyImmediate(image);throw;}
        finally{RenderTexture.active=old;resolved.Release();UnityEngine.Object.DestroyImmediate(resolved);}
    }

    static Color32[] PreviewPixels()
    {
        const int side=StareMobileCamera.FrameSize;var pixels=new Color32[side*side];
        for(int y=0;y<side;y++)for(int x=0;x<side;x++)
        {
            Color32 color=x<side/2?new Color32(22,166,125,255):new Color32(48,80,212,255);
            float dx=x-80,dy=y-80;
            if(dx*dx+dy*dy<54*54)color=new Color32((byte)(226+x/12),(byte)(169+y/12),125,255);
            if((x-62)*(x-62)+(y-95)*(y-95)<6*6||(x-98)*(x-98)+(y-95)*(y-95)<6*6)color=new Color32(22,30,45,255);
            if(y>52&&y<58&&x>63&&x<97)color=new Color32(130,51,54,255);
            pixels[y*side+x]=color;
        }
        return pixels;
    }

    public static string Run(string outputFolder)
    {
        if(!Application.isPlaying)throw new Exception("Run UI checks in the initialized phone Play scene");
        var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();
        var hud=session!=null?session.GetComponent<StareMobileHud>():null;
        if(session==null||hud==null||session.ViewCamera==null||session.MobileCamera==null)throw new Exception("Initialized phone scene and HUD required");
        if(session.Connected||session.Busy||session.MobileCamera.Running)throw new Exception("Leave any room and stop the camera before running UI fixtures");
        if(EventSystem.current==null)throw new Exception("Phone EventSystem required");
        Directory.CreateDirectory(outputFolder);
        var canvas=hud.GetComponentInChildren<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();var raycaster=canvas.GetComponent<GraphicRaycaster>();
        var camera=session.ViewCamera;var face=session.MobileCamera;
        var oldRenderMode=canvas.renderMode;var oldWorldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance,oldCanvasScale=canvas.scaleFactor;
        var oldScaleMode=scaler.uiScaleMode;var oldReference=scaler.referenceResolution;var oldMatchMode=scaler.screenMatchMode;float oldMatch=scaler.matchWidthOrHeight,oldScalerScale=scaler.scaleFactor;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var oldRect=camera.rect;float oldAspect=camera.aspect;
        Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
        bool oldLandscape=session.LandscapeMode,oldSettings=hud.SettingsVisible,oldNameModal=hud.NameEditorVisible;
        string oldDraft=hud.PlayerNameInput.text,oldCode=hud.RoomCodeInput.text,oldNameReview=hud.NameReviewInput.text,oldStatus=session.Status;
        var oldSelected=EventSystem.current.currentSelectedGameObject;
        int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Phone UI: "+label);count++;};
        var preferences=new Preferences();RenderTexture target=null;bool captureStarted=false;
        try
        {
            check(camera.actualRenderingPath==RenderingPath.Forward&&camera.allowMSAA&&!camera.allowHDR,"main phone camera uses forward rendering, MSAA and no HDR");
            StarePlayerName.Save("Existing Pilot");
            face.BeginTestCapture();captureStarted=true;face.SubmitTestPixels(PreviewPixels(),StareMobileCamera.FrameSize,StareMobileCamera.FrameSize,0,false);
            var canonical=face.Preview as Texture2D;
            check(canonical!=null&&canonical.GetPixel(12,80).g>canonical.GetPixel(12,80).b&&canonical.GetPixel(148,80).b>canonical.GetPixel(148,80).g,"camera fixture pixels are canonical and unmirrored");
            check(face.CameraEnabledIntent,"camera opt-in is retained by provider fixture");
            var sizes=new[]{new Vector2Int(900,1600),new Vector2Int(1600,900)};
            var insets=new[]{new Rect(0,50,900,1460),new Rect(65,32,1470,868)};
            for(int index=0;index<sizes.Length;index++)
            {
                var size=sizes[index];var safe=insets[index];string prefix=index==0?"portrait":"landscape";
                session.Leave();StarePlayerName.Save("Existing Pilot");session.SetLandscapeMode(index!=0);hud.ShowSettings(false);hud.ShowNameEditor(false);
                hud.PlayerNameInput.SetTextWithoutNotify("Existing Pilot");hud.RoomCodeInput.SetTextWithoutNotify("1234");
                target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32){antiAliasing=2,name="Phone "+prefix+" UI fixture"};
                int supported=SystemInfo.GetRenderTextureSupportedMSAASampleCount(target.descriptor);
                check(supported>=2,"graphics device supports the required 2x UI capture");
                if(!target.Create())throw new Exception("Phone UI capture RT creation failed");
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=size.x/(float)size.y;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.1f;
                // The override must follow the camera/RT assignment. It owns
                // the 600-point short edge; never force canvas scale back to 1.
                hud.SetReviewViewport(new Vector2(size.x,size.y),safe);Tick(hud);Canvas.ForceUpdateCanvases();camera.Render();
                // Native canvas dimensions follow the RT on rendering. Apply
                // safe-area layout again after a synchronous orientation swap.
                hud.SetReviewViewport(new Vector2(size.x,size.y),safe);Tick(hud);Canvas.ForceUpdateCanvases();
                check(target.antiAliasing==2&&target.depth>=24,"capture keeps 2x MSAA and at least24bit depth (Metal may select32)");
                check(hud.IsPortraitLayout==(index==0)&&hud.HomeVisible,"home selects the requested orientation");
                var actualSafe=ScreenRect(hud.SafeAreaRect,camera);
                check(Mathf.Abs(actualSafe.x-safe.x)<3&&Mathf.Abs(actualSafe.y-safe.y)<3&&Mathf.Abs(actualSafe.width-safe.width)<3&&Mathf.Abs(actualSafe.height-safe.height)<3,"safe area matches the capture viewport: "+actualSafe+" vs "+safe);
                Inside(hud.SettingsButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" Settings button");
                Inside(hud.HomePanelRect,safe,camera,check,prefix+" home panel");
                Inside(hud.PlayerNameInput.GetComponent<RectTransform>(),safe,camera,check,prefix+" required name field");
                Inside(hud.PracticeButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" Practice button");
                Inside(hud.HostButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" Create button");
                Inside(hud.JoinButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" Join button");
                Inside(hud.CameraPanelRect,safe,camera,check,prefix+" camera panel");
                check(!ScreenRect(hud.PlayerNameInput.GetComponent<RectTransform>(),camera).Overlaps(ScreenRect(hud.CameraPanelRect,camera)),"name entry does not overlap camera card");
                check(hud.CameraPreview.uvRect==new Rect(1,0,-1,1)&&hud.CameraPreview.rectTransform.localScale==Vector3.one&&Quaternion.Angle(hud.CameraPreview.rectTransform.localRotation,Quaternion.identity)<.01f,"local preview applies exactly one horizontal UV mirror without transform rotation/flip");
                check(hud.CameraSetupButton.GetComponentInChildren<Text>().text=="Camera settings","enabled home camera is not another enable/off step");
                bool runningBeforeSettings=face.Running;Tap(hud.CameraSetupButton,raycaster,camera);Tick(hud);
                check(hud.SettingsVisible&&face.Running==runningBeforeSettings&&face.CameraEnabledIntent,"home camera button opens settings without toggling capture");
                Tap(hud.SettingsButton,raycaster,camera);Tick(hud);
                var home=Capture(camera,target,Path.Combine(outputFolder,prefix+"-home.png"));
                try
                {
                    var imageRect=ScreenRect(hud.CameraPreviewRect,camera);
                    Color left=home.GetPixel(Mathf.RoundToInt(imageRect.x+imageRect.width*.08f),Mathf.RoundToInt(imageRect.center.y));
                    Color right=home.GetPixel(Mathf.RoundToInt(imageRect.x+imageRect.width*.92f),Mathf.RoundToInt(imageRect.center.y));
                    check(left.b>left.g&&right.g>right.b,"rendered preview is mirrored once from canonical pixels");
                }
                finally{UnityEngine.Object.DestroyImmediate(home);}

                Tap(hud.SettingsButton,raycaster,camera);Tick(hud);Canvas.ForceUpdateCanvases();check(hud.SettingsVisible,"Settings opens through real graphic raycast");
                Inside(hud.SettingsPanelRect,safe,camera,check,prefix+" Settings panel");
                Inside(hud.MusicToggleButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" music mute button");
                Inside(hud.MusicVolumeSlider.GetComponent<RectTransform>(),safe,camera,check,prefix+" music slider");
                Inside(hud.PortraitButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" portrait button");
                Inside(hud.LandscapeButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" landscape button");
                Inside(hud.CalibrateButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" calibration button");
                bool musicBefore=session.MusicEnabled;Tap(hud.MusicToggleButton,raycaster,camera);Tick(hud);
                check(session.MusicEnabled!=musicBefore&&PlayerPrefs.GetInt("Staredown.MobileMusicEnabled",-1)==(session.MusicEnabled?1:0),"music toggle changes and persists the preference");
                var music=session.GetComponents<AudioSource>().FirstOrDefault(source=>source.loop&&source.clip==Resources.Load<AudioClip>("Audio/ArenaPulse"));
                check(music!=null&&music.mute==!session.MusicEnabled,"music toggle reaches the playing arena AudioSource");
                float desired=session.MusicVolume<.5f?.78f:.24f;Slide(hud.MusicVolumeSlider,desired,raycaster,camera);Tick(hud);
                check(Mathf.Abs(session.MusicVolume-desired)<.035f&&Mathf.Abs(music.volume-session.MusicVolume)<.005f&&Mathf.Abs(PlayerPrefs.GetFloat("Staredown.MobileMusicVolume",-1)-session.MusicVolume)<.005f,"touching slider updates audible and saved volume");
                var settings=Capture(camera,target,Path.Combine(outputFolder,prefix+"-settings.png"));UnityEngine.Object.DestroyImmediate(settings);
                Tap(hud.SettingsButton,raycaster,camera);Tick(hud);check(!hud.SettingsVisible,"Settings can close from its always-visible button");

                hud.PlayerNameInput.SetTextWithoutNotify("---");Tick(hud);Canvas.ForceUpdateCanvases();
                check(session.HasPlayerName&&!hud.ValidNameDraft&&!hud.PracticeButton.interactable&&!hud.HostButton.interactable&&!hud.JoinButton.interactable,"invalid draft cannot reuse a previously saved valid name");
                Tap(hud.PracticeButton,raycaster,camera);check(!session.Connected&&!session.Busy,"disabled Practice does not enter a room");
                foreach(string action in new[]{"practice","host","join"})hud.RequestEntryForReview(action);
                check(!session.Connected&&!session.Busy&&session.PlayerName=="Existing Pilot","entry handler independently rejects invalid name for all three actions");
                hud.PlayerNameInput.DeactivateInputField();hud.PlayerNameInput.SetTextWithoutNotify("UI Pilot");Tick(hud);Canvas.ForceUpdateCanvases();
                check(hud.ValidNameDraft&&hud.PracticeButton.interactable&&hud.HostButton.interactable&&hud.JoinButton.interactable,"a valid name and four-digit code enable entry choices");
                Tap(hud.PracticeButton,raycaster,camera);Tick(session);Tick(hud);Canvas.ForceUpdateCanvases();
                check(session.Connected&&session.Hosting&&session.HasPlayerName&&session.PlayerName=="UI Pilot"&&!hud.HomeVisible&&!hud.NameEditorVisible,"Practice saves visible name and enters directly without duplicate confirmation");
                check(session.State.fighters.Any(f=>f.id==session.LocalId&&f.name=="UI Pilot")&&session.State.phase==(int)MatchPhase.Lobby,"saved name reaches the practice lobby roster");
                check(face.Running&&face.CameraEnabledIntent&&ReferenceEquals(canonical,face.Preview),"entering practice preserves camera opt-in, capture and preview");
                check(hud.CollapsedPreview&&!hud.PreviewExpanded&&hud.RoomToolbarRect.rect.height<=70&&hud.CameraPanelRect.rect.height<=160,"connected HUD uses compact top strip and collapsed preview");
                check(!hud.CaptionPanelRect.gameObject.activeInHierarchy,"calibration does not create a permanent arena instruction panel");
                Canvas.ForceUpdateCanvases();camera.Render();
                var playPoint=RectTransformUtility.WorldToScreenPoint(camera,hud.SafeAreaRect.TransformPoint(hud.SafeAreaRect.rect.center));
                var playHit=new PointerEventData(EventSystem.current){position=playPoint};var playHits=new List<RaycastResult>();raycaster.Raycast(playHit,playHits);
                check(playHits.Count>0&&playHits[0].gameObject==hud.LookControl.gameObject,"arena center remains available for swipe look");
                Tap(hud.CameraPreviewButton,raycaster,camera);Tick(hud);check(hud.PreviewExpanded&&!hud.CollapsedPreview&&face.Running,"preview expands without interrupting capture");
                Tap(hud.CameraPreviewButton,raycaster,camera);Tick(hud);check(hud.CollapsedPreview&&!hud.PreviewExpanded&&face.Running,"preview collapses without interrupting capture");
                Tap(hud.SettingsButton,raycaster,camera);Tick(hud);check(hud.TrackingDiagnostics==session.PerformanceSummary,"settings uses measured game/camera/video diagnostics");
                check(hud.CameraPowerButton.GetComponentInChildren<Text>().text=="Camera off","explicit camera-off action lives in Settings");
                Tap(hud.SettingsButton,raycaster,camera);Tick(hud);
                check(!face.Ready&&!hud.StartButton.interactable,"uncalibrated camera cannot use Start");
                session.StartMatch();Tick(session);Tick(hud);check(session.State.phase==(int)MatchPhase.Lobby,"session rejects starting before eye calibration");
                Inside(hud.SettingsButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" lobby Settings button");
                Inside(hud.CameraPanelRect,safe,camera,check,prefix+" lobby camera panel");
                Inside(hud.StartButton.GetComponent<RectTransform>(),safe,camera,check,prefix+" lobby Start button");
                if(hud.CaptionPanelRect.gameObject.activeInHierarchy)Inside(hud.CaptionPanelRect,safe,camera,check,prefix+" lobby caption");
                var lobby=Capture(camera,target,Path.Combine(outputFolder,prefix+"-practice-lobby.png"));UnityEngine.Object.DestroyImmediate(lobby);
                // Render the actual competition card at native phone safe-area
                // sizes, including independent eyes and missing-face grace.
                var contest=new Tournament();contest.Add(0,"UI Pilot").phoneCamera=true;contest.Add(1,"Friend");contest.Input(0,0,0,true,true,0,0,0);contest.Input(1,0,0,true,true,0,0,0);contest.Start();
                long eyeSequence=100;double eyeTime=100+index*100;
                foreach(string pose in new[]{"countdown","open","closing","lost"})
                {
                    contest.Phase=pose=="countdown"?MatchPhase.Countdown:MatchPhase.Duel;contest.Remaining=pose=="countdown"?3:40;
                    bool tracked=pose!="lost";float closed=pose=="closing"?.5f:0;contest.Find(0).faceMissingFor=tracked?0:.6f;
                    face.SubmitTestSample(closed,0,tracked,eyeTime+=.04,eyeSequence++);
                    typeof(StareMobileSession).GetProperty("State").SetValue(session,contest.Snapshot());Tick(hud);Canvas.ForceUpdateCanvases();
                    check(hud.CompetitionPreview&&hud.PreviewExpanded,"competition automatically expands selfie: "+prefix+" "+pose);
                    Inside(hud.CameraPanelRect,safe,camera,check,prefix+" competition selfie "+pose);
                    foreach(var label in hud.CameraPanelRect.GetComponentsInChildren<Text>().Where(label=>label.gameObject.activeInHierarchy))
                        check(label.preferredHeight<=label.rectTransform.rect.height+3,"competition text fits: "+label.text);
                    var matchShot=Capture(camera,target,Path.Combine(outputFolder,prefix+"-eyes-"+pose+".png"));UnityEngine.Object.DestroyImmediate(matchShot);
                }
                session.Leave();camera.targetTexture=oldTarget;target.Release();UnityEngine.Object.DestroyImmediate(target);target=null;
            }
            return count+" phone UI/name/settings/orientation checks passed; fourteen 2x-MSAA PNGs saved in "+Path.GetFullPath(outputFolder);
        }
        finally
        {
            session.Leave();if(captureStarted)face.StopCamera();preferences.Dispose();session.SetLandscapeMode(oldLandscape);
            camera.targetTexture=oldTarget;camera.rect=oldRect;camera.aspect=oldAspect;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
            RenderTexture.active=oldActive;if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            canvas.renderMode=oldRenderMode;canvas.worldCamera=oldWorldCamera;canvas.planeDistance=oldPlane;
            hud.ClearReviewViewport();scaler.uiScaleMode=oldScaleMode;scaler.referenceResolution=oldReference;scaler.screenMatchMode=oldMatchMode;scaler.matchWidthOrHeight=oldMatch;scaler.scaleFactor=oldScalerScale;canvas.scaleFactor=oldCanvasScale;
            hud.PlayerNameInput.SetTextWithoutNotify(oldDraft);hud.RoomCodeInput.SetTextWithoutNotify(oldCode);hud.ShowSettings(false);hud.ShowNameEditor(false);
            if(oldSettings)hud.ShowSettings(true);else if(oldNameModal){hud.ShowNameEditor(true);hud.NameReviewInput.SetTextWithoutNotify(oldNameReview);}
            typeof(StareMobileSession).GetProperty("Status").SetValue(session,oldStatus);
            var restoredMusic=session.GetComponents<AudioSource>().FirstOrDefault(source=>source.loop&&source.clip==Resources.Load<AudioClip>("Audio/ArenaPulse"));
            if(restoredMusic!=null){restoredMusic.volume=session.MusicVolume;restoredMusic.mute=!session.MusicEnabled;}
            Tick(hud);Canvas.ForceUpdateCanvases();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(oldSelected);
        }
    }
}
#endif
