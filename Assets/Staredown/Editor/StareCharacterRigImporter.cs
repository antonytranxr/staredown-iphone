using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public sealed class StareCharacterRigImporter : AssetPostprocessor
{
 void OnPreprocessModel()
 {
  if(!assetPath.StartsWith("Assets/Staredown/Resources/StaredownAvatars/")||!assetPath.EndsWith(".fbx"))return;
  var importer=(ModelImporter)assetImporter;
  importer.animationType=ModelImporterAnimationType.Generic;
  importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
  importer.optimizeGameObjects=false;importer.importAnimation=!assetPath.Contains("_LOD");
  importer.animationCompression=ModelImporterAnimationCompression.Optimal;
  importer.animationRotationError=.3f;importer.animationPositionError=.2f;
  if(importer.importAnimation)
  {
   var clips=importer.defaultClipAnimations;
   foreach(var clip in clips){clip.loopTime=true;clip.loopPose=true;clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;}
   if(clips.Length>0)importer.clipAnimations=clips;
  }
 }
 void OnPostprocessModel(GameObject root)
 {
  if(!assetPath.StartsWith("Assets/Staredown/Resources/StaredownAvatars/")||!assetPath.EndsWith(".fbx"))return;
  // Bake a head mask into UV4 at import time. No readable mesh copy is needed
  // on the headset, and merged skins keep their torso, arms and legs intact.
  foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
  {
   var mesh=skin.sharedMesh;if(mesh==null)continue;
   var bones=skin.bones;var weights=mesh.boneWeights;var mask=new List<Vector2>(mesh.vertexCount);
   bool IsHead(int index){if(index<0||index>=bones.Length||bones[index]==null)return false;for(var bone=bones[index];bone!=null;bone=bone.parent)if(bone.name=="Head"||bone.name=="head")return true;return false;}
   foreach(var w in weights)mask.Add(new Vector2((IsHead(w.boneIndex0)?w.weight0:0)+(IsHead(w.boneIndex1)?w.weight1:0)+(IsHead(w.boneIndex2)?w.weight2:0)+(IsHead(w.boneIndex3)?w.weight3:0),0));
   if(mask.Count==mesh.vertexCount)mesh.SetUVs(3,mask);
  }
  foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
  {
   bool head=false;for(var parent=filter.transform;parent!=null;parent=parent.parent)if(parent.name=="Head"||parent.name=="head")head=true;
   if(head&&filter.sharedMesh!=null)filter.sharedMesh.SetUVs(3,Enumerable.Repeat(Vector2.right,filter.sharedMesh.vertexCount).ToList());
  }
 }
}
