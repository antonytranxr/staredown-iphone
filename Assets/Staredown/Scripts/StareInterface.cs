using System.Collections.Generic;
using UnityEngine;

namespace Staredown
{
 public sealed partial class StareVisuals
 {
  static readonly Color UIInk=new Color(.91f,.95f,.98f), UIMuted=new Color(.49f,.59f,.70f), UIAccent=new Color(.48f,.91f,.86f);
  static readonly Color UISurface=new Color(.037f,.052f,.081f), UIRaised=new Color(.054f,.073f,.105f);
  readonly Dictionary<Font,Material> interfaceFonts=new Dictionary<Font,Material>();
  bool uiFontHook;
  Transform lobbyContents,joinPanel,settingsPanel,homeActions,roomActions,openingInstructions,namePanel;
  TextMesh playerNameLabel,nameDraftLabel,nameLengthLabel;StareButton nameSaveButton;string nameDraft="";
  public bool NamePanelVisible=>namePanel!=null&&namePanel.gameObject.activeSelf;
  public string NameDraft=>nameDraft;
  TextMesh fpsSettingLabel;StareButton botFillButton;Transform danceChoices,skinsPanel;TextMesh progressionLabel,skinStatus;readonly List<StareButton> skinChoices=new List<StareButton>();
  Transform danceAnchor;
  Transform openingVideo,inputLesson,controllerLesson,handLesson;TextMesh lessonTitle,lessonBody,lessonHint,openingModeLabel,calibrateLabel;StareButton openingPlay;
  public bool DanceChoicesVisible=>danceChoices!=null&&danceChoices.gameObject.activeSelf;
  public bool DanceAttachedTo(Transform anchor)=>DanceChoicesVisible&&danceChoices.parent==anchor;
  public bool DanceFromWrist=>DanceChoicesVisible&&danceChoices.parent==wristDance.transform;
  public void HideAuxiliaryPanels(){if(danceChoices!=null)danceChoices.gameObject.SetActive(false);if(skinsPanel!=null)skinsPanel.gameObject.SetActive(false);if(NamePanelVisible)ShowNamePanel(false);}
  public bool SkinsVisible=>skinsPanel!=null&&skinsPanel.gameObject.activeSelf;
  public bool OpeningInstructionsVisible=>openingInstructions!=null&&openingInstructions.gameObject.activeSelf;
  public void SetFpsLabel(bool shown){if(fpsSettingLabel!=null)fpsSettingLabel.text=shown?"FPS counter: On":"FPS counter: Off";}
  TextMesh settingsStatus,settingsVoiceLabel,homeSubtitle,arenaChoiceLabel;StareButton exploreButton,brightArenaButton,moonlitArenaButton,sunstoneArenaButton;
  TextMesh micLabel,matchMicLabel,voiceLabel,matchMicrophoneStatus;
  public void SetMicrophoneStatus(string value)
  {if(matchMicrophoneStatus!=null){matchMicrophoneStatus.text=value;FitBoardLabel(matchMicrophoneStatus,2.34f,.020f*.42f);}}
  public void SetVoiceLabels(bool muted,bool enabled){if(micLabel!=null)micLabel.text=muted?"Mic: Muted":"Mic: On";if(matchMicLabel!=null)matchMicLabel.text=muted?"Mic: Muted":"Mic: On";if(voiceLabel!=null)voiceLabel.text=enabled?"Voice: On":"Voice: Off";if(settingsVoiceLabel!=null)settingsVoiceLabel.text=enabled?"Voice: On":"Voice: Off";}
  TextMesh profileName,inputModeLabel,roomLabel,joinNoticeLabel;StareButton hostRoomButton,joinRoomButton,joinCodeButton,inputModeButton;Transform demoBlink;
  readonly List<TextMesh> musicLabels=new List<TextMesh>();
  public void SetMusicLabel(bool muted,float volume=1)
  {string label=muted?"Music: Off":"Music "+Mathf.RoundToInt(volume*100)+"%";foreach(var text in musicLabels)if(text.text!=label)text.text=label;}
  void MusicControls(Transform parent,Vector3 position,float width)
  {
   float button=width*.23f,center=width*.49f;
   var down=UIButton("-","music-down",parent,position+Vector3.left*(width-button)*.5f,new Vector2(button,.29f));
   var label=UIButton("Music 100%","music",parent,position,new Vector2(center,.29f)).GetComponentInChildren<TextMesh>();label.characterSize=.032f*.42f;musicLabels.Add(label);
   UIButton("+","music-up",parent,position+Vector3.right*(width-button)*.5f,new Vector2(button,.29f));
  }
  StareButton startMatchButton,leaveMatchButton;
  StareButton wristDance,wristHelp,settingsDance,matchDance;
  Transform matchMenu,wristMenu;TextMesh matchMenuTitle,matchMenuBody;StareButton resumeButton;

  public Renderer Panel(string name,Transform parent,Vector3 position,Vector2 size,Color color,Color accent,float radius=.09f,bool primary=false)
  {
   var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=new Vector3(size.x,size.y,1);
   Destroy(g.GetComponent<Collider>());
   var renderer=g.GetComponent<Renderer>();var material=new Material(Resources.Load<Shader>("StarePanel")){name=name+" surface"};
   material.SetColor("_Color",color);material.SetColor("_Accent",accent);material.SetVector("_Size",new Vector4(size.x,size.y,Mathf.Min(radius,Mathf.Min(size.x,size.y)*.5f),.004f));material.SetFloat("_Primary",primary?1:0);
   renderer.sharedMaterial=material;materials["interface / "+g.GetInstanceID()]=material;return renderer;
  }
  TextMesh UILabel(string value,Transform parent,Vector3 position,float size,Color color,bool bold=false,TextAnchor anchor=TextAnchor.MiddleLeft)
  {
   var g=new GameObject(value);g.transform.SetParent(parent,false);g.transform.localPosition=position;
   var font=Resources.Load<Font>("UI/Lato-"+(bold?"SemiBold":"Regular"))??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
   if(!interfaceFonts.TryGetValue(font,out var material))
   {
    font.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;/!?-+•→✓",100);
    material=new Material(Resources.Load<Shader>("StareWorldText")){mainTexture=font.material.mainTexture};interfaceFonts[font]=material;
    if(!uiFontHook){Font.textureRebuilt+=RefreshInterfaceFonts;uiFontHook=true;}
   }
   var text=g.AddComponent<TextMesh>();text.font=font;text.fontSize=100;text.characterSize=size*.42f;text.anchor=anchor;text.alignment=anchor==TextAnchor.MiddleCenter?TextAlignment.Center:TextAlignment.Left;text.color=color;text.text=value;
   text.GetComponent<Renderer>().sharedMaterial=material;return text;
  }
  void RefreshInterfaceFonts(Font font){if(interfaceFonts.TryGetValue(font,out var material))material.mainTexture=font.material.mainTexture;}
  void DisposeInterface(){if(uiFontHook)Font.textureRebuilt-=RefreshInterfaceFonts;foreach(var material in interfaceFonts.Values)Destroy(material);}
  StareButton UIButton(string label,string action,Transform parent,Vector3 position,Vector2 size,bool primary=false,Color? accent=null)
  {
   var root=Group(label,parent,position);Color tint=accent??UIAccent;
   var surface=Panel(label,root,Vector3.zero,size,UIRaised,tint,.07f,primary);
   var hit=root.gameObject.AddComponent<BoxCollider>();hit.size=new Vector3(size.x,size.y,.045f);hit.center=Vector3.zero;
   var button=root.gameObject.AddComponent<StareButton>();button.action=action;button.Configure(surface);
   UILabel(label,root,new Vector3(0,0,-.014f),.040f,primary?new Color(.026f,.083f,.093f):UIInk,true,TextAnchor.MiddleCenter);
   return button;
  }
  void UIRule(Transform parent,Vector3 position,float width){Panel("Hairline divider",parent,position,new Vector2(width,.007f),new Color(.10f,.14f,.19f),UIMuted,.002f);}
  void UIEyeMark(Transform parent,Vector3 position,float scale,Color color)
  {
   var root=Group("Staredown eye mark",parent,position);
   var line=root.gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=.009f*scale;
   line.sharedMaterial=UIGlyphMaterial(color);
   for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.13f*scale,Mathf.Sin(a)*.067f*scale,0));}
   var pupil=Panel("Eye pupil",root,new Vector3(0,0,-.004f),Vector2.one*.066f*scale,color,color,.033f*scale,true);
  }
  Material UIGlyphMaterial(Color color)
  {
   string key="UI glyph "+color;if(materials.TryGetValue(key,out var found))return found;
   var m=new Material(Shader.Find("Sprites/Default")){color=color};materials[key]=m;return m;
  }
  void UIFrame(Transform root,Vector2 size,string section)
  {
   Panel("Soft outer edge",root,new Vector3(0,-.035f,.10f),size+new Vector2(.06f,.07f),new Color(.007f,.013f,.024f),UIAccent,.16f);
   Panel("Main floating panel",root,Vector3.zero,size,new Color(.019f,.028f,.048f),UIAccent,.14f);
   float left=-size.x*.5f+.24f,top=size.y*.5f-.24f;
   UIEyeMark(root,new Vector3(left+.11f,top,-.035f),.7f,UIAccent);
   UILabel("S T A R E D O W N",root,new Vector3(left+.30f,top,-.035f),.032f,UIInk,true);
   UILabel(section,root,new Vector3(size.x*.5f-.24f,top,-.035f),.027f,UIMuted,false,TextAnchor.MiddleRight);
  }

  void CreateMenu()
  {
   menu=Group("Staredown / Lobby",transform,new Vector3(0,2.05f,-6.8f));UIFrame(menu,new Vector2(3.9f,3.15f),"PLAY");
   lobbyContents=Group("Lobby controls",menu,Vector3.zero);
   homeActions=Group("Choose how to play",lobbyContents,Vector3.zero);
   UILabel("Don't blink.",homeActions,new Vector3(0,.85f,-.04f),.11f,UIInk,true,TextAnchor.MiddleCenter);
   homeSubtitle=UILabel("Eye-tracking staring contest. First to blink loses.",homeActions,new Vector3(0,.52f,-.04f),.034f,UIMuted,false,TextAnchor.MiddleCenter);
   UIButton("Practice with bots","demo",homeActions,new Vector3(0,.08f,-.065f),new Vector2(3.22f,.42f),true);
   hostRoomButton=UIButton("Create room","host",homeActions,new Vector3(0,-.43f,-.065f),new Vector2(3.22f,.42f));
   joinRoomButton=UIButton("Join room","join-panel",homeActions,new Vector3(0,-.94f,-.065f),new Vector2(3.22f,.42f));
   roomActions=Group("Room controls",lobbyContents,Vector3.zero);
   UILabel("YOUR ROOM CODE",roomActions,new Vector3(0,.89f,-.04f),.030f,UIMuted,true,TextAnchor.MiddleCenter);
   roomLabel=UILabel("",roomActions,new Vector3(0,.51f,-.04f),.14f,UIAccent,true,TextAnchor.MiddleCenter);
   botFillButton=UIButton("Players: Humans + bots","bot-fill",roomActions,new Vector3(0,.05f,-.065f),new Vector2(3.22f,.28f));
   startMatchButton=UIButton("Start match","start",roomActions,new Vector3(0,-.38f,-.065f),new Vector2(3.22f,.36f),true);
   exploreButton=UIButton("Explore arena","explore",roomActions,new Vector3(-.83f,-.78f,-.065f),new Vector2(1.55f,.37f));
   leaveMatchButton=UIButton("Leave room","leave",roomActions,new Vector3(.83f,-.78f,-.065f),new Vector2(1.55f,.37f));
   roomActions.gameObject.SetActive(false);
   profileName=UIButton("Frost","characters",lobbyContents,new Vector3(-1.10f,-1.38f,-.065f),new Vector2(1.02f,.29f)).GetComponentInChildren<TextMesh>();
   playerNameLabel=UIButton("Your name","name-panel",lobbyContents,new Vector3(0,-1.38f,-.065f),new Vector2(1.02f,.29f)).GetComponentInChildren<TextMesh>();playerNameLabel.characterSize=.029f*.42f;SetPlayerNameLabel(StarePlayerName.Saved);
   UIButton("Settings","settings",lobbyContents,new Vector3(1.10f,-1.38f,-.065f),new Vector2(1.02f,.29f));
   lobbyStatus=UILabel("Choose how to play",lobbyContents,new Vector3(0,-1.12f,-.05f),.021f,UIMuted,false,TextAnchor.MiddleCenter);
   BuildJoinPanel();BuildSettingsPanel();BuildNamePanel();BuildCharacterSelection();BuildMatchMenu();BuildOpeningInstructions();
  }
  void BuildOpeningInstructions()
  {
   openingInstructions=Group("How to play / Opening instructions",menu,new Vector3(0,0,-.16f));
   UILabel("How to play",openingInstructions,new Vector3(0,1.30f,-.04f),.038f,UIInk,true,TextAnchor.MiddleCenter);
   var screen=GameObject.CreatePrimitive(PrimitiveType.Quad);screen.name="Animated how-to-play video";screen.transform.SetParent(openingInstructions,false);screen.transform.localPosition=new Vector3(0,.15f,-.045f);screen.transform.localScale=new Vector3(3.30f,1.85625f,1);Destroy(screen.GetComponent<Collider>());
   openingVideo=screen.transform;screen.SetActive(false);
   screen.AddComponent<StareTutorialVideo>().Initialize(screen.GetComponent<Renderer>());
   inputLesson=Group("Device-specific blink lesson",openingInstructions,new Vector3(0,.15f,-.045f));
   Panel("Blink lesson card",inputLesson,Vector3.zero,new Vector2(3.30f,1.85625f),UISurface,UIAccent,.12f);
   lessonTitle=UILabel("Detecting your headset...",inputLesson,new Vector3(0,.66f,-.025f),.056f,UIInk,true,TextAnchor.MiddleCenter);
   controllerLesson=Group("Controller blink buttons",inputLesson,new Vector3(0,.24f,-.025f));
   for(int side=-1;side<=1;side+=2){Panel("Blink button",controllerLesson,new Vector3(side*.35f,0,0),new Vector2(.40f,.40f),UIAccent,UIAccent,.20f,true);UILabel(side<0?"X":"A",controllerLesson,new Vector3(side*.35f,0,-.02f),.10f,UISurface,true,TextAnchor.MiddleCenter);}
   handLesson=Group("Hand-only blink button",inputLesson,new Vector3(0,.24f,-.025f));
   Panel("Hand blink button",handLesson,Vector3.zero,new Vector2(1.03f,.38f),UIAccent,UIAccent,.07f,true);UILabel("BLINK",handLesson,new Vector3(0,0,-.02f),.059f,UISurface,true,TextAnchor.MiddleCenter);
   lessonBody=UILabel("",inputLesson,new Vector3(0,-.18f,-.025f),.040f,UIInk,false,TextAnchor.MiddleCenter);
   lessonHint=UILabel("",inputLesson,new Vector3(0,-.67f,-.025f),.029f,UIMuted,false,TextAnchor.MiddleCenter);
   openingModeLabel=UILabel("",openingInstructions,new Vector3(0,-.84f,-.065f),.026f,UIMuted,false,TextAnchor.MiddleCenter);
   openingPlay=UIButton("Play","intro-done",openingInstructions,new Vector3(0,-1.10f,-.065f),new Vector2(3.22f,.42f),true);
   UpdateInputInstructions(StareEyeCapability.Checking,false,false);
   ShowOpeningInstructions(true);
  }
  public void ShowOpeningInstructions(bool shown)
  {
   if(openingInstructions==null)return;openingInstructions.gameObject.SetActive(shown);
   if(namePanel!=null)namePanel.gameObject.SetActive(false);
   foreach(var label in menu.GetComponentsInChildren<TextMesh>(true))if(label.transform.parent==menu)label.gameObject.SetActive(!shown);var mark=menu.Find("Staredown eye mark");if(mark!=null)mark.gameObject.SetActive(!shown);
   joinPanel.gameObject.SetActive(false);foreach(var button in settingsPanel.GetComponentsInChildren<StareButton>())if(button.action=="skins"||button.action=="how-to-play"||button.action=="settings-back")button.GetComponentInChildren<TextMesh>().characterSize=.029f*.42f;
   settingsPanel.gameObject.SetActive(false);lobbyContents.gameObject.SetActive(!shown);
  }
  void BuildJoinPanel()
  {
   joinPanel=Group("Join a match",menu,new Vector3(0,0,-.15f));
   UILabel("Join your friends.",joinPanel,new Vector3(0,.93f,-.04f),.070f,UIInk,true,TextAnchor.MiddleCenter);
   UILabel("Enter their 4-digit room code.",joinPanel,new Vector3(0,.65f,-.04f),.033f,UIMuted,false,TextAnchor.MiddleCenter);
   Panel("Room code field",joinPanel,new Vector3(0,.30f,-.04f),new Vector2(2.40f,.40f),UISurface,UIAccent,.06f);
   ipText=UILabel("_ _ _ _",joinPanel,new Vector3(0,.30f,-.07f),.073f,UIInk,true,TextAnchor.MiddleCenter);
   const string keys="123456789";
   for(int i=0;i<keys.Length;i++)
   {
    var key=UIButton(keys[i].ToString(),"key:"+keys[i],joinPanel,new Vector3(-.94f+i%3*.94f,-.14f-i/3*.29f,-.06f),new Vector2(.82f,.27f));
    key.GetComponentInChildren<TextMesh>().characterSize=.052f*.42f;
   }
   UIButton("Clear","key:CLEAR",joinPanel,new Vector3(-.94f,-1.01f,-.06f),new Vector2(.82f,.27f));
   UIButton("0","key:0",joinPanel,new Vector3(0,-1.01f,-.06f),new Vector2(.82f,.27f));
   UIButton("Delete","key:DEL",joinPanel,new Vector3(.94f,-1.01f,-.06f),new Vector2(.82f,.27f));
   joinNoticeLabel=UILabel("",joinPanel,new Vector3(0,1.10f,-.06f),.018f,UIMuted,false,TextAnchor.MiddleCenter);
   UIButton("Back","join-back",joinPanel,new Vector3(-.83f,-1.38f,-.07f),new Vector2(1.55f,.29f));
   joinCodeButton=UIButton("Join","join",joinPanel,new Vector3(.83f,-1.38f,-.07f),new Vector2(1.55f,.29f),true);
   joinPanel.gameObject.SetActive(false);
  }
  void BuildNamePanel()
  {
   namePanel=Group("Choose your player name",menu,new Vector3(0,0,-.15f));
   UILabel("Your name.",namePanel,new Vector3(0,.99f,-.04f),.070f,UIInk,true,TextAnchor.MiddleCenter);
   UILabel("Shown above your avatar to everyone in the room.",namePanel,new Vector3(0,.73f,-.04f),.028f,UIMuted,false,TextAnchor.MiddleCenter);
   Panel("Player name field",namePanel,new Vector3(0,.44f,-.04f),new Vector2(3.22f,.34f),UISurface,UIAccent,.06f);
   nameDraftLabel=UILabel("",namePanel,new Vector3(0,.44f,-.07f),.070f,UIInk,true,TextAnchor.MiddleCenter);
   nameLengthLabel=UILabel("",namePanel,new Vector3(1.58f,.20f,-.07f),.021f,UIMuted,false,TextAnchor.MiddleRight);
   string[] rows={"1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"};
   for(int row=0;row<rows.Length;row++)
   {
    float start=-(rows[row].Length-1)*.16f;
    for(int column=0;column<rows[row].Length;column++)
    {
     string key=rows[row][column].ToString();
     UIButton(key,"name-key:"+key,namePanel,new Vector3(start+column*.32f,.08f-row*.26f,-.06f),new Vector2(.285f,.225f));
    }
   }
   foreach(var choice in new[]{new[]{"Clear","CLEAR"},new[]{"Space","SPACE"},new[]{"Delete","DEL"}})
   {
    int index=choice[1]=="CLEAR"?-1:choice[1]=="SPACE"?0:1;
    var key=UIButton(choice[0],"name-key:"+choice[1],namePanel,new Vector3(index*1.1f,-1.00f,-.06f),new Vector2(1.02f,.23f));key.GetComponentInChildren<TextMesh>().characterSize=.031f*.42f;
   }
   UIButton("Cancel","name-back",namePanel,new Vector3(-.83f,-1.38f,-.07f),new Vector2(1.55f,.29f));
   nameSaveButton=UIButton("Save name","name-save",namePanel,new Vector3(.83f,-1.38f,-.07f),new Vector2(1.55f,.29f),true);
   namePanel.gameObject.SetActive(false);
  }
  public void SetPlayerNameLabel(string value){if(playerNameLabel!=null)playerNameLabel.text="Name: "+StarePlayerName.Clean(value);}
  public void ShowNamePanel(bool shown,string initial="")
  {
   if(namePanel==null)return;
   if(shown){ShowOpeningInstructions(false);nameDraft=StarePlayerName.Clean(initial);UpdateNameDraft();}
   namePanel.gameObject.SetActive(shown);joinPanel.gameObject.SetActive(false);settingsPanel.gameObject.SetActive(false);lobbyContents.gameObject.SetActive(!shown&&!OpeningInstructionsVisible);
  }
  public void EditPlayerName(string key)
  {
   if(!NamePanelVisible)return;
   if(key=="CLEAR")nameDraft="";
   else if(key=="DEL"){if(nameDraft.Length>0)nameDraft=nameDraft.Substring(0,nameDraft.Length-1);}
   else if(key=="SPACE"){if(nameDraft.Length>0&&nameDraft.Length<StarePlayerName.MaxLength&&!nameDraft.EndsWith(" "))nameDraft+=" ";}
   else if(key.Length==1&&StarePlayerName.InputCharacter(key[0])&&nameDraft.Length<StarePlayerName.MaxLength)nameDraft+=key;
   UpdateNameDraft();
  }
  void UpdateNameDraft()
  {
   nameDraftLabel.text=nameDraft.Length>0?nameDraft:"Enter your name";nameDraftLabel.color=nameDraft.Length>0?UIInk:UIMuted;
   nameLengthLabel.text=nameDraft.Length+" / "+StarePlayerName.MaxLength;
   nameSaveButton.Interactable=StarePlayerName.Clean(nameDraft).Length>0;
  }
  void BuildSettingsPanel()
  {
   settingsPanel=Group("Settings",menu,new Vector3(0,0,-.15f));
   UILabel("Make it yours.",settingsPanel,new Vector3(0,.99f,-.04f),.070f,UIInk,true,TextAnchor.MiddleCenter);
   arenaChoiceLabel=UILabel("ARENA / CHOOSE YOUR SETTING",settingsPanel,new Vector3(0,.67f,-.04f),.025f,UIMuted,true,TextAnchor.MiddleCenter);
   brightArenaButton=UIButton("Bright","arena:0",settingsPanel,new Vector3(-1.10f,.41f,-.065f),new Vector2(1.02f,.32f),false,new Color(1,.76f,.25f));
   moonlitArenaButton=UIButton("Moonlit","arena:1",settingsPanel,new Vector3(0,.41f,-.065f),new Vector2(1.02f,.32f),false,new Color(.65f,.58f,1));
   sunstoneArenaButton=UIButton("Sunstone","arena:2",settingsPanel,new Vector3(1.10f,.41f,-.065f),new Vector2(1.02f,.32f),false,new Color(1,.62f,.34f));
   inputModeButton=UIButton("Eye tracking","input-mode",settingsPanel,new Vector3(-.83f,-.03f,-.065f),new Vector2(1.55f,.32f));inputModeLabel=inputModeButton.GetComponentInChildren<TextMesh>();
   calibrateLabel=UIButton("Calibrate eyes","calibrate",settingsPanel,new Vector3(.83f,-.03f,-.065f),new Vector2(1.55f,.32f)).GetComponentInChildren<TextMesh>();
   micLabel=UIButton("Mic: On","mic",settingsPanel,new Vector3(-.83f,-.46f,-.065f),new Vector2(1.55f,.32f)).GetComponentInChildren<TextMesh>();
   settingsVoiceLabel=UIButton("Voice: On","voice",settingsPanel,new Vector3(.83f,-.46f,-.065f),new Vector2(1.55f,.32f)).GetComponentInChildren<TextMesh>();
   MusicControls(settingsPanel,new Vector3(-.83f,-.89f,-.065f),1.55f);
   fpsSettingLabel=UIButton("FPS counter: Off","fps-toggle",settingsPanel,new Vector3(.83f,-.89f,-.065f),new Vector2(1.55f,.32f)).GetComponentInChildren<TextMesh>();
   settingsStatus=UILabel("",settingsPanel,new Vector3(0,-1.13f,-.04f),.014f,UIMuted,false,TextAnchor.MiddleCenter);
   UIButton("Skins","skins",settingsPanel,new Vector3(-1.24f,-1.38f,-.065f),new Vector2(.76f,.29f));
   UIButton("How to play","how-to-play",settingsPanel,new Vector3(-.41f,-1.38f,-.065f),new Vector2(.76f,.29f));
   UIButton("Repair voice","voice-reconnect",settingsPanel,new Vector3(.41f,-1.38f,-.065f),new Vector2(.76f,.29f));
   UIButton("Done","settings-back",settingsPanel,new Vector3(1.24f,-1.38f,-.065f),new Vector2(.76f,.29f),true);
   settingsPanel.gameObject.SetActive(false);
  }
  public void UpdateArenaSelection(int theme,bool canChoose,bool guest)
  {
   if(brightArenaButton==null)return;
   brightArenaButton.SetSelected(theme==0);moonlitArenaButton.SetSelected(theme==1);sunstoneArenaButton.SetSelected(theme==2);
   brightArenaButton.Interactable=moonlitArenaButton.Interactable=sunstoneArenaButton.Interactable=canChoose;
   arenaChoiceLabel.text=guest?"ARENA / CHOSEN BY THE HOST":canChoose?"ARENA / CHOOSE YOUR SETTING":"ARENA / LOCKED DURING MATCH";
  }
  public void UpdateDanceControls(bool allowed,bool dancing)
  {if(settingsDance!=null){settingsDance.Interactable=allowed;settingsDance.SetSelected(dancing);}if(matchDance!=null){matchDance.Interactable=allowed;matchDance.SetSelected(dancing);}}
  public void SetSettingsStatus(string message){if(settingsStatus!=null)settingsStatus.text=message;}
  public void SetJoinStatus(string message){if(joinNoticeLabel!=null)joinNoticeLabel.text=message;}
  public void ShowJoinPanel(bool shown){if(joinPanel==null)return;if(namePanel!=null)namePanel.gameObject.SetActive(false);joinPanel.gameObject.SetActive(shown);settingsPanel.gameObject.SetActive(false);lobbyContents.gameObject.SetActive(!shown&&!OpeningInstructionsVisible);if(shown)SetJoinStatus("");}
  public void ShowSettingsPanel(bool shown){if(settingsPanel==null)return;if(namePanel!=null)namePanel.gameObject.SetActive(false);settingsPanel.gameObject.SetActive(shown);joinPanel.gameObject.SetActive(false);lobbyContents.gameObject.SetActive(!shown&&!OpeningInstructionsVisible);}
  public void UpdateLobbyAvailability(bool host,bool connected,ArenaState state)
  {
   bool ready=host&&state.phase==(int)MatchPhase.Lobby&&state.fighters.Length>=2;
   foreach(var fighter in state.fighters)if(!fighter.ready||!fighter.valid)ready=false;
   if(startMatchButton!=null)startMatchButton.Interactable=ready;
   if(leaveMatchButton!=null)leaveMatchButton.Interactable=connected;
  }
  public void UpdateRoomControls(bool busy,bool connected,string code,bool simulated,string enteredCode,bool host,ArenaState state)
  {
   hostRoomButton.Interactable=joinRoomButton.Interactable=!busy&&!connected;
   if(busy){joinRoomButton.Interactable=true;joinRoomButton.action="leave";}else joinRoomButton.action="join-panel";
   joinRoomButton.GetComponentInChildren<TextMesh>().text=busy?"Cancel":"Join room";
   joinCodeButton.Interactable=!busy&&StareRelayNetwork.ValidCode(enteredCode);
   homeActions.gameObject.SetActive(!connected);roomActions.gameObject.SetActive(connected);
   startMatchButton.gameObject.SetActive(host);
   exploreButton.Interactable=connected&&!busy;
   inputModeLabel.text=simulated?"Button demo":"Eye tracking";
   roomLabel.text=string.IsNullOrEmpty(code)?"PRACTICE":code;botFillButton.Interactable=host&&!busy&&!string.IsNullOrEmpty(code);botFillButton.GetComponentInChildren<TextMesh>().text=string.IsNullOrEmpty(code)?"Practice: you vs bots":state.botFill?"Players: Humans + bots":"Players: Humans only";
   bool showStatus=connected||busy||lobbyStatus.text!="Choose how to play";
   lobbyStatus.gameObject.SetActive(showStatus);homeSubtitle.gameObject.SetActive(connected||!showStatus);
   lobbyStatus.transform.localPosition=new Vector3(0,connected?-1.12f:.52f,-.05f);
   exploreButton.SetLayoutPosition(new Vector3(-.83f,host?-.78f:-.29f,-.065f));
   leaveMatchButton.SetLayoutPosition(new Vector3(.83f,host?-.78f:-.29f,-.065f));
   inputModeButton.Interactable=state.phase==(int)MatchPhase.Lobby;
  }
  public void UpdateDemoBlink(bool shown,Transform head)
  {
   if(demoBlink==null)
   {
    demoBlink=Group("Button demo blink",transform,Vector3.zero);
    UIButton("BLINK","blink",demoBlink,Vector3.zero,new Vector2(.24f,.09f),true);
    foreach(var label in demoBlink.GetComponentsInChildren<TextMesh>())label.characterSize=.016f*.42f;
    UILabel("DEMO / A OR X",demoBlink,new Vector3(0,-.075f,-.02f),.011f,UIMuted,false,TextAnchor.MiddleCenter);
   }
   demoBlink.gameObject.SetActive(shown);if(!shown||head==null)return;
   var direction=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
   if(direction.sqrMagnitude<.1f)direction=Vector3.forward;
   demoBlink.position=head.position+direction*1.1f-Vector3.up*.30f;
   demoBlink.rotation=Quaternion.LookRotation(direction,Vector3.up);
  }
  public void UpdateInputInstructions(StareEyeCapability capability,bool simulated,bool handsOnly)
  {
   bool checking=capability==StareEyeCapability.Checking,buttons=!checking&&simulated;
   if(openingVideo==null)return;
   openingVideo.gameObject.SetActive(!checking&&!buttons);inputLesson.gameObject.SetActive(checking||buttons);
   controllerLesson.gameObject.SetActive(buttons&&!handsOnly);handLesson.gameObject.SetActive(buttons&&handsOnly);
   lessonTitle.text=checking?"Detecting your headset...":"Button blink demo";
   lessonBody.text=checking?"Your controls will be set automatically.":handsOnly?"Select BLINK to blink.\nFirst to blink loses.":"Press A or X to blink.\nFirst to blink loses.";
   lessonHint.text=checking?"":handsOnly?"Keep BLINK released to keep your eyes open.":"Keep A/X released. Using hands? Select BLINK.";
   openingModeLabel.text=checking?"":buttons?"Real blinks are not tracked in this demo.":"Real eye tracking / Keep your eyes open.";
   openingPlay.Interactable=!checking;
   openingPlay.GetComponentInChildren<TextMesh>().text=buttons?"Continue":"Play";
   if(homeSubtitle!=null)homeSubtitle.text=checking?"Detecting your headset...":buttons?"Button demo. Press A/X or BLINK to blink.":"Keep your eyes open. First to blink loses.";
   if(inputModeButton!=null){inputModeButton.Interactable&=capability==StareEyeCapability.EyeTracking;inputModeLabel.text=checking?"Detecting...":buttons?"Button demo":"Eye tracking";}
   if(calibrateLabel!=null)calibrateLabel.text=buttons?"Get ready":"Calibrate eyes";
   if(demoBlink!=null){var hint=demoBlink.GetComponentsInChildren<TextMesh>(true);foreach(var label in hint)if(label.gameObject.name=="DEMO / A OR X")label.text=handsOnly?"DEMO / SELECT BLINK":"DEMO / A OR X";}
  }
  void BuildMatchMenu()
  {
   matchMenu=Group("Staredown / Match menu",transform,Vector3.zero);matchMenu.localScale=Vector3.one*.65f;UIFrame(matchMenu,new Vector2(2.85f,2.85f),"MATCH");
   matchMicLabel=UIButton("Mic: On","mic",matchMenu,new Vector3(-.61f,.85f,-.06f),new Vector2(1.12f,.25f)).GetComponentInChildren<TextMesh>();
   voiceLabel=UIButton("Voice: On","voice",matchMenu,new Vector3(.61f,.85f,-.06f),new Vector2(1.12f,.25f)).GetComponentInChildren<TextMesh>();
   matchMicrophoneStatus=UILabel("MIC / JOIN AN ONLINE ROOM",matchMenu,new Vector3(0,.66f,-.045f),.020f,UIAccent,false,TextAnchor.MiddleCenter);
   matchMenuTitle=UILabel("Take a breath.",matchMenu,new Vector3(-1.16f,.52f,-.04f),.063f,UIInk,true);
   matchMenuBody=UILabel("The round continues while this menu is open.",matchMenu,new Vector3(-1.16f,.24f,-.04f),.027f,UIMuted);
   MusicControls(matchMenu,new Vector3(0,-.14f,-.06f),2.34f);
   resumeButton=UIButton("Resume","resume",matchMenu,new Vector3(-.61f,-.59f,-.06f),new Vector2(1.12f,.33f));
   matchDance=UIButton("Dance","dance",matchMenu,new Vector3(.61f,-.59f,-.06f),new Vector2(1.12f,.33f));
   UIButton("Leave room","leave",matchMenu,new Vector3(0,-1.05f,-.06f),new Vector2(2.34f,.33f),true);
   matchMenu.gameObject.SetActive(false);
   wristMenu=Group("Hand menu shortcut",transform,Vector3.zero);
   var wrist=UIButton("Menu","match-menu",wristMenu,Vector3.zero,new Vector2(.20f,.074f));
   var label=wrist.GetComponentInChildren<TextMesh>();label.characterSize=.012f*.42f;
   wristDance=UIButton("Dance","dance",wristMenu,new Vector3(.23f,0,0),new Vector2(.20f,.074f));
   wristHelp=UIButton("Help","throw-help",wristMenu,new Vector3(.46f,0,0),new Vector2(.20f,.074f));
   foreach(var t in wristMenu.GetComponentsInChildren<TextMesh>())t.characterSize=.012f*.42f;
   wristMenu.gameObject.SetActive(false);
  }
  public void ShowSkins(bool shown,StareProgression progression)
  {
   if(skinsPanel==null)
   {
    skinsPanel=Group("Skin collection",menu,new Vector3(0,0,-.20f));progressionLabel=UILabel("",skinsPanel,new Vector3(0,.92f,-.04f),.048f,UIAccent,true,TextAnchor.MiddleCenter);
    UILabel("Win rounds. Level up. Unlock your look.",skinsPanel,new Vector3(0,.55f,-.04f),.029f,UIMuted,false,TextAnchor.MiddleCenter);
    for(int i=0;i<4;i++)skinChoices.Add(UIButton("","skin:"+i,skinsPanel,new Vector3(0,.19f-i*.33f,-.065f),new Vector2(3.22f,.29f)));
    skinStatus=UILabel("",skinsPanel,new Vector3(0,-1.05f,-.04f),.026f,UIMuted,false,TextAnchor.MiddleCenter);UIButton("Back","skins-back",skinsPanel,new Vector3(0,-1.38f,-.065f),new Vector2(3.22f,.29f),true);
   }
   skinsPanel.gameObject.SetActive(shown);settingsPanel.gameObject.SetActive(!shown);lobbyContents.gameObject.SetActive(false);UpdateSkinChoices(progression);
  }
  public void UpdateSkinChoices(StareProgression progression)
  {
   if(skinsPanel==null||!skinsPanel.gameObject.activeSelf||progression==null)return;progressionLabel.text="LEVEL "+progression.Level+" / "+progression.Coins+" COINS\n"+progression.LevelXP+" / "+StareProgression.RequiredXP(progression.Level)+" XP";skinStatus.text=progression.Status;
   for(int i=0;i<skinChoices.Count;i++){skinChoices[i].Interactable=progression.Available(i);skinChoices[i].GetComponentInChildren<TextMesh>().text=progression.Label(i);skinChoices[i].GetComponentInChildren<TextMesh>().characterSize=.026f*.42f;skinChoices[i].SetSelected(progression.Equipped==i);}
  }
  public void ShowDanceChoices(Transform head,bool shown,Transform anchor=null)
  {
   if(!shown){if(danceChoices!=null)danceChoices.gameObject.SetActive(false);return;}
   if(anchor==null)anchor=matchDance.transform;
   if(danceChoices==null)
   {
    danceChoices=Group("Quick dance picker",transform,Vector3.zero);
    Panel("Dance picker surface",danceChoices,new Vector3(0,0,.008f),new Vector2(.32f,.36f),UISurface,UIAccent,.018f);
    UILabel("DANCE",danceChoices,new Vector3(-.135f,.151f,-.015f),.008f,UIAccent,true);
    var close=UIButton("×","dance-back",danceChoices,new Vector3(.132f,.15f,-.025f),new Vector2(.041f,.038f));
    close.GetComponentInChildren<TextMesh>().characterSize=.0038f;
    string[] names={"Hip hop","Charleston","Lindy hop","Fancy footwork","Stop dancing"};
    for(int i=0;i<names.Length;i++)
    {
     var button=UIButton(names[i],"dance:"+(i==4?-1:i),danceChoices,new Vector3(0,.098f-i*.054f,-.025f),new Vector2(.282f,.046f));
     button.GetComponentInChildren<TextMesh>().characterSize=.0027f;
    }
   }
   // Retain a constant physical size even on the scaled match menu.
   danceChoices.SetParent(anchor,false);MarkUiForCapture(danceChoices);Vector3 scale=anchor.lossyScale;
   danceChoices.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
   danceAnchor=anchor;PositionDanceChoices(head);
   danceChoices.gameObject.SetActive(true);
  }
  public static void MarkUiForCapture(Transform root){if(root!=null)StareFloorReflection.Mark(root,26);}
  public void MarkMenusForCapture(){foreach(var root in new[]{menu,characterMenu,matchMenu,wristMenu,openingInstructions,demoBlink})MarkUiForCapture(root);}
  public void PositionDanceChoices(Transform head)
  {
   if(danceChoices==null||danceAnchor==null||head==null)return;
   var box=danceAnchor.GetComponent<BoxCollider>();float top=box==null?.04f:box.size.y*.5f*Mathf.Abs(danceAnchor.lossyScale.y);
   var toward=Vector3.ProjectOnPlane(danceAnchor.position-head.position,Vector3.up);
   if(toward.sqrMagnitude<.001f)toward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
   var normal=toward.normalized;var point=danceAnchor.position+Vector3.up*(top+.195f)-normal*.065f;
   point.y=Mathf.Max(point.y,head.position.y-.35f);
   danceChoices.SetPositionAndRotation(point,Quaternion.LookRotation(normal,Vector3.up));
  }
  public void UpdateMatchMenu(bool shown,ArenaState state,int localId,Transform head)
  {
   if(matchMenu==null||head==null)return;
   if(shown&&!matchMenu.gameObject.activeSelf)
   {
    matchMenu.rotation=Quaternion.Euler(0,head.eulerAngles.y,0);
    matchMenu.position=head.position+matchMenu.forward*2.2f;
   }
   matchMenu.gameObject.SetActive(shown);if(!shown)return;
   bool complete=state.phase==(int)MatchPhase.Champion;
   matchMenuTitle.text=complete?(state.winner==localId?"You held your nerve.":"A champion is crowned."):"Take a breath.";
   matchMenuTitle.characterSize=(complete?.047f:.063f)*.42f;
   matchMenuBody.text=complete?"Ready for another round? Choose a new contender.":"The round continues while this menu is open.";
   matchMenuBody.characterSize=(complete?.024f:.027f)*.42f;
   resumeButton.gameObject.SetActive(!complete);
  }
  public void UpdateWristMenu(bool shown,bool canDance,bool dancing,bool canThrow,Vector3 handPosition,Transform head)
  {
   if(wristMenu==null)return;
   wristMenu.gameObject.SetActive(shown&&head!=null);if(!wristMenu.gameObject.activeSelf)return;
   wristDance.gameObject.SetActive(canDance);wristDance.SetSelected(dancing);wristHelp.gameObject.SetActive(canThrow);
   wristMenu.position=handPosition+Vector3.up*.10f-head.right*.07f;
   wristMenu.rotation=Quaternion.LookRotation(wristMenu.position-head.position,Vector3.up);
   if(DanceChoicesVisible)PositionDanceChoices(head);
  }
 }
}

namespace Staredown
{
 public static class StareShortcutMenu
 {
  public static bool Visible(bool menuOpen,bool drawing,bool canThrow,bool handTracked,bool leftController,bool rightController,bool picker)
  {return !drawing&&(picker||!menuOpen&&(leftController||rightController||handTracked&&!canThrow));}
 }
}
