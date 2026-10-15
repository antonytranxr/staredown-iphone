using UnityEngine;
namespace Staredown
{
 public sealed class StareButton : MonoBehaviour
 {
  public static readonly System.Collections.Generic.HashSet<StareButton> Active=new System.Collections.Generic.HashSet<StareButton>();
  void OnEnable(){Active.Add(this);}
  public string action;
  public bool Interactable { get; set; } = true;
  Renderer surface; MaterialPropertyBlock properties;
  Vector3 home; float hover,pressedAt=-10; bool hovered,selected;
  TextMesh[] labels;Color[] labelColors;bool wasEnabled=true;
  public void Configure(Renderer target){surface=target;properties=new MaterialPropertyBlock();home=transform.localPosition;}
  public void SetHovered(bool value){bool next=value&&Interactable;if(next&&!hovered)StareFeedback.Instance?.UI(false,transform.position);hovered=next;}
  public void SetSelected(bool value){selected=value;}
  public void SetLayoutPosition(Vector3 position){if(home==position)return;home=position;transform.localPosition=position;}
  public void Pulse(){if(Interactable){pressedAt=Time.unscaledTime;StareFeedback.Instance?.UI(true,transform.position);}}
  void Start(){labels=GetComponentsInChildren<TextMesh>();labelColors=new Color[labels.Length];for(int i=0;i<labels.Length;i++)labelColors[i]=labels[i].color;}
  void Update()
  {
   if(surface==null)return;
   hover=Mathf.MoveTowards(hover,hovered&&Interactable?1:0,Time.unscaledDeltaTime*9);
   float press=Mathf.Clamp01(1-(Time.unscaledTime-pressedAt)/.17f);
   transform.localPosition=home+Vector3.forward*(press*.015f-hover*.006f);
   properties.SetFloat("_Hover",hover);properties.SetFloat("_Selected",selected?1:0);properties.SetFloat("_Disabled",Interactable?0:1);
   if(wasEnabled!=Interactable&&labels!=null){wasEnabled=Interactable;for(int i=0;i<labels.Length;i++)labels[i].color=Interactable?labelColors[i]:new Color(.40f,.47f,.55f);}
   surface.SetPropertyBlock(properties);
  }
  void OnDisable(){Active.Remove(this);hovered=false;hover=0;if(surface!=null)transform.localPosition=home;}
 }
}
