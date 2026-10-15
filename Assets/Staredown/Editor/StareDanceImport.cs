using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static class StareDanceImport
{
 public static string Import()
 {
  int count=0;foreach(string name in new[]{"Frost","Ember","Obsidian","Aurora","Knight","Mage","Rogue","Barbarian"})
  {
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Staredown/Resources/StaredownAvatars/Animations/"+name+".controller");
   for(int dance=1;dance<=3;dance++)
   {
    string path="Assets/Staredown/Art/DanceSources/"+name+"_Dance"+dance+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;var definitions=importer.defaultClipAnimations;
    foreach(var clip in definitions){clip.loopTime=true;clip.loopPose=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.lockRootRotation=true;clip.keepOriginalOrientation=true;}importer.clipAnimations=definitions;importer.SaveAndReimport();
    var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
    var original=AssetDatabase.LoadAllAssetsAtPath("Assets/Staredown/Resources/StaredownAvatars/"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
    string rig=name=="Knight"||name=="Mage"||name=="Rogue"||name=="Barbarian"?"KayKitRig":"StareRig";string rigPath=AnimationUtility.GetCurveBindings(original).First(b=>b.path==rig||b.path.EndsWith("/"+rig)).path;string prefix=rigPath.Substring(0,rigPath.Length-rig.Length);
    string clipPath="Assets/Staredown/Resources/StaredownAvatars/Dances/"+name+"_Dance"+dance+".anim";var motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(motion==null){motion=new AnimationClip();AssetDatabase.CreateAsset(motion,clipPath);}motion.ClearCurves();motion.name=name+"_Dance"+dance;motion.frameRate=30;
    foreach(var binding in AnimationUtility.GetCurveBindings(source)){var fixedBinding=binding;if(binding.path==rig||binding.path.StartsWith(rig+"/"))fixedBinding.path=prefix+binding.path;AnimationUtility.SetEditorCurve(motion,fixedBinding,AnimationUtility.GetEditorCurve(source,binding));}
    var settings=AnimationUtility.GetAnimationClipSettings(source);settings.loopTime=true;settings.loopBlend=true;AnimationUtility.SetAnimationClipSettings(motion,settings);EditorUtility.SetDirty(motion);
    var machine=controller.layers[0].stateMachine;var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Dance"+dance)??machine.AddState("Dance"+dance);state.motion=motion;state.writeDefaultValues=true;EditorUtility.SetDirty(controller);count++;
   }
  }
  AssetDatabase.SaveAssets();return count+" CMU dance clips imported and controllers updated";
 }
}
