using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Staredown
{
    /// <summary>Responsive phone controls. Camera sharing starts only after an explicit camera tap.</summary>
    public sealed class StareMobileHud : MonoBehaviour
    {
        static readonly Color Ink=new Color(.025f,.045f,.07f,.96f),Surface=new Color(.065f,.105f,.145f,.98f),Cyan=new Color(.25f,.94f,1),Muted=new Color(.67f,.77f,.82f);
        StareMobileSession session;Canvas canvas;CanvasScaler scaler;Font regular,bold;Sprite rounded;
        RectTransform voicePanel;Text voiceDetails;Button voiceRepair,voiceDone;
        RectTransform leftEyeChip,rightEyeChip,trackingGrace,trackingGraceFill;Text leftEyeText,rightEyeText,eyeGuidance;
        RectTransform safe,homeShade,home,homeIntro,nameCard,playCard,homeStatusPanel,roomBar,cameraPanel,cameraFrame,captionPanel,dancePicker,nameEditor,settingsPanel,scrim;
        Text playTitle,joinHint,roomMicLabel,videoShareLabel,slingTitle,slingHint;RectTransform slingPanel,slingBall;StareMobileSlingPad slingPad;Button roomMic,videoShare;
        Text brand,brandTag,hero,description,namePrompt,homeHint,homeStatus,roomLabel,roomStatus,caption,movementHint,lookHint,cameraTag,cameraStatus,cameraPrivacy;
        Text fillLabel,startLabel,nameButtonLabel,cameraToggleLabel,nameTitle,nameHint,nameError,musicLabel,musicPercent,micLabel,voiceLabel,voiceStatus,calibrationStatus,diagnosticsTitle,diagnosticsText;
        InputField code,playerName,nameEditorInput;RawImage preview;Slider musicVolume;
        Button practice,host,join,leave,start,fill,dance,cameraToggle,cameraCard,cameraPower,editPlayerName,settingsButton,settingsDone,nameCancel,nameConfirm,musicToggle,mic,voiceToggle,portraitChoice,landscapeChoice,calibrate,settingsName,settingsLeave;
        StareMobileJoystick joystick;StareMobileLookPad lookPad;
        Vector2 lastScreen,layoutSize;Rect lastSafe;bool portrait,lastConnected,lastLobby,lastHosting,layoutReady,lastCanThrow;
        string pendingEntry="",pendingCode="",entryError="";bool syncingMusic;
        bool previewExpanded,wasConnected,wasCanMove;float movementHintUntil,captionChangedAt;string previousCaption="";
        bool contestPreview;float missingEyesAt=-1;
        static readonly Color EyeGood=new Color(.31f,1,.73f),EyeWarning=new Color(1,.77f,.31f),EyeDanger=new Color(1,.37f,.40f);
#if UNITY_EDITOR
        bool reviewViewport;Vector2 reviewPixels;Rect reviewSafe;
#endif
        public RawImage CameraPreview=>preview;
        public string ActiveCaption=>caption!=null?caption.text:"";
        public bool HomeVisible=>home!=null&&home.gameObject.activeSelf;
        public bool NameEditorVisible=>nameEditor!=null&&nameEditor.gameObject.activeSelf;
        public bool SettingsVisible=>settingsPanel!=null&&settingsPanel.gameObject.activeSelf;
        public bool IsPortraitLayout=>portrait;
        public Vector2 LayoutSize=>layoutSize;
        public RectTransform SafeAreaRect=>safe;
        public RectTransform HomePanelRect=>home;
        public RectTransform CameraPanelRect=>cameraPanel;
        public RectTransform CameraPreviewRect=>preview!=null?preview.rectTransform:null;
        public RectTransform SettingsPanelRect=>settingsPanel;
        public RectTransform CaptionPanelRect=>captionPanel;
        public InputField PlayerNameInput=>playerName;
        public InputField NameReviewInput=>nameEditorInput;
        public InputField RoomCodeInput=>code;
        public Button SettingsButton=>settingsButton;
        public Button PracticeButton=>practice;
        public Button HostButton=>host;
        public Button JoinButton=>join;
        public Button ConfirmNameButton=>nameConfirm;
        public Button StartButton=>start;
        public Button MusicToggleButton=>musicToggle;
        public Slider MusicVolumeSlider=>musicVolume;
        public Button PortraitButton=>portraitChoice;
        public Button LandscapeButton=>landscapeChoice;
        public Button CalibrateButton=>calibrate;
        public Button CameraPowerButton=>cameraPower;
        public Button CameraPreviewButton=>cameraCard;
        public Button CameraSetupButton=>cameraToggle;
        public Button RoomMicButton=>roomMic;
        public Button ShareVideoButton=>videoShare;
        public RectTransform SlingshotPanelRect=>slingPanel;
        public StareMobileSlingPad SlingshotControl=>slingPad;
        public bool CollapsedPreview=>session!=null&&session.Connected&&!PreviewExpanded;
        public bool PreviewExpanded=>session!=null&&session.Connected&&(previewExpanded||contestPreview);
        public bool CompetitionPreview=>contestPreview;
        public string EyeStatus=>cameraStatus!=null?cameraStatus.text:"";
        public string EyeGuidance=>eyeGuidance!=null?eyeGuidance.text:"";
        public string LeftEyeStatus=>leftEyeText!=null?leftEyeText.text:"";
        public string RightEyeStatus=>rightEyeText!=null?rightEyeText.text:"";
        public float TrackingGraceRemaining {get;private set;}=1;
        public RectTransform RoomToolbarRect=>roomBar;
        public string TrackingDiagnostics=>diagnosticsText!=null?diagnosticsText.text:"";
        public string PendingEntryAction=>pendingEntry;
        public bool ValidNameDraft=>playerName!=null&&StarePlayerName.IsValid(playerName.text);
        public int MovePointerId=>joystick!=null?joystick.PointerId:int.MinValue;
        public int LookPointerId=>lookPad!=null?lookPad.PointerId:int.MinValue;
        public StareMobileJoystick MovementControl=>joystick;
        public StareMobileLookPad LookControl=>lookPad;

        public void Initialize(StareMobileSession owner)
        {
            if(canvas!=null)return;session=owner;
            regular=Resources.Load<Font>("UI/Lato-Regular")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");bold=Resources.Load<Font>("UI/Lato-SemiBold")??regular;rounded=RoundedSprite();
            var root=new GameObject("Mobile overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;scaler=root.GetComponent<CanvasScaler>();
            safe=LayoutRect("Safe area",root.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            EnsureEvents();BuildLook();BuildHeader();BuildHome();BuildRoom();BuildCamera();BuildControls();BuildModals();
            settingsButton.transform.SetAsLastSibling();UpdateSafeArea();Update();
        }
        void EnsureEvents()
        {
            if(EventSystem.current!=null)return;
            var events=new GameObject("Mobile touch events",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        void BuildLook()
        {
            var area=LayoutRect("Swipe to look",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);area.gameObject.AddComponent<Image>().color=Color.clear;
            lookPad=area.gameObject.AddComponent<StareMobileLookPad>();lookPad.Initialize(session,canvas);
        }
        void BuildHeader()
        {
            homeShade=LayoutRect("Entry backdrop",safe.parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);homeShade.SetAsFirstSibling();
            var shade=homeShade.gameObject.AddComponent<Image>();shade.color=new Color(.008f,.018f,.028f,.78f);shade.raycastTarget=false;
            brand=Label("STAREDOWN",safe,Vector2.zero,Vector2.one,32,Color.white,true);brandTag=Label("PHONE × VR",safe,Vector2.zero,Vector2.one,16,Cyan,true);
            settingsButton=ButtonElement("Settings",safe,Vector2.zero,new Vector2(152,56),()=>ShowSettings(!SettingsVisible));
        }
        void BuildHome()
        {
            home=LayoutRect("Join the arena",safe,new Vector2(0,1),new Vector2(0,1),Vector2.zero,Vector2.zero);
            homeIntro=Box("Staring contest",home,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Ink);
            hero=Label("Don't blink.",homeIntro,Vector2.zero,Vector2.one,42,Color.white,true);
            description=Label("Keep your eyes open longer than your opponent.",homeIntro,Vector2.zero,Vector2.one,23,Color.white);
            nameCard=Box("Your identity",home,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Ink);
            namePrompt=Label("YOUR NAME",nameCard,Vector2.zero,Vector2.one,19,Cyan,true);
            playerName=NameField(nameCard);playerName.SetTextWithoutNotify(session.HasPlayerName?session.PlayerName:"");
            playerName.targetGraphic.color=new Color(.11f,.18f,.23f,1);
            playerName.placeholder.GetComponent<Text>().text="Enter your name";
            playerName.textComponent.alignment=TextAnchor.MiddleLeft;playerName.placeholder.GetComponent<Text>().alignment=TextAnchor.MiddleLeft;
            playerName.onValueChanged.AddListener(value=>entryError="");
            playerName.onEndEdit.AddListener(value=>playerName.SetTextWithoutNotify(StarePlayerName.Clean(value)));
            homeHint=Label("Shown above your avatar.",nameCard,Vector2.zero,Vector2.one,16,Muted);
            playCard=Box("Play with friends",home,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Ink);
            playTitle=Label("Play with friends",playCard,Vector2.zero,Vector2.one,22,Color.white,true);
            joinHint=Label("HAVE A ROOM CODE?",playCard,Vector2.zero,Vector2.one,14,Muted,true);
            practice=ButtonElement("Practice with bots",home,Vector2.zero,Vector2.one,()=>RequestEntryForReview("practice"));
            host=ButtonElement("Create a room",playCard,Vector2.zero,Vector2.one,()=>RequestEntryForReview("host"),true);
            var field=Box("Four digit room code",playCard,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Surface);
            code=field.gameObject.AddComponent<InputField>();code.characterLimit=StareRelayNetwork.CodeLength;code.contentType=InputField.ContentType.IntegerNumber;code.keyboardType=TouchScreenKeyboardType.NumberPad;
            var value=Label("",field,new Vector2(14,-8),new Vector2(200,50),27,Color.white,true,TextAnchor.MiddleCenter);var placeholder=Label("4-digit code",field,new Vector2(14,-8),new Vector2(200,50),19,Muted,false,TextAnchor.MiddleCenter);
            code.textComponent=value;code.placeholder=placeholder;code.targetGraphic=field.GetComponent<Image>();
            join=ButtonElement("Join room",playCard,Vector2.zero,Vector2.one,()=>RequestEntryForReview("join"));
            homeStatusPanel=Box("Entry feedback",home,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Ink);
            homeStatus=Label("",homeStatusPanel,Vector2.zero,Vector2.one,17,EyeWarning);
        }
        void BuildRoom()
        {
            roomBar=Box("Room toolbar",safe,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Ink);
            roomBar.GetComponent<Image>().raycastTarget=false;
            roomMic=ButtonElement("Mic on",safe,Vector2.zero,Vector2.one,()=>session.Act("mic"));roomMicLabel=roomMic.GetComponentInChildren<Text>();
            roomLabel=Label("",roomBar,Vector2.zero,Vector2.one,28,Color.white,true);roomStatus=Label("",roomBar,Vector2.zero,Vector2.one,18,Muted);
            editPlayerName=ButtonElement("Your name",roomBar,Vector2.zero,Vector2.one,()=>ShowNameEditor(true));nameButtonLabel=editPlayerName.GetComponentInChildren<Text>();Fit(nameButtonLabel,15,21);
            fill=ButtonElement("Bots: on",roomBar,Vector2.zero,Vector2.one,()=>session.Act("bot-fill"));fillLabel=fill.GetComponentInChildren<Text>();
            start=ButtonElement("Start match",safe,Vector2.zero,Vector2.one,()=>session.StartMatch(),true);startLabel=start.GetComponentInChildren<Text>();Fit(startLabel,16,23);
            leave=ButtonElement("Leave",roomBar,Vector2.zero,Vector2.one,()=>session.Leave());
            captionPanel=Box("Match caption",safe,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Ink);caption=Label("",captionPanel,Vector2.zero,Vector2.one,25,Color.white,true,TextAnchor.MiddleCenter);
            captionPanel.GetComponent<Image>().raycastTarget=false;
        }
        void BuildCamera()
        {
            cameraPanel=Box("Front camera",safe,Vector2.one,Vector2.zero,Vector2.one,Vector2.one,Ink);
            cameraCard=cameraPanel.gameObject.AddComponent<Button>();cameraCard.transition=Selectable.Transition.None;cameraCard.targetGraphic=cameraPanel.GetComponent<Image>();cameraCard.navigation=new Navigation{mode=Navigation.Mode.None};
            cameraCard.onClick.AddListener(()=>{if(session.Connected)TogglePreview();else CameraSetup();});
            cameraTag=Label("FRONT CAMERA",cameraPanel,Vector2.zero,Vector2.one,14,Cyan,true);
            cameraFrame=Box("Camera frame",cameraPanel,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Surface);
            var backing=LayoutRect("Camera letterbox",cameraFrame,Vector2.zero,Vector2.one,new Vector2(3,3),new Vector2(-3,-3));
            var backingImage=backing.gameObject.AddComponent<Image>();backingImage.sprite=rounded;backingImage.type=Image.Type.Sliced;backingImage.color=Surface;backingImage.raycastTarget=false;
            var image=LayoutRect("Front facing preview",cameraFrame,Vector2.zero,Vector2.one,new Vector2(3,3),new Vector2(-3,-3));
            preview=image.gameObject.AddComponent<RawImage>();preview.raycastTarget=false;
            // Preview pixels are upright and unmirrored. Only this LOCAL display
            // mirrors horizontally; the transmitted camera head stays canonical.
            preview.uvRect=new Rect(1,0,-1,1);
            image.gameObject.AddComponent<AspectRatioFitter>().aspectMode=AspectRatioFitter.AspectMode.FitInParent;
            cameraStatus=Label("Camera is off",cameraPanel,Vector2.zero,Vector2.one,16,Color.white);
            cameraPrivacy=Label("Preview only",cameraPanel,Vector2.zero,Vector2.one,13,Muted);
            cameraToggle=ButtonElement("Enable camera",cameraPanel,Vector2.zero,Vector2.one,CameraSetup,true);cameraToggleLabel=cameraToggle.GetComponentInChildren<Text>();Fit(cameraToggleLabel,15,19);
            leftEyeChip=Box("Left eye status",cameraPanel,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Surface);leftEyeChip.GetComponent<Image>().raycastTarget=false;
            rightEyeChip=Box("Right eye status",cameraPanel,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Surface);rightEyeChip.GetComponent<Image>().raycastTarget=false;
            leftEyeText=Label("L · OPEN",leftEyeChip,Vector2.zero,Vector2.one,14,EyeGood,true,TextAnchor.MiddleCenter);StretchLabel(leftEyeText,3,1);
            rightEyeText=Label("R · OPEN",rightEyeChip,Vector2.zero,Vector2.one,14,EyeGood,true,TextAnchor.MiddleCenter);StretchLabel(rightEyeText,3,1);
            eyeGuidance=Label("Keep both eyes in view.",cameraPanel,Vector2.zero,Vector2.one,16,Muted,false,TextAnchor.MiddleCenter);
            trackingGrace=Box("Tracking grace",cameraPanel,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Surface);trackingGrace.GetComponent<Image>().raycastTarget=false;
            trackingGraceFill=Box("Time to return eyes",trackingGrace,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),EyeDanger);trackingGraceFill.GetComponent<Image>().raycastTarget=false;
        }
        void BuildControls()
        {
            var pad=Box("Movement joystick",safe,Vector2.zero,Vector2.zero,new Vector2(144,144),Vector2.zero,new Color(.05f,.12f,.18f,.80f));
            var knob=Box("Joystick thumb",pad,new Vector2(.5f,.5f),Vector2.zero,new Vector2(58,58),new Vector2(.5f,.5f),Cyan);knob.GetComponent<Image>().raycastTarget=false;
            joystick=pad.gameObject.AddComponent<StareMobileJoystick>();joystick.Initialize(session,knob,49);
            movementHint=Label("",safe,Vector2.zero,Vector2.one,17,Color.white);lookHint=Label("Swipe to look",safe,Vector2.zero,Vector2.one,17,Muted,false,TextAnchor.MiddleRight);
            dance=ButtonElement("Dance",safe,Vector2.zero,new Vector2(144,64),()=>{session.Slingshot?.Cancel();dancePicker.gameObject.SetActive(!dancePicker.gameObject.activeSelf);});
            dancePicker=Box("Quick dances",safe,new Vector2(1,0),Vector2.zero,new Vector2(238,300),new Vector2(1,0),Ink);
            string[] options={"Hip hop","Charleston","Lindy hop","Fancy footwork","Stop dancing"};
            for(int index=0;index<options.Length;index++){int choice=index==4?-1:index;var button=ButtonElement(options[index],dancePicker,new Vector2(10,-10-index*57),new Vector2(218,49),()=>{session.Act("dance:"+choice);dancePicker.gameObject.SetActive(false);});Fit(button.GetComponentInChildren<Text>(),17,21);}
            dancePicker.gameObject.SetActive(false);
            slingPanel=Box("Touch slingshot",safe,new Vector2(1,0),Vector2.zero,new Vector2(188,172),new Vector2(1,0),Ink);
            slingTitle=Label("PULL & RELEASE",slingPanel,new Vector2(10,-10),new Vector2(168,26),16,Cyan,true,TextAnchor.MiddleCenter);
            slingHint=Label("Pull ball down. Sideways to aim.",slingPanel,new Vector2(9,-127),new Vector2(170,36),13,Muted,false,TextAnchor.MiddleCenter);
            var forkLeft=Box("Sling left fork",slingPanel,new Vector2(.5f,1),new Vector2(-36,-61),new Vector2(13,54),new Vector2(.5f,.5f),Muted);forkLeft.localRotation=Quaternion.Euler(0,0,25);forkLeft.GetComponent<Image>().raycastTarget=false;
            var forkRight=Box("Sling right fork",slingPanel,new Vector2(.5f,1),new Vector2(36,-61),new Vector2(13,54),new Vector2(.5f,.5f),Muted);forkRight.localRotation=Quaternion.Euler(0,0,-25);forkRight.GetComponent<Image>().raycastTarget=false;
            slingBall=Box("Drag projectile",slingPanel,new Vector2(.5f,1),new Vector2(0,-72),new Vector2(48,48),new Vector2(.5f,.5f),Cyan);slingBall.GetComponent<Image>().raycastTarget=false;
            slingPad=slingPanel.gameObject.AddComponent<StareMobileSlingPad>();slingPad.Initialize(session.Slingshot,slingBall);slingPanel.gameObject.SetActive(false);
        }
        void BuildModals()
        {
            scrim=Box("Modal backdrop",safe,Vector2.zero,Vector2.zero,Vector2.one,Vector2.zero,new Color(.006f,.012f,.025f,.76f));scrim.anchorMin=Vector2.zero;scrim.anchorMax=Vector2.one;scrim.offsetMin=scrim.offsetMax=Vector2.zero;scrim.gameObject.SetActive(false);
            BuildSettings();BuildNameEditor();BuildVoiceCheck();
        }
        void BuildSettings()
        {
            settingsPanel=Box("Phone settings",safe,new Vector2(.5f,1),Vector2.zero,Vector2.one,new Vector2(.5f,1),Ink);
            settingsPanel.GetComponent<Image>().color=new Color(Ink.r,Ink.g,Ink.b,1);
            Label("Settings",settingsPanel,new Vector2(20,-18),new Vector2(300,40),30,Color.white,true);
            settingsDone=ButtonElement("Done",settingsPanel,Vector2.zero,new Vector2(96,50),()=>ShowSettings(false));
            musicLabel=Label("Music",settingsPanel,Vector2.zero,Vector2.one,24,Color.white,true);
            musicToggle=ButtonElement("Music on",settingsPanel,Vector2.zero,Vector2.one,()=>session.SetMusicEnabled(!session.MusicEnabled));
            musicPercent=Label("",settingsPanel,Vector2.zero,Vector2.one,18,Muted,false,TextAnchor.MiddleRight);
            musicVolume=BuildSlider(settingsPanel);musicVolume.onValueChanged.AddListener(value=>{if(!syncingMusic)session.SetMusicVolume(value);});
            mic=ButtonElement("Enable mic",settingsPanel,Vector2.zero,Vector2.one,()=>session.Act("mic"));micLabel=mic.GetComponentInChildren<Text>();Fit(micLabel,16,22);
            voiceToggle=ButtonElement("Voice on",settingsPanel,Vector2.zero,Vector2.one,()=>session.Act("voice"));voiceLabel=voiceToggle.GetComponentInChildren<Text>();
            voiceStatus=Label("",settingsPanel,Vector2.zero,Vector2.one,17,Muted);
            portraitChoice=ButtonElement("Portrait",settingsPanel,Vector2.zero,Vector2.one,()=>session.SetLandscapeMode(false));landscapeChoice=ButtonElement("Landscape",settingsPanel,Vector2.zero,Vector2.one,()=>session.SetLandscapeMode(true));
            calibrate=ButtonElement("Calibrate eyes",settingsPanel,Vector2.zero,Vector2.one,()=>session.Act("calibrate"),true);
            cameraPower=ButtonElement("Enable camera",settingsPanel,Vector2.zero,Vector2.one,()=>session.Act("camera"));
            videoShare=ButtonElement("Video shared",settingsPanel,Vector2.zero,Vector2.one,()=>session.Act("share-video"));videoShareLabel=videoShare.GetComponentInChildren<Text>();
            calibrationStatus=Label("",settingsPanel,Vector2.zero,Vector2.one,18,Muted);
            diagnosticsTitle=Label("TRACKING DIAGNOSTICS",settingsPanel,Vector2.zero,Vector2.one,16,Cyan,true);
            diagnosticsText=Label("",settingsPanel,Vector2.zero,Vector2.one,14,Muted);
            settingsName=ButtonElement("Edit your name",settingsPanel,Vector2.zero,Vector2.one,()=>ShowNameEditor(true));settingsLeave=ButtonElement("Leave room",settingsPanel,Vector2.zero,Vector2.one,()=>{ShowSettings(false);session.Leave();});
            fill.transform.SetParent(settingsPanel,false);
        }
        void BuildVoiceCheck()
        {
            voicePanel=Box("Voice check",safe,new Vector2(.5f,.5f),Vector2.zero,new Vector2(500,560),new Vector2(.5f,.5f),Ink);
            Label("Voice check",voicePanel,new Vector2(20,-20),new Vector2(450,40),28,Color.white,true);
            voiceDetails=Label("",voicePanel,new Vector2(20,-80),new Vector2(450,350),20,Muted);
            voiceRepair=ButtonElement("Reconnect voice",voicePanel,new Vector2(20,-438),new Vector2(280,58),()=>session.Voice?.ReconnectVoice());
            voiceDone=ButtonElement("Done",voicePanel,new Vector2(320,-438),new Vector2(160,58),()=>voicePanel.gameObject.SetActive(false));
            voicePanel.gameObject.SetActive(false);
            var voiceCheck=ButtonElement("Voice check",voiceStatus.rectTransform,Vector2.zero,Vector2.one,()=>voicePanel.gameObject.SetActive(true));voiceCheck.GetComponent<Image>().color=Color.clear;var checkRect=voiceCheck.GetComponent<RectTransform>();checkRect.anchorMin=Vector2.zero;checkRect.anchorMax=Vector2.one;checkRect.offsetMin=checkRect.offsetMax=Vector2.zero;
            // Status text stays readable; the transparent hit target opens details.
            foreach(var label in voiceStatus.GetComponentsInChildren<Text>())if(label!=voiceStatus)label.text="";
        }
        void BuildNameEditor()
        {
            nameEditor=Box("Review player name",safe,new Vector2(.5f,1),Vector2.zero,Vector2.one,new Vector2(.5f,1),Ink);
            nameEditor.GetComponent<Image>().color=new Color(Ink.r,Ink.g,Ink.b,1);
            nameTitle=Label("Your arena name",nameEditor,Vector2.zero,Vector2.one,28,Color.white,true);
            nameEditorInput=NameField(nameEditor);nameEditorInput.onValueChanged.AddListener(value=>nameError.text="");
            nameHint=Label("Everyone in the room will see this name.",nameEditor,Vector2.zero,Vector2.one,19,Muted);
            nameError=Label("",nameEditor,Vector2.zero,Vector2.one,18,new Color(1,.65f,.43f));
            nameCancel=ButtonElement("Cancel",nameEditor,Vector2.zero,Vector2.one,()=>ShowNameEditor(false));nameConfirm=ButtonElement("Save name",nameEditor,Vector2.zero,Vector2.one,ConfirmName,true);
            settingsPanel.gameObject.SetActive(false);nameEditor.gameObject.SetActive(false);
        }
        InputField NameField(Transform parent)
        {
            var field=Box("Player name",parent,new Vector2(0,1),Vector2.zero,Vector2.one,new Vector2(0,1),Surface);
            var input=field.gameObject.AddComponent<InputField>();input.characterLimit=StarePlayerName.MaxLength;input.contentType=InputField.ContentType.Standard;input.lineType=InputField.LineType.SingleLine;input.keyboardType=TouchScreenKeyboardType.Default;
            var value=Label("",field,new Vector2(14,-8),new Vector2(350,48),25,Color.white,true);var placeholder=Label("Your name",field,new Vector2(14,-8),new Vector2(350,48),23,Muted);
            input.textComponent=value;input.placeholder=placeholder;input.targetGraphic=field.GetComponent<Image>();
            input.onValidateInput=(text,index,character)=>StarePlayerName.InputCharacter(character)&&!char.IsControl(character)?character:'\0';return input;
        }
        Slider BuildSlider(Transform parent)
        {
            var node=LayoutRect("Music volume",parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,Vector2.zero);node.pivot=new Vector2(0,1);
            node.gameObject.AddComponent<Image>().color=Color.clear;
            var slider=node.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
            var track=Box("Volume track",node,new Vector2(0,.5f),Vector2.zero,new Vector2(300,8),new Vector2(0,.5f),Surface);track.anchorMin=new Vector2(0,.5f);track.anchorMax=new Vector2(1,.5f);track.offsetMin=new Vector2(0,-4);track.offsetMax=new Vector2(0,4);
            var fillArea=LayoutRect("Volume fill area",node,new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(0,-4),new Vector2(0,4));
            var fillRect=Box("Volume fill",fillArea,Vector2.zero,Vector2.zero,Vector2.one,Vector2.zero,Cyan);fillRect.anchorMin=Vector2.zero;fillRect.anchorMax=Vector2.one;fillRect.offsetMin=fillRect.offsetMax=Vector2.zero;slider.fillRect=fillRect;
            var handleArea=LayoutRect("Volume handle area",node,new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(14,-19),new Vector2(-14,19));
            var handle=Box("Volume thumb",handleArea,new Vector2(.5f,.5f),Vector2.zero,new Vector2(28,0),new Vector2(.5f,.5f),Cyan);slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.direction=Slider.Direction.LeftToRight;
            return slider;
        }
        public void RequestEntryForReview(string action)
        {
            if(action!="practice"&&action!="host"&&action!="join"||session.Busy||session.Connected)return;
            if(!StarePlayerName.IsValid(playerName.text)){entryError="Enter your name to continue.";playerName.ActivateInputField();return;}
            if(action=="join"&&!StareRelayNetwork.ValidCode(code.text)){entryError="Enter a 4-digit room code.";return;}
            string value=StarePlayerName.Clean(playerName.text);session.SetPlayerName(value);
            if(!session.HasPlayerName||session.PlayerName!=value){entryError="Your name could not be saved. Please try again.";return;}
            playerName.SetTextWithoutNotify(value);playerName.DeactivateInputField();
            if(action=="practice")session.Practice();else if(action=="host")session.Host();else session.Join(StareRelayNetwork.NormalizeCode(code.text));
        }
        public void ShowNameEditor(bool shown)
        {
            if(!shown){nameEditorInput.DeactivateInputField();nameEditor.gameObject.SetActive(false);pendingEntry="";pendingCode="";UpdateModal();return;}
            if(session.Connected&&session.State.phase!=(int)MatchPhase.Lobby)return;
            OpenNameEditor(session.HasPlayerName?session.PlayerName:playerName.text,"");
        }
        void OpenNameEditor(string value,string action)
        {
            settingsPanel.gameObject.SetActive(false);pendingEntry=action;nameEditorInput.SetTextWithoutNotify(value);nameError.text="";
            nameTitle.text=action.Length>0?"Enter as this name?":"Your arena name";
            nameHint.text="Everyone in the room will see this name.";
            nameConfirm.GetComponentInChildren<Text>().text=action=="join"?"Join room":action=="host"?"Create room":action=="practice"?"Start practice":"Save name";
            nameEditor.gameObject.SetActive(true);PauseTouches();UpdateModal();
        }
        public void ConfirmName()
        {
            string value=StarePlayerName.Clean(nameEditorInput.text);
            if(!StarePlayerName.IsValid(value)){nameError.text="Choose a name with at least one letter or number.";return;}
            session.SetPlayerName(value);
            if(!session.HasPlayerName||session.PlayerName!=value){nameError.text="Your name could not be saved. Please try again.";return;}
            string action=pendingEntry,room=pendingCode;playerName.SetTextWithoutNotify(value);ShowNameEditor(false);
            if(action=="practice")session.Practice();else if(action=="host")session.Host();else if(action=="join")session.Join(room);
        }
        public void ShowSettings(bool shown)
        {
            if(shown){ShowNameEditor(false);PauseTouches();}
            settingsPanel.gameObject.SetActive(shown);if(!shown&&voicePanel!=null)voicePanel.gameObject.SetActive(false);UpdateModal();
        }
        void CameraSetup()
        {
            if(session.MobileCamera!=null&&session.MobileCamera.CameraEnabledIntent)ShowSettings(true);
            else session.Act("camera");
        }
        public void TogglePreview()
        {
            if(!session.Connected||contestPreview)return;previewExpanded=!previewExpanded;layoutReady=false;
            ApplyLayout(layoutSize.x,layoutSize.y,portrait,true,session.State.phase==(int)MatchPhase.Lobby,session.Hosting);
        }
        void PauseTouches(){session?.Slingshot?.Cancel();joystick?.Release();lookPad?.Release();if(dancePicker!=null)dancePicker.gameObject.SetActive(false);}
        void UpdateModal(){scrim.gameObject.SetActive(SettingsVisible||NameEditorVisible);settingsButton.transform.SetAsLastSibling();}

        void Update()
        {
            if(session==null||canvas==null)return;UpdateSafeArea();
            bool connected=session.Connected;var state=session.State??new ArenaState();bool lobby=state.phase==(int)MatchPhase.Lobby;
            var local=state.fighters.FirstOrDefault(f=>f.id==session.LocalId);
            bool active=local!=null&&local.alive&&local.pair>=0&&local.pair<state.pairs.Length&&state.pairs[local.pair].winner<0;
            bool competing=connected&&active&&(state.phase==(int)MatchPhase.Countdown||state.phase==(int)MatchPhase.Duel);
            if(contestPreview!=competing){contestPreview=competing;missingEyesAt=-1;layoutReady=false;}
            if(connected!=wasConnected){previewExpanded=false;layoutReady=false;if(connected)movementHintUntil=Time.unscaledTime+4;}
            if(connected&&session.CanMove&&!wasCanMove)movementHintUntil=Time.unscaledTime+4;
            wasConnected=connected;wasCanMove=session.CanMove;
            if(connected&&!lobby&&NameEditorVisible)ShowNameEditor(false);
            if(!layoutReady||connected!=lastConnected||lobby!=lastLobby||session.Hosting!=lastHosting||session.CanThrow!=lastCanThrow)ApplyLayout(layoutSize.x,layoutSize.y,portrait,connected,lobby,session.Hosting);
            home.gameObject.SetActive(!connected);brand.gameObject.SetActive(!connected);brandTag.gameObject.SetActive(!connected);roomBar.gameObject.SetActive(connected);
            homeShade.gameObject.SetActive(!connected);
            bool modal=SettingsVisible||NameEditorVisible;
            roomMic.gameObject.SetActive(connected&&!modal);slingPanel.gameObject.SetActive(connected&&session.CanThrow&&!modal&&!dancePicker.gameObject.activeSelf);
            if(session.Slingshot!=null){slingTitle.text=session.Slingshot.Hint;slingBall.GetComponent<Image>().color=Color.Lerp(Cyan,new Color(1,.8f,.3f),session.Slingshot.Strength);}
            lookPad.gameObject.SetActive(connected&&!modal);lookHint.gameObject.SetActive(false);joystick.gameObject.SetActive(connected&&session.CanMove&&!modal);
            dance.gameObject.SetActive(connected&&!modal);dance.interactable=session.CanDance;
            if(!connected||!session.CanDance||modal)dancePicker.gameObject.SetActive(false);
            bool validDraft=StarePlayerName.IsValid(playerName.text);
            homeHint.text=validDraft?"Shown above your avatar.":"Enter your name before playing.";
            practice.interactable=host.interactable=!session.Busy&&validDraft;join.interactable=!session.Busy&&validDraft&&StareRelayNetwork.ValidCode(code.text);
            bool idleStatus=session.Status=="Enable your camera, or explore a room first."||session.Status=="Choose practice, create a room, or join friends.";
            homeStatus.text=entryError.Length>0?entryError:!validDraft||idleStatus?"":session.Status;
            homeStatusPanel.gameObject.SetActive(!string.IsNullOrEmpty(homeStatus.text));
            leave.gameObject.SetActive(false);editPlayerName.gameObject.SetActive(false);nameButtonLabel.text=session.PlayerName;
            roomLabel.text=string.IsNullOrEmpty(session.RoomCode)?"PRACTICE":"ROOM "+session.RoomCode;
            roomStatus.text=lobby?state.fighters.Count(f=>!f.bot)+" players · "+state.fighters.Count(f=>f.bot)+" bots":state.livesMode&&local!=null?"Round "+state.round+" · "+(local.alive?local.lives+(local.lives==1?" life":" lives"):"Spectating"):"Round "+state.round+" · "+state.fighters.Count(f=>f.alive)+" remaining";
            fill.gameObject.SetActive(session.Hosting&&lobby);fill.interactable=!session.Busy;fillLabel.text=state.botFill?"Humans + bots":"Humans only";
            var mobileCamera=session.MobileCamera;bool running=mobileCamera!=null&&mobileCamera.Running;
            bool enabledIntent=mobileCamera!=null&&mobileCamera.CameraEnabledIntent,recovering=mobileCamera!=null&&mobileCamera.Recovering;
            bool localReady=running&&mobileCamera.Valid&&mobileCamera.Ready;
            bool rosterReady=state.fighters.Length>=2&&state.fighters.All(f=>f.bot||f.ready&&f.valid);
            start.gameObject.SetActive(session.Hosting&&lobby&&!modal);start.interactable=!session.Busy&&session.HasPlayerName&&localReady&&rosterReady;
            startLabel.text=localReady&&!rosterReady?"Waiting…":"Start match";
            caption.text=MatchCaption(state,local,session.Status);
            if(caption.text!=previousCaption){previousCaption=caption.text;captionChangedAt=Time.unscaledTime;}
            bool urgent=active&&(!local.valid||state.trackingPaused),countdown=active&&state.phase==(int)MatchPhase.Countdown;
            bool freshCaption=Time.unscaledTime-captionChangedAt<(state.phase==(int)MatchPhase.Champion?5:3);
            captionPanel.gameObject.SetActive(connected&&!modal&&!string.IsNullOrEmpty(caption.text)&&(urgent||countdown||freshCaption));
            movementHint.gameObject.SetActive(connected&&!modal&&session.CanMove&&Time.unscaledTime<movementHintUntil);
            movementHint.text=local!=null&&!local.alive?"Left thumb: move outside fields · Swipe: look":"Left thumb: move · Swipe: look";
            bool live=session.VideoSending&&!string.IsNullOrEmpty(session.RoomCode);
            cameraTag.text=connected?(previewExpanded?"YOUR CAMERA −":live?"YOU · LIVE +":"YOU +"):"YOUR CAMERA";
            cameraStatus.text=recovering?"Resuming camera…":!enabledIntent?"Camera off":!running?"Starting camera…":!mobileCamera.Valid?"Face not visible":!mobileCamera.Ready?"Calibrating "+mobileCamera.CalibrationPercent+"%":"Eyes tracked";
            cameraStatus.color=localReady?Cyan:enabledIntent?new Color(1,.76f,.47f):Muted;
            UpdateEyeStatus(state,local,running&&mobileCamera.Valid);
            cameraPrivacy.text=live?"Video shared · eyes tracked":enabledIntent?(session.VideoSharingEnabled?"Video shared when joined":"Private video · eyes tracked"):"Enable once to track blinks";
            cameraPrivacy.gameObject.SetActive(!connected);cameraToggle.gameObject.SetActive(!connected);
            cameraToggleLabel.text=enabledIntent?"Camera settings":"Enable camera";
            preview.texture=mobileCamera!=null?mobileCamera.Preview:null;preview.enabled=running&&preview.texture!=null;
            if(preview.texture!=null)preview.GetComponent<AspectRatioFitter>().aspectRatio=(float)preview.texture.width/Mathf.Max(1,preview.texture.height);
            musicToggle.GetComponentInChildren<Text>().text=session.MusicEnabled?"Music on":"Music off";Selected(musicToggle,session.MusicEnabled);
            musicPercent.text=Mathf.RoundToInt(session.MusicVolume*100)+"%";syncingMusic=true;musicVolume.SetValueWithoutNotify(session.MusicVolume);syncingMusic=false;
            var voice=session.Voice;roomMic.interactable=mic.interactable=voiceToggle.interactable=voice!=null;
            micLabel.text=voice==null?"Microphone":!voice.MicPermissionGranted?"Enable mic":voice.MicMuted?"Unmute mic":"Mute mic";
            roomMicLabel.text=voice==null?"Mic off":!voice.MicPermissionGranted?"Enable mic":!voice.VoiceEnabled?"Voice off":voice.MicMuted?"Mic off":"Mic on";
            Selected(roomMic,voice!=null&&voice.MicPermissionGranted&&!voice.MicMuted&&voice.VoiceEnabled);
            videoShareLabel.text=session.VideoSharingEnabled?"Video: shared":"Video: private";Selected(videoShare,session.VideoSharingEnabled);
            voiceLabel.text=voice!=null&&voice.VoiceEnabled?"Voice on":"Voice off";voiceStatus.text=voice!=null?voice.ReceiveSummary+"\nTap for voice check":"Join an online room for room chat.";
            if(voicePanel!=null&&voicePanel.gameObject.activeSelf)voiceDetails.text=voice!=null?voice.DiagnosticsSummary:"Join a room first.";
            portraitChoice.GetComponentInChildren<Text>().text="Portrait";landscapeChoice.GetComponentInChildren<Text>().text="Landscape";Selected(portraitChoice,!session.LandscapeMode);Selected(landscapeChoice,session.LandscapeMode);
            calibrate.interactable=running&&!recovering;
            cameraPower.GetComponentInChildren<Text>().text=enabledIntent?"Camera off":"Enable camera";
            calibrationStatus.text=mobileCamera==null?"Front camera unavailable.":recovering?"Resuming the camera you enabled…":mobileCamera.Status+"\n"+(session.VideoSharingEnabled?"Video shared with this room.":"Video private. Eye tracking stays on.");
            if(SettingsVisible)diagnosticsText.text=session.PerformanceSummary;
            settingsName.interactable=!connected||lobby;settingsLeave.gameObject.SetActive(connected);
            nameConfirm.interactable=StarePlayerName.IsValid(nameEditorInput.text)&&!session.Busy;
        }
        public static string EyeLabel(bool tracked,float closure)
        {return !tracked?"LOST":closure>=BlinkGate.ClosureThreshold?"CLOSED":closure>=.3f?"CLOSING":"OPEN";}
        static Color EyeColor(bool tracked,float closure)
        {return !tracked||closure>=BlinkGate.ClosureThreshold?EyeDanger:closure>=.3f?EyeWarning:EyeGood;}
        void UpdateEyeStatus(ArenaState state,Fighter local,bool tracked)
        {
            leftEyeChip.gameObject.SetActive(contestPreview);rightEyeChip.gameObject.SetActive(contestPreview);eyeGuidance.gameObject.SetActive(contestPreview);
            cameraCard.interactable=!contestPreview;trackingGrace.gameObject.SetActive(false);
            cameraFrame.GetComponent<Image>().color=Surface;
            if(!contestPreview){missingEyesAt=-1;TrackingGraceRemaining=1;return;}
            var camera=session.MobileCamera;float closure=Mathf.Max(camera.Left,camera.Right);
            leftEyeText.text="L · "+EyeLabel(tracked,camera.Left);rightEyeText.text="R · "+EyeLabel(tracked,camera.Right);
            leftEyeText.color=EyeColor(tracked,camera.Left);rightEyeText.color=EyeColor(tracked,camera.Right);
            cameraTag.text="YOUR EYES";
            cameraStatus.text=!tracked?"Eyes not found":closure>=BlinkGate.ClosureThreshold?"Eyes closed":closure>=.3f?"Keep eyes open":"Eyes open";
            cameraStatus.color=EyeColor(tracked,closure);cameraFrame.GetComponent<Image>().color=cameraStatus.color;
            bool countdown=state.phase==(int)MatchPhase.Countdown;
            eyeGuidance.text=countdown?(tracked?"Stay in view. Blinks free until GO.":"Find both eyes before GO."):tracked?"Blink or leave view = out.":"Face the camera or you're out.";
            if(!countdown&&!tracked)
            {
                if(missingEyesAt<0)missingEyesAt=Time.unscaledTime;
                float elapsed=Mathf.Max(Time.unscaledTime-missingEyesAt,local!=null?local.faceMissingFor:0);
                TrackingGraceRemaining=Mathf.Clamp01(1-elapsed/Tournament.PhoneFaceLossGrace);
                trackingGrace.gameObject.SetActive(true);
                trackingGraceFill.sizeDelta=new Vector2(trackingGrace.rect.width*TrackingGraceRemaining,trackingGrace.rect.height);
            }
            else{missingEyesAt=-1;TrackingGraceRemaining=1;}
        }
        public static string MatchCaption(ArenaState state,Fighter local,string status)
        {
            if(state==null||local==null)return "";
            if(state.phase==(int)MatchPhase.Champion){var winner=state.fighters.FirstOrDefault(f=>f.id==state.winner);return state.winner==local.id?"You are the champion!":winner!=null?winner.name+" wins!":"Match complete";}
            if(!local.alive)return local.status!=null&&local.status.StartsWith("FACE LOST")?"Face left camera · you're out":"You're out";
            bool active=local.pair>=0&&local.pair<state.pairs.Length&&state.pairs[local.pair].winner<0;
            if(active&&state.trackingPaused)return "Tracking paused · face your camera";
            if(active&&state.phase==(int)MatchPhase.Countdown)return "Get ready · "+Mathf.CeilToInt(state.remaining);
            if(active&&state.phase==(int)MatchPhase.Duel)return ""; // The live eye card owns tracking guidance.
            if(state.phase==(int)MatchPhase.Lobby)return "";
            if(state.livesMode)
            {
                var pair=local.pair>=0&&local.pair<state.pairs.Length?state.pairs[local.pair]:null;
                if(pair!=null&&pair.loser==local.id)return "Life lost · "+local.lives+" "+(local.lives==1?"life":"lives")+" left";
                if(pair!=null&&pair.winner==local.id)return "Round won · "+local.lives+" "+(local.lives==1?"life":"lives")+" left";
                return "Next opponent soon · "+local.lives+" "+(local.lives==1?"life":"lives");
            }
            return "You advanced · next round soon";
        }
        void UpdateSafeArea()
        {
            var pixels=new Vector2(Screen.width,Screen.height);var inset=Screen.safeArea;
#if UNITY_EDITOR
            if(reviewViewport){pixels=reviewPixels;inset=reviewSafe;}
#endif
            if(pixels==lastScreen&&inset==lastSafe&&layoutReady)return;lastScreen=pixels;lastSafe=inset;
            portrait=pixels.y>=pixels.x;float scale=(portrait?pixels.x:pixels.y)/600f;
            // Keep the complete entry/settings flow inside shorter tablet and
            // split-screen safe areas as well as the normal iPhone layout.
            scale=Mathf.Max(.01f,Mathf.Min(scale,inset.height/(portrait?940f:620f)));
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=scale;canvas.scaleFactor=scale;
            safe.anchorMin=new Vector2(inset.x/Mathf.Max(1,pixels.x),inset.y/Mathf.Max(1,pixels.y));safe.anchorMax=new Vector2(inset.xMax/Mathf.Max(1,pixels.x),inset.yMax/Mathf.Max(1,pixels.y));
            layoutSize=new Vector2(inset.width/scale,inset.height/scale);layoutReady=false;
            ApplyLayout(layoutSize.x,layoutSize.y,portrait,session.Connected,session.State!=null&&session.State.phase==(int)MatchPhase.Lobby,session.Hosting);
        }
#if UNITY_EDITOR
        public void SetReviewViewport(Vector2 pixels,Rect safeArea)
        {reviewViewport=pixels.x>0&&pixels.y>0;reviewPixels=pixels;reviewSafe=safeArea;lastScreen=new Vector2(-1,-1);UpdateSafeArea();Canvas.ForceUpdateCanvases();Update();}
        public void ClearReviewViewport(){reviewViewport=false;lastScreen=new Vector2(-1,-1);UpdateSafeArea();Canvas.ForceUpdateCanvases();Update();}
#endif
        void ApplyLayout(float width,float height,bool tall,bool connected,bool lobby,bool hosting)
        {
            if(width<=0||height<=0)return;layoutReady=true;lastConnected=connected;lastLobby=lobby;lastHosting=hosting;lastCanThrow=session.CanThrow;
            const float margin=16;
            Place(settingsButton.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(1,1),new Vector2(-margin,-12),new Vector2(connected?112:160,connected?64:70));
            StretchLabel(settingsButton.GetComponentInChildren<Text>(),6,3);Fit(settingsButton.GetComponentInChildren<Text>(),18,connected?21:23);
            Place(roomMic.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(1,1),new Vector2(-140,-12),new Vector2(120,64));StretchLabel(roomMicLabel,6,3);Fit(roomMicLabel,15,19);
            Put(brand,new Vector2(16,-13),new Vector2(width-190,36),30);Put(brandTag,new Vector2(18,-51),new Vector2(width-190,24),16);
            float homeWidth=tall?width-32:Mathf.Min(760,width-282);float homeTop=tall?88:82;float homeHeight=tall?784:Mathf.Min(484,height-homeTop-12);
            Place(home,new Vector2(0,1),new Vector2(0,1),new Vector2(margin,-homeTop),new Vector2(homeWidth,homeHeight));
            float inner=homeWidth-40;
            if(tall)
            {
                Place(homeIntro,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(homeWidth,240));
                hero.text="Don't blink.";Put(hero,new Vector2(20,-22),new Vector2(homeWidth-244,62),40);Fit(hero,28,40);
                Put(description,new Vector2(20,-102),new Vector2(homeWidth-244,100),22);
                Place(nameCard,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-256),new Vector2(homeWidth,144));
                Place(playCard,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-416),new Vector2(homeWidth,236));
                PlaceButton(practice,0,668,homeWidth,60,22);
                Place(homeStatusPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-744),new Vector2(homeWidth,40));
            }
            else
            {
                Place(homeIntro,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(homeWidth,104));
                hero.text="Don't blink.";Put(hero,new Vector2(20,-12),new Vector2(inner,42),34);Fit(hero,28,34);Put(description,new Vector2(20,-62),new Vector2(inner,32),18);
                float column=(homeWidth-16)*.5f;
                Place(nameCard,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-120),new Vector2(column,144));
                Place(playCard,new Vector2(0,1),new Vector2(0,1),new Vector2(column+16,-120),new Vector2(column,236));
                PlaceButton(practice,0,280,column,58,21);
                Place(homeStatusPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-372),new Vector2(homeWidth,60));
            }
            float nameWidth=nameCard.rect.width-40,playWidth=playCard.rect.width-40,joinWidth=tall?140:112;
            Put(namePrompt,new Vector2(20,-14),new Vector2(nameWidth,24),18);FieldPlace(playerName,new Vector2(20,-48),new Vector2(nameWidth,60));
            Put(homeHint,new Vector2(20,-116),new Vector2(nameWidth,20),15);
            Put(playTitle,new Vector2(20,-14),new Vector2(playWidth,28),22);PlaceButton(host,20,54,playWidth,56,21);
            Put(joinHint,new Vector2(20,-122),new Vector2(playWidth,20),14);
            FieldPlace(code,new Vector2(20,-150),new Vector2(playWidth-joinWidth-12,64));PlaceButton(join,32+playWidth-joinWidth-12,150,joinWidth,64,20);
            Put(homeStatus,new Vector2(16,-8),new Vector2(homeWidth-32,tall?24:44),17);Fit(homeStatus,14,17);
            float cameraWidth=contestPreview?(tall?276:244):connected?(previewExpanded?218:132):(tall?188:218),cameraHeight=contestPreview?(tall?384:330):connected?(previewExpanded?252:152):216;
            Place(cameraPanel,Vector2.one,Vector2.one,new Vector2(-margin,connected?-88:tall?-100:-88),new Vector2(cameraWidth,cameraHeight));
            cameraPanel.GetComponent<Image>().color=connected?Ink:Surface;
            if(contestPreview)
            {
                float frameHeight=tall?224:172,eyesDown=frameHeight+43,titleDown=eyesDown+30,hintDown=titleDown+33;
                Put(cameraTag,new Vector2(12,-8),new Vector2(cameraWidth-24,18),13);
                Place(cameraFrame,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-34),new Vector2(cameraWidth-24,frameHeight));
                float half=(cameraWidth-32)*.5f;
                Place(leftEyeChip,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-eyesDown),new Vector2(half,24));
                Place(rightEyeChip,new Vector2(0,1),new Vector2(0,1),new Vector2(20+half,-eyesDown),new Vector2(half,24));
                Put(cameraStatus,new Vector2(12,-titleDown),new Vector2(cameraWidth-24,26),22);cameraStatus.alignment=TextAnchor.MiddleCenter;Fit(cameraStatus,18,22);
                Put(eyeGuidance,new Vector2(12,-hintDown),new Vector2(cameraWidth-24,36),16);Fit(eyeGuidance,14,16);
                Place(trackingGrace,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-(cameraHeight-13)),new Vector2(cameraWidth-24,4));
            }
            else if(connected&&!previewExpanded)
            {
                Put(cameraTag,new Vector2(8,-6),new Vector2(cameraWidth-16,16),12);Place(cameraFrame,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-25),new Vector2(cameraWidth-16,96));
                Put(cameraStatus,new Vector2(8,-124),new Vector2(cameraWidth-16,22),14);Fit(cameraStatus,12,14);
            }
            else if(connected)
            {
                Put(cameraTag,new Vector2(12,-8),new Vector2(cameraWidth-24,18),13);Place(cameraFrame,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-34),new Vector2(cameraWidth-24,154));
                Put(cameraStatus,new Vector2(12,-200),new Vector2(cameraWidth-24,40),16);Fit(cameraStatus,14,16);
            }
            else
            {
                Put(cameraTag,new Vector2(12,-8),new Vector2(cameraWidth-24,18),13);Place(cameraFrame,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-32),new Vector2(cameraWidth-24,80));
                Put(cameraStatus,new Vector2(12,-116),new Vector2(cameraWidth-24,20),14);Fit(cameraStatus,12,14);PlaceButton(cameraToggle,12,140,cameraWidth-24,48,17);
                Put(cameraPrivacy,new Vector2(12,-196),new Vector2(cameraWidth-24,16),11);
            }
            if(!contestPreview)cameraStatus.alignment=TextAnchor.UpperLeft;
            float roomWidth=Mathf.Min(width-292,560);
            Place(roomBar,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-12),new Vector2(roomWidth,64));
            Put(roomLabel,new Vector2(12,-7),new Vector2(roomWidth-24,29),22);Put(roomStatus,new Vector2(12,-39),new Vector2(roomWidth-24,19),14);
            Place(start.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,48),new Vector2(152,64));StretchLabel(startLabel,7,4);Fit(startLabel,18,22);
            Place(captionPanel,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,session.CanThrow?318:204),new Vector2(Mathf.Min(width-48,420),54));StretchLabel(caption,12,7);Fit(caption,16,22);
            Place(joystick.GetComponent<RectTransform>(),Vector2.zero,Vector2.zero,new Vector2(28,52),new Vector2(144,144));
            Place(dance.GetComponent<RectTransform>(),new Vector2(1,0),new Vector2(1,0),new Vector2(-20,session.CanThrow?236:48),new Vector2(134,60));
            Place(dancePicker,new Vector2(1,0),new Vector2(1,0),new Vector2(session.CanThrow&&!tall?-174:-20,session.CanThrow&&tall?308:120),new Vector2(238,300));
            Place(slingPanel,new Vector2(1,0),new Vector2(1,0),new Vector2(-20,48),new Vector2(188,172));
            Place(movementHint.rectTransform,Vector2.zero,Vector2.zero,new Vector2(20,10),new Vector2(width-40,30));Fit(movementHint,14,16);
            Place(lookHint.rectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(-174,59),new Vector2(tall?190:250,36));
            LayoutSettings(width,height,tall);LayoutName(width,height,tall);
            float voiceWidth=Mathf.Min(500,width-32),voiceHeight=Mathf.Min(560,height-32);
            Place(voicePanel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(voiceWidth,voiceHeight));
            voiceDetails.rectTransform.sizeDelta=new Vector2(voiceWidth-40,voiceHeight-190);
            PlaceButton(voiceRepair,20,voiceHeight-100,voiceWidth-200,58,20);PlaceButton(voiceDone,voiceWidth-180,voiceHeight-100,160,58,20);
        }
        void LayoutSettings(float width,float height,bool tall)
        {
            float panelWidth=tall?width-32:Mathf.Min(800,width-32),panelHeight=tall?840:520;
            float top=Mathf.Max(84,(height-panelHeight)*.5f);
            Place(settingsPanel,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-top),new Vector2(panelWidth,panelHeight));
            PlaceButton(settingsDone,panelWidth-110,14,90,64,20);
            float inner=panelWidth-40;bool showBots=session.Connected&&session.Hosting&&session.State.phase==(int)MatchPhase.Lobby;
            if(tall)
            {
                Put(musicLabel,new Vector2(20,-84),new Vector2(inner-150,34),24);PlaceButton(musicToggle,panelWidth-154,76,134,54,20);
                Place(musicVolume.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(24,-145),new Vector2(inner-68,50));Put(musicPercent,new Vector2(panelWidth-78,-152),new Vector2(56,30),18);
                float half=(inner-12)*.5f;PlaceButton(mic,20,216,half,58,20);PlaceButton(voiceToggle,32+half,216,half,58,20);Put(voiceStatus,new Vector2(20,-287),new Vector2(inner,52),14);
                PlaceButton(portraitChoice,20,354,half,58,22);PlaceButton(landscapeChoice,32+half,354,half,58,22);
                PlaceButton(calibrate,20,438,half,58,20);PlaceButton(cameraPower,32+half,438,half,58,20);Put(calibrationStatus,new Vector2(20,-508),new Vector2(inner,36),16);
                PlaceButton(videoShare,20,552,inner,50,20);
                Put(diagnosticsTitle,new Vector2(20,-616),new Vector2(inner,22),16);Put(diagnosticsText,new Vector2(20,-642),new Vector2(inner,56),12);
                PlaceButton(settingsName,20,710,showBots?half:inner,50,20);PlaceButton(fill,32+half,710,half,50,18);PlaceButton(settingsLeave,20,776,inner,48,21);
            }
            else
            {
                float column=(inner-24)*.5f,right=44+column;
                Put(musicLabel,new Vector2(20,-80),new Vector2(column-132,34),23);PlaceButton(musicToggle,20+column-124,72,124,52,19);
                Place(musicVolume.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(24,-139),new Vector2(column-66,50));Put(musicPercent,new Vector2(20+column-53,-150),new Vector2(53,30),17);
                float half=(column-10)*.5f;PlaceButton(mic,20,206,half,56,18);PlaceButton(voiceToggle,30+half,206,half,56,18);Put(voiceStatus,new Vector2(20,-276),new Vector2(column,58),14);
                PlaceButton(settingsName,20,354,showBots?half:column,50,17);PlaceButton(fill,30+half,354,half,50,17);PlaceButton(settingsLeave,20,418,column,48,20);
                PlaceButton(portraitChoice,right,82,half,56,19);PlaceButton(landscapeChoice,right+half+10,82,half,56,19);
                PlaceButton(calibrate,right,154,column,54,21);PlaceButton(cameraPower,right,218,column,54,21);PlaceButton(videoShare,right,282,column,52,20);Put(calibrationStatus,new Vector2(right,-346),new Vector2(column,36),16);
                Put(diagnosticsTitle,new Vector2(right,-394),new Vector2(column,22),15);Put(diagnosticsText,new Vector2(right,-420),new Vector2(column,80),12);
            }
        }
        void LayoutName(float width,float height,bool tall)
        {
            float panelWidth=Mathf.Min(520,width-32),panelHeight=324,top=tall?Mathf.Min(160,height*.16f):Mathf.Max(90,(height-panelHeight)*.5f);
            Place(nameEditor,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-top),new Vector2(panelWidth,panelHeight));
            Put(nameTitle,new Vector2(20,-18),new Vector2(panelWidth-40,39),27);FieldPlace(nameEditorInput,new Vector2(20,-78),new Vector2(panelWidth-40,64));
            Put(nameHint,new Vector2(20,-156),new Vector2(panelWidth-40,44),19);Put(nameError,new Vector2(20,-201),new Vector2(panelWidth-40,33),17);
            float half=(panelWidth-52)*.5f;PlaceButton(nameCancel,20,246,half,60,22);PlaceButton(nameConfirm,32+half,246,half,60,22);
        }
        static void Place(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;}
        void PlaceButton(Button button,float x,float down,float width,float height,int points){Place(button.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(x,-down),new Vector2(width,height));var label=button.GetComponentInChildren<Text>();StretchLabel(label,6,3);Fit(label,Mathf.Max(14,points-3),points);}
        void Put(Text label,Vector2 position,Vector2 size,int points){Place(label.rectTransform,new Vector2(0,1),new Vector2(0,1),position,size);label.fontSize=points;}
        void FieldPlace(InputField input,Vector2 position,Vector2 size){Place(input.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),position,size);StretchLabel(input.textComponent,14,8);if(input.placeholder!=null)StretchLabel(input.placeholder.GetComponent<Text>(),14,8);}
        static void StretchLabel(Text label,float horizontal,float vertical){var rect=label.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(horizontal,vertical);rect.offsetMax=new Vector2(-horizontal,-vertical);}
        static void Fit(Text label,int minimum,int maximum){label.resizeTextForBestFit=true;label.resizeTextMinSize=minimum;label.resizeTextMaxSize=maximum;}
        static void Selected(Button button,bool selected){var image=button.targetGraphic as Image;if(image!=null)image.color=selected?new Color(.12f,.35f,.39f):Surface;}
        RectTransform LayoutRect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high){var node=new GameObject(name,typeof(RectTransform));node.transform.SetParent(parent,false);var rect=node.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;return rect;}
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot,Color color){var rect=LayoutRect(name,parent,anchor,anchor,Vector2.zero,Vector2.zero);rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;var image=rect.gameObject.AddComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.color=color;return rect;}
        Text Label(string value,Transform parent,Vector2 position,Vector2 size,int points,Color color,bool heavy=false,TextAnchor alignment=TextAnchor.UpperLeft){var rect=LayoutRect(string.IsNullOrEmpty(value)?"Label":value,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,Vector2.zero);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;var text=rect.gameObject.AddComponent<Text>();text.text=value;text.font=heavy?bold:regular;text.fontSize=points;text.color=color;text.alignment=alignment;text.raycastTarget=false;text.supportRichText=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
        Button ButtonElement(string label,Transform parent,Vector2 position,Vector2 size,Action clicked,bool primary=false){var rect=Box(label,parent,new Vector2(0,1),position,size,new Vector2(0,1),primary?Cyan:Surface);var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>clicked());var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.8f,1,1);colors.pressedColor=new Color(.6f,.83f,.86f);colors.disabledColor=new Color(.5f,.5f,.5f,.6f);button.colors=colors;button.navigation=new Navigation{mode=Navigation.Mode.None};Label(label,rect,Vector2.zero,size,23,primary?Ink:Color.white,true,TextAnchor.MiddleCenter);return button;}
        Sprite RoundedSprite(){const int width=32;const float radius=8;var texture=new Texture2D(width,width,TextureFormat.RGBA32,false){name="Mobile rounded surface"};var pixels=new Color[width*width];for(int y=0;y<width;y++)for(int x=0;x<width;x++){var delta=new Vector2(Mathf.Max(radius-x-.5f,x+.5f-(width-radius)),Mathf.Max(radius-y-.5f,y+.5f-(width-radius)));float edge=new Vector2(Mathf.Max(0,delta.x),Mathf.Max(0,delta.y)).magnitude-radius;pixels[y*width+x]=new Color(1,1,1,Mathf.Clamp01(.5f-edge));}texture.SetPixels(pixels);texture.Apply();return Sprite.Create(texture,new Rect(0,0,width,width),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*radius);}
        void OnDisable(){PauseTouches();}
        void OnDestroy(){if(rounded!=null){Destroy(rounded.texture);Destroy(rounded);}}
    }

    /// <summary>The first finger owns movement until release; another finger can independently look.</summary>
    public sealed class StareMobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        StareMobileSession session; RectTransform knob, rect; float radius;
        public int PointerId { get; private set; } = int.MinValue;
        public void Initialize(StareMobileSession owner, RectTransform thumb, float reach) { session = owner; knob = thumb; rect = GetComponent<RectTransform>(); radius = reach; }
        public bool Begin(int id, Vector2 local)
        {
            if (PointerId != int.MinValue || session == null || !session.CanMove) return false;
            PointerId = id; Move(id, local); return true;
        }
        public void Move(int id, Vector2 local)
        {
            if (id != PointerId || session == null) return;
            var value = Vector2.ClampMagnitude(local / radius, 1); session.MoveInput = value; knob.anchoredPosition = value * radius;
        }
        public void End(int id) { if (id == PointerId) Release(); }
        public void Release() { PointerId = int.MinValue; if (session != null) session.MoveInput = Vector2.zero; if (knob != null) knob.anchoredPosition = Vector2.zero; }
        Vector2 Local(PointerEventData e) { Vector2 point; RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out point); return point - rect.rect.center; }
        public void OnPointerDown(PointerEventData e) { Begin(e.pointerId, Local(e)); }
        public void OnDrag(PointerEventData e) { Move(e.pointerId, Local(e)); }
        public void OnPointerUp(PointerEventData e) { End(e.pointerId); }
        public void OnCancel(BaseEventData e) { Release(); }
        void OnDisable() { Release(); }
    }

    public sealed class StareMobileLookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        StareMobileSession session; Canvas canvas; Vector2 last;
        public int PointerId { get; private set; } = int.MinValue;
        public void Initialize(StareMobileSession owner, Canvas overlay) { session = owner; canvas = overlay; }
        public bool Begin(int id, Vector2 position)
        {
            if (PointerId != int.MinValue || session == null || !session.Connected) return false;
            PointerId = id; last = position; return true;
        }
        public void Move(int id, Vector2 position)
        {
            if (id != PointerId || session == null) return;
            session.LookInput += (position - last) * (.14f / Mathf.Max(.01f, canvas.scaleFactor)); last = position;
        }
        public void End(int id) { if (id == PointerId) Release(); }
        public void Release() { PointerId = int.MinValue; if (session != null) session.LookInput = Vector2.zero; }
        public void OnPointerDown(PointerEventData e) { Begin(e.pointerId, e.position); }
        public void OnDrag(PointerEventData e) { Move(e.pointerId, e.position); }
        public void OnPointerUp(PointerEventData e) { End(e.pointerId); }
        public void OnCancel(BaseEventData e) { Release(); }
        void OnDisable() { Release(); }
    }
}
