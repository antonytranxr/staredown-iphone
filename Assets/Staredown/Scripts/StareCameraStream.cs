using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Staredown
{
 public static class StareCameraFrameValidation
 {
  public const int MaxJpeg=5000,MaxEncoded=6668,MaxDimension=256,MaxJson=8192;
  public const float FramesPerSecond=6,FreshSeconds=1.5f;
  public static bool Jpeg(byte[] bytes,out int width,out int height)
  {
   width=height=0;
   if(bytes==null||bytes.Length<14||bytes.Length>MaxJpeg||bytes[0]!=255||bytes[1]!=216||bytes[bytes.Length-2]!=255||bytes[bytes.Length-1]!=217)return false;
   int offset=2;
   while(offset+4<=bytes.Length)
   {
    if(bytes[offset++]!=255)return false;
    while(offset<bytes.Length&&bytes[offset]==255)offset++;
    if(offset>=bytes.Length)return false;int marker=bytes[offset++];
    if(marker==216||marker==1||marker>=208&&marker<=215)continue;
    if(marker==217||marker==218||marker==0)return false;
    if(offset+2>bytes.Length)return false;int length=(bytes[offset]<<8)|bytes[offset+1];
    if(length<2||offset+length>bytes.Length)return false;
    if(marker==192||marker==193||marker==194)
    {
     if(length<8||bytes[offset+2]!=8)return false;
     height=(bytes[offset+3]<<8)|bytes[offset+4];width=(bytes[offset+5]<<8)|bytes[offset+6];int components=bytes[offset+7];
     return width>=8&&height>=8&&width<=MaxDimension&&height<=MaxDimension&&(components==1||components==3)&&length>=8+components*3;
    }
    offset+=length;
   }
   return false;
  }
  public static bool Decode(WireMessage message,out byte[] jpeg)
  {
   jpeg=null;
   if(message==null||message.kind!="cameraFrame"||message.id<0||message.cameraSequence<=0||string.IsNullOrEmpty(message.cameraFrame)||message.cameraFrame.Length>MaxEncoded)return false;
   // JSON is bounded as well as the binary payload, including unexpected fields.
   if(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(message))>MaxJson)return false;
   try{jpeg=Convert.FromBase64String(message.cameraFrame);}catch(FormatException){return false;}
   int width,height;if(!Jpeg(jpeg,out width,out height)){jpeg=null;return false;}return true;
  }
 }

 // Small live face previews travel independently of gameplay. The host grants
 // the camera profile and stamps the connection's own identity on every relay.
 public sealed class StareCameraStream : MonoBehaviour
 {
  sealed class Feed
  {public Texture2D texture;public float received=-10;}
  sealed class Budget {public int sequence;public float tokens=2,tokenAt=-10;}
  readonly Dictionary<int,Feed> feeds=new Dictionary<int,Feed>();
  readonly Dictionary<int,Budget> budgets=new Dictionary<int,Budget>();
  readonly Dictionary<int,Fighter> roster=new Dictionary<int,Fighter>();
  readonly HashSet<int> profiles=new HashSet<int>();
  readonly HashSet<int> cameraCapabilities=new HashSet<int>();
  readonly HashSet<int> disabledProfiles=new HashSet<int>();
  readonly HashSet<StareCameraHead> attached=new HashSet<StareCameraHead>();
  int publishSequence;float publishAt=-10;StareVisuals lastVisuals;
  public int AcceptedFrames {get;private set;}
  public int FeedCount=>feeds.Count;

  public bool Handle(WireMessage message,int sender,Tournament authority,IStareTransport transport,int localId)
  {
   if(message==null||message.kind!="cameraProfile"&&message.kind!="cameraFrame")return false;
   bool host=authority!=null;
   if(message.kind=="cameraProfile")
   {
    if(host)
    {
     var fighter=authority.Find(sender);
     if(fighter==null||fighter.bot||fighter.disconnected||message.id!=sender||message.camera&&authority.Phase!=MatchPhase.Lobby&&!cameraCapabilities.Contains(sender))return true;
     if(message.camera&&authority.Phase==MatchPhase.Lobby)cameraCapabilities.Add(sender);
     fighter.phoneCamera=message.camera;
     SetProfile(sender,message.camera);
     transport?.Broadcast(new WireMessage{kind="cameraProfile",id=sender,camera=message.camera});
    }
    else if(sender==0&&message.id>=0&&(profiles.Count<Tournament.Capacity||profiles.Contains(message.id)))SetProfile(message.id,message.camera);
    return true;
   }
   Fighter owner=null;
   if(host)
   {
    owner=authority.Find(sender);
    if(owner==null||owner.bot||owner.disconnected||!owner.phoneCamera||message.id!=sender)return true;
   }
   else
   {
    if(sender!=0||!profiles.Contains(message.id)||!roster.TryGetValue(message.id,out owner)||owner.bot||owner.disconnected||!owner.phoneCamera)return true;
   }
   byte[] bytes;if(!StareCameraFrameValidation.Decode(message,out bytes))return true;
   if(!Accept(message.id,message.cameraSequence,bytes,host))return true;
   if(host)transport?.Broadcast(new WireMessage{kind="cameraFrame",id=sender,cameraSequence=message.cameraSequence,cameraFrame=message.cameraFrame});
   return true;
  }

  void SetProfile(int id,bool enabled)
  {
   if(enabled){disabledProfiles.Remove(id);if(profiles.Count<Tournament.Capacity)profiles.Add(id);}
   else {disabledProfiles.Add(id);profiles.Remove(id);Remove(id);}
  }
  bool Accept(int id,int sequence,byte[] bytes,bool rateLimit)
  {
   Budget budget;
   if(!budgets.TryGetValue(id,out budget))
   {if(budgets.Count>=Tournament.Capacity)return false;budget=new Budget();budgets.Add(id,budget);}
   if(sequence<=budget.sequence)return false;
   float now=Time.unscaledTime;
   if(rateLimit)
   {
    float elapsed=budget.tokenAt<0?0:Mathf.Max(0,now-budget.tokenAt);
    budget.tokens=Mathf.Min(2,budget.tokens+elapsed*StareCameraFrameValidation.FramesPerSecond);budget.tokenAt=now;
    if(budget.tokens<1)return false;budget.tokens-=1;
   }
   Feed feed;
   if(!feeds.TryGetValue(id,out feed))
   {if(feeds.Count>=Tournament.Capacity)return false;feed=new Feed();feeds.Add(id,feed);}
   if(feed.texture==null)feed.texture=new Texture2D(2,2,TextureFormat.RGB24,false){name="Contender "+id+" live face",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
   try{if(!ImageConversion.LoadImage(feed.texture,bytes,false))return false;}catch(Exception){return false;}
   budget.sequence=sequence;feed.received=now;AcceptedFrames++;return true;
  }

  public void Publish(byte[] jpeg,int localId,Tournament authority,IStareTransport transport)
  {
   Fighter fighter=null;
   if(localId<0||Time.unscaledTime<publishAt)return;
   if(authority!=null)fighter=authority.Find(localId);else roster.TryGetValue(localId,out fighter);
   if(fighter==null||fighter.bot||fighter.disconnected||!fighter.phoneCamera)return;
   int width,height;if(!StareCameraFrameValidation.Jpeg(jpeg,out width,out height))return;
   if(publishSequence==int.MaxValue)return;
   var message=new WireMessage{kind="cameraFrame",id=localId,cameraSequence=++publishSequence,cameraFrame=Convert.ToBase64String(jpeg)};
   if(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(message))>StareCameraFrameValidation.MaxJson)return;
   publishAt=Time.unscaledTime+1/StareCameraFrameValidation.FramesPerSecond;
   if(!Accept(localId,message.cameraSequence,jpeg,false))return;
   if(authority!=null)transport?.Broadcast(message);else transport?.Send(0,message);
  }

  public void Sync(StareVisuals visuals,ArenaState state,int localId)
  {
   lastVisuals=visuals;roster.Clear();
   if(state==null)return;
   foreach(var fighter in state.fighters)
   {
    roster[fighter.id]=fighter;
    if(fighter.phoneCamera&&!fighter.bot&&!disabledProfiles.Contains(fighter.id))profiles.Add(fighter.id);else profiles.Remove(fighter.id);
    if(visuals==null)continue;var avatar=visuals.AvatarFor(fighter.id);if(avatar==null)continue;
    Feed feed;bool live=profiles.Contains(fighter.id)&&fighter.phoneCamera&&!fighter.bot&&!fighter.disconnected&&feeds.TryGetValue(fighter.id,out feed)&&feed.texture!=null&&Time.unscaledTime-feed.received<=StareCameraFrameValidation.FreshSeconds;
    if(live)
    {
     if(avatar.cameraHead==null){avatar.cameraHead=avatar.root.gameObject.AddComponent<StareCameraHead>();avatar.cameraHead.Bind(avatar);}
     attached.Add(avatar.cameraHead);
     avatar.cameraHead.Present(feeds[fighter.id].texture);
    }
    else if(avatar.cameraHead!=null)avatar.cameraHead.Present(null);
   }
   foreach(int id in feeds.Keys.ToArray())
   {
    Fighter fighter;
    if(!roster.TryGetValue(id,out fighter)||fighter.bot||fighter.disconnected||!fighter.phoneCamera){profiles.Remove(id);Remove(id);}
    else if(Time.unscaledTime-feeds[id].received>StareCameraFrameValidation.FreshSeconds&&feeds[id].texture!=null)
    {Destroy(feeds[id].texture);feeds[id].texture=null;}
   }
   foreach(int id in profiles.ToArray())if(!roster.ContainsKey(id))profiles.Remove(id);
   foreach(int id in cameraCapabilities.ToArray())
   {Fighter fighter;if(!roster.TryGetValue(id,out fighter)||fighter.bot||fighter.disconnected)cameraCapabilities.Remove(id);}
   foreach(int id in budgets.Keys.ToArray())
   {Fighter fighter;if(!roster.TryGetValue(id,out fighter)||fighter.bot||fighter.disconnected)budgets.Remove(id);}
   foreach(int id in disabledProfiles.ToArray())if(!roster.ContainsKey(id))disabledProfiles.Remove(id);
   attached.RemoveWhere(head=>head==null);
  }
  public bool HasLiveFeed(int id)
  {Feed feed;return profiles.Contains(id)&&feeds.TryGetValue(id,out feed)&&feed.texture!=null&&Time.unscaledTime-feed.received<=StareCameraFrameValidation.FreshSeconds;}
  void Remove(int id)
  {
   Feed feed;if(feeds.TryGetValue(id,out feed)){if(feed.texture!=null)Destroy(feed.texture);feeds.Remove(id);}
   var avatar=lastVisuals!=null?lastVisuals.AvatarFor(id):null;if(avatar!=null&&avatar.cameraHead!=null)avatar.cameraHead.Present(null);
  }
  public void Reset()
  {
   foreach(var head in attached)if(head!=null)head.Present(null);
   foreach(var feed in feeds.Values)if(feed.texture!=null)Destroy(feed.texture);
   feeds.Clear();budgets.Clear();roster.Clear();profiles.Clear();cameraCapabilities.Clear();disabledProfiles.Clear();attached.Clear();publishAt=-10;publishSequence=0;AcceptedFrames=0;lastVisuals=null;
  }
  void OnDestroy(){Reset();}
 }

 // The screen follows the animated head bone, including dance clips and live
 // VR head poses. Whole torso/arm renderers are never disabled or rescaled.
 [DefaultExecutionOrder(2300)]
 public sealed class StareCameraHead : MonoBehaviour
 {
  sealed class Hidden {public Renderer renderer;public bool enabled;}
  readonly List<Hidden> hidden=new List<Hidden>();
  CreatureView avatar;Transform head,panel;Vector3 offset;Quaternion correction;Material display,frame;bool visible;
  public bool Live=>visible;
  public Vector3 PortraitFocus=>panel!=null?panel.TransformPoint(Vector3.back*.253f):transform.position+Vector3.up*1.55f;
  public int HiddenRenderers=>hidden.Count;
  public int HiddenSkinnedRenderers=>hidden.Count(item=>item.renderer is SkinnedMeshRenderer);
  public string[] HiddenRendererNames=>hidden.Where(item=>item.renderer!=null).Select(item=>item.renderer.name).ToArray();
  public void Bind(CreatureView actor)
  {
   avatar=actor;head=actor.preset.GetComponentsInChildren<Transform>(true).FirstOrDefault(node=>node.name=="Head"||node.name=="head");
   if(head==null)head=actor.preset;
   Vector3 eyes=actor.eyes.left!=null&&actor.eyes.right!=null?(actor.eyes.left.position+actor.eyes.right.position)*.5f:transform.position+Vector3.up*1.55f;
   // Inverse rotation, rather than inverse transform scale, keeps a 56 cm head
   // independent of the FBX importer's bone scale.
   offset=Quaternion.Inverse(head.rotation)*(eyes-head.position-transform.forward*.035f);
   correction=Quaternion.Inverse(head.rotation)*Quaternion.LookRotation(transform.forward,transform.up);
   foreach(var renderer in actor.preset.GetComponentsInChildren<Renderer>(true))
   {
    if(renderer is LineRenderer||renderer.GetComponent<TextMesh>()!=null)continue;
    // The imported character is merged by material, so a single skin contains
    // head, torso and limbs. Never infer a head-only skin from a first-frame
    // BakeMesh: the animator may not have evaluated and can collapse its bounds.
    // The opaque housing encloses the original sculpted head. Only rigid eyes
    // and facial extras directly attached to the head bone are hidden.
    bool attachedToHead=renderer.transform==head||renderer.transform.IsChildOf(head);
    if(!(renderer is SkinnedMeshRenderer)&&attachedToHead)hidden.Add(new Hidden{renderer=renderer,enabled=renderer.enabled});
   }
   panel=new GameObject("Live phone camera head").transform;panel.SetParent(actor.preset,false);panel.localScale=Vector3.one;
   display=new Material(Resources.Load<Shader>("StareVideoSurface")){name="Live contestant camera"};
   frame=new Material(Shader.Find("Standard")){name="Camera head frame",color=new Color(.015f,.025f,.04f)};frame.SetFloat("_Metallic",.55f);frame.SetFloat("_Glossiness",.55f);
   var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Camera head housing";backing.transform.SetParent(panel,false);backing.transform.localScale=new Vector3(.60f,.60f,.48f);backing.GetComponent<Renderer>().sharedMaterial=frame;Destroy(backing.GetComponent<Collider>());
   var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Live front camera";face.transform.SetParent(panel,false);face.transform.localPosition=Vector3.back*.253f;face.transform.localScale=new Vector3(.56f,.56f,1);face.GetComponent<Renderer>().sharedMaterial=display;Destroy(face.GetComponent<Collider>());
   StareFloorReflection.Mark(panel,actor.root.gameObject.layer);
   panel.gameObject.SetActive(false);ApplyPose();
  }
  public void Present(Texture texture)
  {
   bool active=texture!=null;
   if(display!=null)display.mainTexture=texture;
   if(panel!=null)panel.gameObject.SetActive(active);
   if(active!=visible)
   {foreach(var item in hidden)if(item.renderer!=null)item.renderer.enabled=active?false:item.enabled;visible=active;}
   if(active)ApplyPose();
  }
  void ApplyPose()
  {
   if(panel==null||head==null)return;
   panel.SetPositionAndRotation(head.position+head.rotation*offset,head.rotation*correction);
   var parent=panel.parent;var scale=parent!=null?parent.lossyScale:Vector3.one;
   panel.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
  }
  void LateUpdate(){if(visible)ApplyPose();}
  void OnDestroy(){Present(null);if(panel!=null)Destroy(panel.gameObject);if(display!=null)Destroy(display);if(frame!=null)Destroy(frame);}
 }
}
