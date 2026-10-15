using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Staredown
{
    public sealed partial class StareVisuals
    {
        public Transform characterMenu;
        readonly List<CreatureView> characterPreviews = new List<CreatureView>();
        readonly List<StareButton> characterCards = new List<StareButton>();
        readonly List<TextMesh> characterChecks = new List<TextMesh>();
        readonly List<TextMesh> characterNames = new List<TextMesh>(), characterTaglines = new List<TextMesh>(), characterNumbers = new List<TextMesh>();
        TextMesh characterPageLabel; int characterPage; bool characterWasShown;
        TextMesh characterTitle,characterRole,characterBio,metaPreview; Renderer heroSurface; int displayedCharacter=-1;
        TextMesh characterStatus, characterSelected;
        Cubemap characterStudioEnvironment;

        public CreatureView CreateAvatar(int kind, string userId, Transform parent, bool local = false)
        {
            int preset = StareAvatarCatalog.IsPreset(kind) ? kind : 0;
            string asset = StareAvatarCatalog.Assets[preset];
            var source = Resources.Load<GameObject>("StaredownAvatars/" + asset);
            CreatureView result;
            if (source == null)
            {
                result = StareCreatureArt.Build(this, preset & 1, parent);
                result.root.localScale = Vector3.one * 1.15f;
                result.preset = result.root;
            }
            else
            {
                var root = Group(StareAvatarCatalog.Names[preset] + " / contestant", parent, Vector3.zero);
                var model = Instantiate(source, root).transform;
                model.name = asset + " / Blender model";
                model.localRotation = Quaternion.Euler(0,180,0) * model.localRotation;
                if(preset>=5)model.localPosition+=Vector3.down*.044f;
                AddContestantLODs(model, root, asset);
                var animator=model.GetComponent<Animator>()??model.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("StaredownAvatars/Animations/"+asset);
                animator.applyRootMotion=false;animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                var data = Resources.Load<TextAsset>("StaredownAvatars/" + asset + ".materials");
                var properties = data == null ? null : JsonUtility.FromJson<StareAvatarMaterials>(data.text);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var imported = renderer.sharedMaterials;
                    var converted = new Material[imported.Length];
                    for (int i = 0; i < imported.Length; i++)
                    {
                        string name = imported[i].name;
                        var definition = properties?.materials?.FirstOrDefault(x => name == x.name || name.StartsWith(x.name + " "));
                        var color = definition?.baseColor;
                        // Blender exports linear reflectance; Standard's color property
                        // expects an sRGB input and converts it back for linear lighting.
                        var tint = color != null && color.Length >= 3 ? new Color(color[0],color[1],color[2],color.Length > 3 ? color[3] : 1).gamma : imported[i].color;
                        var mat = Mat("Avatar / " + asset + " / " + name, tint);
                        if (definition != null)
                        {
                            mat.SetFloat("_Metallic", definition.metallic);
                            mat.SetFloat("_Glossiness", 1 - definition.roughness);
                            if (!string.IsNullOrEmpty(definition.albedoTexture))
                            {
                                var albedo = Resources.Load<Texture2D>(definition.albedoTexture);
                                if (albedo != null) { mat.mainTexture = albedo; mat.color = Color.white; }
                                else Debug.LogError("Missing avatar albedo: " + definition.albedoTexture);
                            }
                            if (!string.IsNullOrEmpty(definition.normalTexture))
                            {
                                var normal = Resources.Load<Texture2D>(definition.normalTexture);
                                if (normal != null)
                                {
                                    mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP");
                                    mat.SetFloat("_BumpScale", definition.normalScale > 0 ? definition.normalScale : 1);
                                }
                                else Debug.LogError("Missing avatar normal map: " + definition.normalTexture);
                            }
                            if (!string.IsNullOrEmpty(definition.occlusionTexture))
                            {
                                var occlusion = Resources.Load<Texture2D>(definition.occlusionTexture);
                                if (occlusion != null)
                                {
                                    mat.SetTexture("_OcclusionMap", occlusion);
                                    mat.SetFloat("_OcclusionStrength", definition.occlusionStrength);
                                }
                                else Debug.LogError("Missing avatar occlusion: " + definition.occlusionTexture);
                            }
                            if (definition.emissionStrength > 0 && definition.emission != null && definition.emission.Length >= 3)
                            {
                                mat.EnableKeyword("_EMISSION");
                                mat.SetColor("_EmissionColor", new Color(definition.emission[0],definition.emission[1],definition.emission[2]) * definition.emissionStrength);
                            }
                        }
                        converted[i] = mat;
                    }
                    renderer.sharedMaterials = converted;
                }
                var nodes = model.GetComponentsInChildren<Transform>(true);
                var eyes = new EyePortrait
                {
                    left = nodes.FirstOrDefault(x => x.name == "LeftEyelid"), right = nodes.FirstOrDefault(x => x.name == "RightEyelid"),
                    pupilL = nodes.FirstOrDefault(x => x.name == "LeftPupil"), pupilR = nodes.FirstOrDefault(x => x.name == "RightPupil")
                };
                if (eyes.left != null && eyes.right != null) eyes.Bind();
                else Debug.LogError(asset + " is missing its animated eyelid nodes.");
                float labelHeight=preset==5?2.45f:preset==6?2.9f:preset==7?2.2f:preset==8?2.4f:2.15f;
                result = new CreatureView { root=root, preset=model, eyes=eyes, body=model.GetComponentInChildren<Renderer>(), label=Text("CONTENDER",root,new Vector3(0,labelHeight,0),.043f,Color.white) };
            }
            result.appearanceKey = StareAvatarCatalog.AppearanceKey(kind, userId);
            if (kind == StareAvatarCatalog.Meta && StareMetaAvatars.Instance != null)
            {
                var personal = StareMetaAvatars.Instance.CreateAvatar(result.root, userId, local);
                if (personal != null) result.meta = personal.GetComponent<StareMetaAvatarView>();
            }
            StareFloorReflection.Mark(result.root);
            result.presence=result.root.gameObject.AddComponent<StareAvatarPresence>();result.presence.Bind(result,this);
            return result;
        }

        void ApplySkin(CreatureView avatar,int skin)
        {
            if(skin<1||skin>3)return;Color[] tint={Color.white,new Color(.38f,.78f,1),new Color(1,.63f,.25f),new Color(.71f,.43f,1)};
            foreach(var renderer in avatar.preset.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer is LineRenderer||renderer.GetComponent<TextMesh>()!=null||renderer.name.Contains("Pupil")||renderer.name.Contains("Eyelid")||renderer.name=="Tracked talking mouth")continue;
                var originals=renderer.sharedMaterials;var variants=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++){var original=originals[i];string key="Unlock skin "+skin+" / "+original.GetInstanceID();Material variant;if(!materials.TryGetValue(key,out variant)){variant=new Material(original);if(variant.HasProperty("_Color"))variant.color=Color.Lerp(original.color,tint[skin],.58f);if(variant.HasProperty("_Metallic"))variant.SetFloat("_Metallic",skin==2?.55f:.22f);materials[key]=variant;}variants[i]=variant;}renderer.sharedMaterials=variants;
            }
        }
        Transform BuildHumanMarker(Transform parent)
        {
            var node=Group("Human player diamond",parent,Vector3.zero);var mesh=new Mesh{name="Shared human identity diamond"};
            artMeshes.Add(mesh);mesh.vertices=new[]{Vector3.up*.18f,Vector3.down*.18f,Vector3.left*.085f,Vector3.forward*.085f,Vector3.right*.085f,Vector3.back*.085f};mesh.triangles=new[]{0,2,3,0,3,4,0,4,5,0,5,2,1,3,2,1,4,3,1,5,4,1,2,5};mesh.RecalculateNormals();node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mat("Human identity lime",new Color(.42f,1,.28f),true);return node;
        }
        void AddContestantLODs(Transform model,Transform root,string asset)
        {
            var body=new List<Renderer>();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                bool animatedEye=false;
                for(var t=renderer.transform;t!=null&&t!=model;t=t.parent)
                    if(t.name=="LeftEyeHolder"||t.name=="RightEyeHolder")animatedEye=true;
                if(!animatedEye)body.Add(renderer);
            }
            var rigBones=new Dictionary<string,Transform>();
            foreach(var node in model.GetComponentsInChildren<Transform>())if(!rigBones.ContainsKey(node.name))rigBones[node.name]=node;
            var lods=new List<LOD>{new LOD(.22f,body.ToArray())};
            for(int level=1;level<=2;level++)
            {
                var source=Resources.Load<GameObject>("StaredownAvatars/"+asset+"_LOD"+level);
                if(source==null)return;
                var distance=Instantiate(source,root).transform;
                distance.name=asset+" / body LOD "+level;
                distance.localRotation=Quaternion.Euler(0,180,0)*distance.localRotation;
                if(asset=="Knight"||asset=="Mage"||asset=="Rogue"||asset=="Barbarian")distance.localPosition+=Vector3.down*.044f;
                // Keep the imported centimeter conversion and the exact world
                // pose; parent to the animated preset so breathing stays coherent.
                distance.SetParent(model,true);
                foreach(var animator in distance.GetComponentsInChildren<Animator>()){animator.enabled=false;Destroy(animator);}
                foreach(var skin in distance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var bones=skin.bones;for(int b=0;b<bones.Length;b++){Transform shared;if(bones[b]!=null&&rigBones.TryGetValue(bones[b].name,out shared))bones[b]=shared;}
                    skin.bones=bones;Transform sharedRoot;if(skin.rootBone!=null&&rigBones.TryGetValue(skin.rootBone.name,out sharedRoot))skin.rootBone=sharedRoot;
                    var bounds=skin.localBounds;bounds.Expand(Vector3.Scale(bounds.size,new Vector3(.35f,.18f,.25f)));skin.localBounds=bounds;
                }
                lods.Add(new LOD(level==1?.08f:.001f,distance.GetComponentsInChildren<Renderer>()));
            }
            var group=model.gameObject.AddComponent<LODGroup>();
            group.fadeMode=LODFadeMode.None;group.SetLODs(lods.ToArray());group.RecalculateBounds();
            float originalLodSize=group.size;var originalLodCenter=group.localReferencePoint;
            // Imported part bounds often cover only the bind pose. A shared actor envelope
            // prevents helmets, limbs and cloth from vanishing during dance/hand IK poses.
            var actorCenter=root.TransformPoint(new Vector3(0,1.2f,0));
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {var space=skin.rootBone!=null?skin.rootBone:skin.transform;var center=space.InverseTransformPoint(actorCenter);var x=space.InverseTransformVector(root.right*1.85f);var y=space.InverseTransformVector(root.up*2.05f);var z=space.InverseTransformVector(root.forward*1.85f);var extent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));skin.localBounds=new Bounds(center,extent*2);}
            group.size=originalLodSize;group.localReferencePoint=originalLodCenter;
        }

        void BuildCharacterSelection()
        {
            characterMenu=Group("Staredown / Character studio",transform,new Vector3(0,2.12f,-6.8f));
            UIFrame(characterMenu,new Vector2(5.6f,3.72f),"YOUR CONTENDER");
            UILabel("Your contender.",characterMenu,new Vector3(-2.49f,1.13f,-.04f),.083f,UIInk,true);
            UILabel("Find your face. Hold your stare.",characterMenu,new Vector3(-2.48f,.82f,-.04f),.036f,UIMuted);
            heroSurface=Panel("Contender showcase",characterMenu,new Vector3(-.99f,-.23f,-.033f),new Vector2(3.03f,1.83f),UISurface,UIAccent,.12f);
            characterTitle=UILabel("Frost",characterMenu,new Vector3(-2.28f,.39f,-.075f),.079f,UIInk,true);
            characterRole=UILabel("THE CIRCUIT FOX",characterMenu,new Vector3(-2.28f,.13f,-.075f),.022f,UIAccent,true);
            characterBio=UILabel("Cool under pressure.\nA gaze like ice.",characterMenu,new Vector3(-2.28f,-.22f,-.075f),.033f,UIMuted);
            UILabel("ALL CHARACTERS\nPLAY BY THE SAME RULES",characterMenu,new Vector3(-2.28f,-.83f,-.075f),.021f,UIMuted);
            metaPreview=UILabel("Your personal avatar\nloads in the arena",characterMenu,new Vector3(-.30f,-.18f,-.11f),.043f,UIInk,false,TextAnchor.MiddleCenter);
            metaPreview.gameObject.SetActive(false);
            for(int slot=0;slot<StareAvatarCatalog.PresetCount;slot++)
            {
                int i=StareAvatarCatalog.PresetId(slot);
                var preview=CreateAvatar(i,null,characterMenu);preview.root.localPosition=new Vector3(-.25f,i>=5?-1.03f:-1.07f,-.47f);preview.root.localScale=Vector3.one*(i==6?.64f:i>=5?.73f:.89f);
                var detail=preview.preset.GetComponent<LODGroup>();if(detail!=null)detail.ForceLOD(0);
                preview.root.localRotation=Quaternion.Euler(0,-14,0);preview.label.gameObject.SetActive(false);StareFloorReflection.Mark(preview.root,0);characterPreviews.Add(preview);
                preview.root.gameObject.SetActive(false);
            }
            UIButton("‹","avatar-page:-1",characterMenu,new Vector3(1.00f,1.13f,-.06f),new Vector2(.36f,.28f));
            UIButton("›","avatar-page:1",characterMenu,new Vector3(2.32f,1.13f,-.06f),new Vector2(.36f,.28f));
            characterPageLabel=UILabel("ORIGINALS / 1 OF 2",characterMenu,new Vector3(1.66f,1.13f,-.07f),.020f,UIMuted,true,TextAnchor.MiddleCenter);
            for(int i=0;i<4;i++)
            {
                float y=.43f-i*.48f;
                var button=UIButton("","avatar:"+i,characterMenu,new Vector3(1.66f,y,-.06f),new Vector2(1.72f,.405f),false,StareAvatarCatalog.Colors[i]);
                button.gameObject.name=StareAvatarCatalog.Names[i]+" selection";characterCards.Add(button);
                Panel("Number badge",button.transform,new Vector3(-.64f,0,-.01f),new Vector2(.23f,.23f),new Color(.075f,.10f,.14f),StareAvatarCatalog.Colors[i],.06f);
                characterNumbers.Add(UILabel("0"+(i+1),button.transform,new Vector3(-.64f,0,-.025f),.029f,StareAvatarCatalog.Colors[i],true,TextAnchor.MiddleCenter));
                characterNames.Add(UILabel(DisplayName(i),button.transform,new Vector3(-.43f,.065f,-.025f),.045f,UIInk,true));
                characterTaglines.Add(UILabel(StareAvatarCatalog.ShortDescriptions[i],button.transform,new Vector3(-.43f,-.085f,-.025f),.024f,UIMuted));
                characterChecks.Add(UILabel("✓",button.transform,new Vector3(.68f,.04f,-.03f),.039f,UIAccent,true,TextAnchor.MiddleCenter));
            }
            // The stage is lit independently of the venue so matte faces remain readable.
            for(int side=-1;side<=1;side+=2)
            {
                var fill=Group("Studio softbox",characterMenu,new Vector3(-.6f+side*1.25f,1,-2.1f)).gameObject.AddComponent<Light>();
                fill.type=LightType.Point;fill.range=4.5f;fill.intensity=side<0?2.1f:1.1f;fill.color=side<0?new Color(.88f,.95f,1):new Color(1,.84f,.72f);fill.shadows=LightShadows.None;
            }
            AddCharacterStudioReflections();
            UIRule(characterMenu,new Vector3(0,-1.25f,-.04f),4.98f);
            UIButton("Use my Meta Avatar","avatar:4",characterMenu,new Vector3(-1.32f,-1.53f,-.06f),new Vector2(2.35f,.39f));
            UIButton("Continue to arena  →","character-done",characterMenu,new Vector3(1.32f,-1.53f,-.06f),new Vector2(2.35f,.39f),true);
            characterSelected=UILabel("",characterMenu,new Vector3(1.66f,.80f,-.055f),.028f,UIMuted,false,TextAnchor.MiddleCenter);
            characterStatus=UILabel("",characterMenu,new Vector3(0,-1.99f,-.05f),.029f,UIInk,false,TextAnchor.MiddleCenter);
        }
        void AddCharacterStudioReflections()
        {
            // A small, static studio environment gives polished armor broad
            // softbox reflections without rendering six extra cameras per frame.
            const int size=64;
            characterStudioEnvironment=new Cubemap(size,TextureFormat.RGBAHalf,true)
                {name="Contender studio reflections",filterMode=FilterMode.Trilinear};
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=(x+.5f)/size*2-1,v=(y+.5f)/size*2-1;
                    Vector3 d;
                    switch(face)
                    {
                        case 0:d=new Vector3(1,-v,-u);break;case 1:d=new Vector3(-1,-v,u);break;
                        case 2:d=new Vector3(u,1,v);break;case 3:d=new Vector3(u,-1,-v);break;
                        case 4:d=new Vector3(u,-v,1);break;default:d=new Vector3(-u,-v,-1);break;
                    }
                    d.Normalize();
                    var baseLight=Color.Lerp(new Color(.025f,.035f,.055f),new Color(.20f,.25f,.34f),(d.y+1)*.5f);
                    float key=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.65f,.50f,-.58f).normalized)),26);
                    float rim=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(.70f,.20f,.68f).normalized)),44);
                    pixels[y*size+x]=baseLight+new Color(1.15f,1.27f,1.45f)*key+new Color(.95f,.57f,.36f)*rim;
                }
                characterStudioEnvironment.SetPixels(pixels,(CubemapFace)face);
            }
            characterStudioEnvironment.Apply(true,true);
            var probe=Group("Contender studio environment",characterMenu,new Vector3(-.48f,-.20f,-.5f)).gameObject.AddComponent<ReflectionProbe>();
            probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;probe.customBakedTexture=characterStudioEnvironment;
            probe.size=new Vector3(2.6f,2.4f,2.8f);probe.importance=10;probe.blendDistance=.2f;probe.intensity=1;
        }
        static string DisplayName(int index){var name=StareAvatarCatalog.Names[Mathf.Clamp(index,0,StareAvatarCatalog.ChoiceCount-1)];return index==StareAvatarCatalog.Meta?"Meta Avatar":name.Substring(0,1)+name.Substring(1).ToLowerInvariant();}
        public void ChangeCharacterPage(int direction)
        {
            int pages=(StareAvatarCatalog.PresetCount+3)/4;
            characterPage=(characterPage+direction%pages+pages)%pages;
            RefreshCharacterPage();
        }
        void RefreshCharacterPage()
        {
            if(characterPageLabel==null)return;
            characterPageLabel.text=characterPage==0?"ORIGINALS / 1 OF 2":"NEW / 2 OF 2";
            for(int row=0;row<characterCards.Count;row++)
            {
                int slot=characterPage*4+row, id=StareAvatarCatalog.PresetId(slot);
                characterCards[row].action="avatar:"+id;
                characterCards[row].gameObject.name=StareAvatarCatalog.Names[id]+" selection";
                characterNames[row].text=DisplayName(id);
                characterTaglines[row].text=StareAvatarCatalog.ShortDescriptions[id];
                characterNumbers[row].text=(slot+1).ToString("00");
            }
        }
        public void UpdateCharacterSelection(bool shown,int selected,string status)
        {
            if(profileName!=null){profileName.text="Character: "+DisplayName(selected);profileName.characterSize=.031f*.42f;}
            characterMenu.gameObject.SetActive(shown);
            if(!shown){characterWasShown=false;return;}
            selected=Mathf.Clamp(selected,0,StareAvatarCatalog.ChoiceCount-1);
            int selectedSlot=StareAvatarCatalog.PresetSlot(selected);
            if(!characterWasShown){if(selectedSlot>=0)characterPage=selectedSlot/4;RefreshCharacterPage();}
            characterWasShown=true;
            if(displayedCharacter!=selected)
            {
                if(selectedSlot>=0&&characterPage!=selectedSlot/4){characterPage=selectedSlot/4;RefreshCharacterPage();}
                displayedCharacter=selected;
                characterTitle.text=DisplayName(selected);
                characterRole.text=StareAvatarCatalog.Descriptions[selected];
                characterRole.color=StareAvatarCatalog.Colors[selected];
                characterBio.text=StareAvatarCatalog.Bios[selected];
                heroSurface.sharedMaterial.SetColor("_Accent",StareAvatarCatalog.Colors[selected]);
            }
            characterSelected.text=selectedSlot>=0?(selectedSlot+1).ToString("00")+" / 08   SELECTED":"META AVATAR SELECTED";
            characterStatus.text=string.IsNullOrEmpty(status)||status.EndsWith(" SELECTED")||status=="SELECT A CHARACTER, THEN CONTINUE"?"Select a contender, then continue. Your choice is saved.":status;
            metaPreview.gameObject.SetActive(selected==StareAvatarCatalog.Meta);
            for(int i=0;i<characterCards.Count;i++)
            {
                int id=StareAvatarCatalog.PresetId(characterPage*4+i);
                characterCards[i].SetSelected(selected==id);characterChecks[i].gameObject.SetActive(selected==id);
            }
            for(int i=0;i<characterPreviews.Count;i++)
            {
                var preview=characterPreviews[i];
                bool active=selected==StareAvatarCatalog.PresetId(i);preview.root.gameObject.SetActive(active);
                if(!active)continue;
                float blink=Mathf.PingPong(Time.unscaledTime+i*1.8f,6)>5.88f?1:0;
                preview.eyes.Set(blink,blink,Mathf.Sin(Time.unscaledTime*.4f)*4,0);
                preview.root.localRotation=Quaternion.Euler(0,-14+Mathf.Sin(Time.unscaledTime*.25f)*3,0);
            }
        }

        public StareMetaAvatarView MetaViewFor(int id)
        {
            return avatars.TryGetValue(id,out var value)?value.meta:null;
        }
    }
}
