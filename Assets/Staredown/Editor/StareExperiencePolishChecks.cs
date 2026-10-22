using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Staredown;
using UnityEngine;
public static class StareExperiencePolishChecks
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 public static string Countdown()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  Func<Tournament> room=()=>{var t=new Tournament(1);t.Add(0,"HOST");t.Add(1,"GUEST");t.Input(0,0,0,true,true,0,0,0);t.Input(1,0,0,true,true,0,0,0);if(!t.Start())throw new Exception("Start fixture");return t;};
  var match=room();int sequence=0;
  for(int i=0;i<110&&match.Phase==MatchPhase.Countdown;i++)
  {
   float closed=i<92&&i%10<2?1:0;if(closed>0&&i%10==0)sequence++;
   float remaining=match.Remaining;match.Input(0,closed,closed,true,true,sequence,0,0);match.Input(1,0,0,true,true,0,0,0);match.Tick(.05f);
   check(match.Find(0).alive&&match.Find(1).alive,"Countdown blink never eliminates");
   check(match.Phase!=MatchPhase.Countdown||match.Remaining<remaining,"Countdown keeps decreasing during blinks");
  }
  check(match.Phase==MatchPhase.Duel&&match.Clock<5.2f,"Repeated countdown blinks do not delay Go");
  match.Input(0,1,1,true,true,sequence+1,0,0);for(int i=0;i<4;i++){match.Input(1,0,0,true,true,0,0,0);match.Tick(.05f);}check(!match.Find(0).alive&&match.Find(1).alive,"Fresh blink after Go loses");
  var held=room();for(int i=0;i<110&&held.Phase==MatchPhase.Countdown;i++){held.Input(0,1,1,true,true,1,0,0);held.Input(1,0,0,true,true,0,0,0);held.Tick(.05f);}check(held.Phase==MatchPhase.Duel,"Holding blink does not reset countdown");
  for(int i=0;i<4;i++){held.Input(0,1,1,true,true,1,0,0);held.Input(1,0,0,true,true,0,0,0);held.Tick(.05f);}check(!held.Find(0).alive&&held.Find(1).alive,"Pre-Go closure held through Go still counts without a second sequence");
  var lost=room();float saved=lost.Remaining;lost.Input(0,0,0,false,true,0,0,0);lost.Tick(.2f);check(lost.TrackingPaused&&lost.Remaining==saved,"Tracking loss still pauses countdown");
  return n+" countdown checks passed";
 }
 public static string NavigationAndFeeds()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};var game=UnityEngine.Object.FindFirstObjectByType<StareGame>();var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();var nav=game.GetComponent<StareNavigation>();var rig=game.rig.transform;var head=game.rig.centerEyeAnchor;var position=rig.position;var rotation=rig.rotation;
  try
  {
   check(game.InLobby,"Navigation fixture is in lobby");var right=Vector3.ProjectOnPlane(head.right,Vector3.up).normalized;var before=head.position;
   nav.HandleMicrogesture(OVRHand.MicrogestureType.SwipeLeft,0);check(Vector3.Dot(head.position-before,right)<-.44f&&Quaternion.Angle(rig.rotation,rotation)<.001f,"Left-hand swipe left strafes without rotation");
   before=head.position;nav.HandleMicrogesture(OVRHand.MicrogestureType.SwipeRight,0);check(Vector3.Dot(head.position-before,right)>.44f&&Quaternion.Angle(rig.rotation,rotation)<.001f,"Left-hand swipe right strafes without rotation");
   nav.HandleMicrogesture(OVRHand.MicrogestureType.SwipeRight,1);check(Mathf.Abs(Quaternion.Angle(rig.rotation,rotation)-30)<.01f,"Right-hand snap turn remains available");
   check(view.ScoreboardMode&&view.ArenaFeed==null,"Direct tournament board needs no arena render texture");
   var mirror=view.GetComponentInChildren<StareArenaMirror>();mirror.Capture(head.GetComponent<Camera>());var feeds=(RenderTexture[])typeof(StareArenaMirror).GetField("feeds",Private).GetValue(mirror);
   check(feeds.All(feed=>feed!=null&&feed.width==1536&&feed.height==1024),"Wider mirror uses landscape textures at the previous pixel budget");
   if(game.State.fighters.Length>=2)
   {
    typeof(StareVisuals).GetMethod("CaptureSelectedEyes",Private).Invoke(view,new object[]{game.State.fighters[0],game.State.fighters[1]});var eyes=(RenderTexture[])typeof(StareVisuals).GetField("eyeFeeds",Private).GetValue(view);
    check(eyes.All(feed=>feed!=null&&feed.width>=1024&&feed.height>=512),"Contestant eye feeds preserve facial detail");
   }
   check(Shader.GetGlobalFloat("_StareExternalView")<.5f,"Captures restore first-person head hiding");return n+" navigation/feed checks passed";
  }
  finally{rig.SetPositionAndRotation(position,rotation);}
 }
 sealed class SkinData{public BoneWeight[] weights;public Vector3[] vertices;public Matrix4x4[] poses;}
 static readonly Dictionary<Mesh,SkinData> skinData=new Dictionary<Mesh,SkinData>();
 static Vector3 SkinPoint(SkinnedMeshRenderer skin,int index)
 {
  // Imported FBX skin transforms can carry scale twice after BakeMesh. Use
  // the GPU skinning equation and measure in head-relative metres instead.
  var mesh=skin.sharedMesh;SkinData data;if(!skinData.TryGetValue(mesh,out data)){data=new SkinData{weights=mesh.boneWeights,vertices=mesh.vertices,poses=mesh.bindposes};skinData[mesh]=data;}var weight=data.weights[index];var vertex=data.vertices[index];var pose=data.poses;var bones=skin.bones;
  return bones[weight.boneIndex0].localToWorldMatrix.MultiplyPoint3x4(pose[weight.boneIndex0].MultiplyPoint3x4(vertex))*weight.weight0+
   bones[weight.boneIndex1].localToWorldMatrix.MultiplyPoint3x4(pose[weight.boneIndex1].MultiplyPoint3x4(vertex))*weight.weight1+
   bones[weight.boneIndex2].localToWorldMatrix.MultiplyPoint3x4(pose[weight.boneIndex2].MultiplyPoint3x4(vertex))*weight.weight2+
   bones[weight.boneIndex3].localToWorldMatrix.MultiplyPoint3x4(pose[weight.boneIndex3].MultiplyPoint3x4(vertex))*weight.weight3;
 }
 public static string HeadMasksAndFaceRig()
 {
  var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();var fixture=new GameObject("Head-mask and facial animation checks");int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  try
  {
   for(int kind=0;kind<9;kind++)
   {
    var a=view.CreateAvatar(kind,null,fixture.transform);var animator=a.preset.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Play("Idle",0,0);animator.Update(.02f);var head=a.preset.GetComponentsInChildren<Transform>().First(t=>t.name=="Head"||t.name=="head");var lod=a.preset.GetComponent<LODGroup>();if(lod!=null)lod.ForceLOD(0);
    int heads=0,bodies=0;var probes=new List<Tuple<SkinnedMeshRenderer,int[]>>();
    foreach(var skin in a.preset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
    {
     var mask=new List<Vector2>();skin.sharedMesh.GetUVs(3,mask);check(mask.Count==skin.sharedMesh.vertexCount,"Imported head mask covers skin "+kind+" / "+skin.name);heads+=mask.Count(x=>x.x>.99f);bodies+=mask.Count(x=>x.x<.01f);
     if(lod!=null&&lod.GetLODs()[0].renderers.Contains(skin)){var indices=mask.Select((value,index)=>new{value,index}).Where(x=>x.value.x>.999f).Select(x=>x.index).ToArray();if(indices.Length>0)probes.Add(Tuple.Create(skin,indices.Where((index,i)=>i%Mathf.Max(1,indices.Length/12)==0).Take(12).ToArray()));}
    }
    check(heads>10&&bodies>10,"Head is hidden independently of body "+kind);
    var samples=new Dictionary<SkinnedMeshRenderer,Vector3[]>();foreach(var probe in probes){var mesh=new Mesh();samples[probe.Item1]=probe.Item2.Select(i=>Quaternion.Inverse(head.rotation)*(SkinPoint(probe.Item1,i)-head.position)).ToArray();UnityEngine.Object.Destroy(mesh);}
    foreach(string clip in new[]{"Idle","Walk","Dance","Dance1","Dance2","Dance3","Dance4","Dance5","Dance6","Dance7"})foreach(float pose in new[]{.25f,.65f})
    {
     animator.Play(clip,0,pose);animator.Update(.01f);float drift=0;
     foreach(var probe in probes){var mesh=new Mesh();for(int i=0;i<probe.Item2.Length;i++)drift=Mathf.Max(drift,Vector3.Distance(samples[probe.Item1][i],Quaternion.Inverse(head.rotation)*(SkinPoint(probe.Item1,probe.Item2[i])-head.position)));UnityEngine.Object.Destroy(mesh);}
     check(drift<.003f,"Rigid facial surface remains with head "+kind+" / "+clip+" / "+pose+" drift="+drift);
    }
    UnityEngine.Object.Destroy(a.root.gameObject);
   }
   return n+" head-mask and facial rig checks passed";
  }
  finally{skinData.Clear();UnityEngine.Object.Destroy(fixture);}
 }
}
