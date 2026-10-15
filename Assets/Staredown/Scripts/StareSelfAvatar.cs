using System.Collections.Generic;
using UnityEngine;
namespace Staredown
{
 // Keep the tracked body below the viewer. Only HMD views clip its own head;
 // external cameras and the arena mirror retain the complete character.
 [DefaultExecutionOrder(2100)]
 public sealed class StareSelfAvatar : MonoBehaviour
 {
  CreatureView actor;Transform viewer;Vector3 originalScale;float referenceEyeHeight;readonly List<Material> owned=new List<Material>();
  public bool Viewing=>viewer!=null;public Transform Viewer=>viewer;
  public void Initialize(CreatureView avatar)
  {
   actor=avatar;originalScale=transform.localScale;
   if(actor.eyes.left!=null&&actor.eyes.right!=null)referenceEyeHeight=((actor.eyes.left.position+actor.eyes.right.position)*.5f-transform.position).y;
   var shader=Resources.Load<Shader>("StareSelfAvatar");if(shader==null)return;
   var copies=new[]{new Dictionary<Material,Material>(),new Dictionary<Material,Material>()};
   foreach(var r in avatar.preset.GetComponentsInChildren<Renderer>(true))
   {
    if(r is LineRenderer||r.GetComponent<TextMesh>()!=null)continue;
    bool headPart=false;if(!(r is SkinnedMeshRenderer))for(var part=r.transform;part!=null&&part!=avatar.preset;part=part.parent)if(part.name=="Head"||part.name=="head"||part.name=="LeftEyeHolder"||part.name=="RightEyeHolder")headPart=true;
    var cache=copies[headPart?1:0];
    var mats=r.sharedMaterials;
    for(int i=0;i<mats.Length;i++)
    {
     var source=mats[i];if(source==null)continue;
     if(!cache.TryGetValue(source,out var copy)){copy=new Material(shader);copy.CopyPropertiesFromMaterial(source);copy.SetFloat("_HeadPart",headPart?1:0);copy.name="Self body / "+source.name;cache[source]=copy;owned.Add(copy);}
     mats[i]=copy;
    }
    r.sharedMaterials=mats;
   }
  }
  public void SetViewer(Transform head){viewer=head;if(head==null)transform.localScale=originalScale;}
  void LateUpdate(){Align();}
  public void Align()
  {
   if(actor==null)return;
   foreach(var mat in owned){mat.SetVector("_LocalHead",viewer!=null?viewer.position:Vector3.zero);mat.SetFloat("_LocalHeadRadius",viewer!=null?.25f:0);}
   if(viewer==null||actor.motion!=null&&actor.motion.Eliminating)return;
   float floor=transform.position.y;
   transform.position=new Vector3(viewer.position.x,floor,viewer.position.z);
   if(actor.meta!=null&&actor.meta.Loaded){transform.localScale=originalScale;return;}
   if(actor.eyes.left==null||actor.eyes.right==null)return;
   float baseHeight=referenceEyeHeight;
   if(baseHeight>.2f){float fit=Mathf.Clamp((viewer.position.y-floor)/baseHeight,.40f,1.35f);transform.localScale=originalScale*fit;}
   var eye=(actor.eyes.left.position+actor.eyes.right.position)*.5f;transform.position+=new Vector3(viewer.position.x-eye.x,0,viewer.position.z-eye.z);
  }
  void OnDestroy(){foreach(var mat in owned)if(mat!=null)Destroy(mat);}
 }
}
