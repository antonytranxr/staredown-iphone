using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Staredown;
public static class StareDanceExpansionChecks
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 public static string AssetsAndRules()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  check(Enumerable.Range(0,8).Select(StareDanceCatalog.DisplayId).Distinct().Count()==8,"All eight IDs appear once in the picker");
  var tournament=new Tournament();tournament.Add(0,"DANCER");
  for(int id=0;id<8;id++)
  {
   check(tournament.Dance(0,true,id)&&tournament.Find(0).dancing&&tournament.Find(0).dance==id,"Host accepts dance "+id);
   var wire=JsonUtility.FromJson<WireMessage>(JsonUtility.ToJson(new WireMessage{kind="dance",dancing=true,dance=id}));
   check(wire.kind=="dance"&&wire.dance==id&&wire.dancing,"Wire preserves dance "+id);
  }
  check(!tournament.Dance(0,true,-1)&&!tournament.Dance(0,true,8)&&!tournament.Dance(404,true,4),"Reject invalid selections and missing player");
  check(tournament.Dance(0,false,0)&&!tournament.Find(0).dancing,"Stop remains available");
  foreach(string name in new[]{"Frost","Ember","Obsidian","Aurora","Knight","Mage","Rogue","Barbarian"})
  {
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Staredown/Resources/StaredownAvatars/Animations/"+name+".controller");
   var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Staredown/Resources/StaredownAvatars/"+name+".fbx");
   for(int id=0;id<8;id++)check(controller.layers[0].stateMachine.states.Any(s=>s.state.name==StareDanceCatalog.State(id)&&s.state.motion!=null),name+" state "+id);
   for(int id=4;id<8;id++)
   {
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Staredown/Resources/StaredownAvatars/Dances/"+name+"_Dance"+id+".anim");
    check(clip!=null&&clip.length>7.9f&&clip.isLooping,name+" duration/loop "+id);
    var bindings=AnimationUtility.GetCurveBindings(clip);check(bindings.Length>50,name+" full body curves "+id);
    float maxSeam=0;int moving=0;
    foreach(var binding in bindings)
    {
     check(binding.path.Length==0||model.transform.Find(binding.path)!=null,name+" bound path "+binding.path);
     var curve=AnimationUtility.GetEditorCurve(clip,binding);check(curve.keys.All(k=>!float.IsNaN(k.value)&&!float.IsInfinity(k.value)),name+" finite curves");
     if(!binding.propertyName.StartsWith("m_LocalRotation"))maxSeam=Mathf.Max(maxSeam,Mathf.Abs(curve.Evaluate(0)-curve.Evaluate(clip.length)));
     if(curve.keys.Max(k=>k.value)-curve.keys.Min(k=>k.value)>.05f)moving++;
    }
    foreach(string path in bindings.Where(b=>b.propertyName=="m_LocalRotation.x").Select(b=>b.path))
    {
     Func<float,Quaternion> at=time=>{var q=new float[4];int index=0;foreach(string axis in new[]{"x","y","z","w"}){var b=bindings.First(k=>k.path==path&&k.propertyName=="m_LocalRotation."+axis);q[index++]=AnimationUtility.GetEditorCurve(clip,b).Evaluate(time);}return new Quaternion(q[0],q[1],q[2],q[3]);};
     check(Quaternion.Angle(at(0),at(clip.length))<.2f,name+" rotational loop "+id+" / "+path);
    }
    check(maxSeam<.02f,name+" clean loop endpoint "+id+" seam="+maxSeam);
    check(moving>20,name+" distinct full-body motion "+id);
   }
  }
  return n+" dance asset/network checks passed";
 }
 public static string MobileMenu()
 {
  var session=UnityEngine.Object.FindFirstObjectByType<StareMobileSession>();var hud=UnityEngine.Object.FindFirstObjectByType<StareMobileHud>();
  var picker=(RectTransform)typeof(StareMobileHud).GetField("dancePicker",Private).GetValue(hud);
  var options=(Button[])typeof(StareMobileHud).GetField("danceOptions",Private).GetValue(hud);
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  check(options.Length==8,"Eight mobile options");
  for(int id=0;id<8;id++)
  {
   picker.gameObject.SetActive(true);options[id].onClick.Invoke();var local=session.State.fighters.First(f=>f.id==session.LocalId);
   check(local.dancing&&local.dance==id,"Phone button selects actual dance "+id);
   check(!picker.gameObject.activeSelf,"One tap closes picker");
   var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(picker,options[id].transform);
   check(bounds.min.x>=picker.rect.xMin&&bounds.max.x<=picker.rect.xMax&&bounds.min.y>=picker.rect.yMin&&bounds.max.y<=picker.rect.yMax,"Phone target contained "+id);
  }
  ((Button)typeof(StareMobileHud).GetField("danceStop",Private).GetValue(hud)).onClick.Invoke();check(!session.State.fighters.First(f=>f.id==session.LocalId).dancing,"Phone Stop works");
  return n+" mobile dance menu checks passed";
 }
}
