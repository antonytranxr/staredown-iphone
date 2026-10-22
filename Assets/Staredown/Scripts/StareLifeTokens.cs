using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Staredown
{
    public sealed partial class StareVisuals
    {
        Mesh lifeTokenMesh;Material activeLifeMaterial,depletedLifeMaterial;
        // A shared, closed faceted solid: one small mesh per life, no lights,
        // particle systems or extra cameras. Its bevel catches arena lighting.
        Transform BuildLifeToken(Transform parent,string name,float scale)
        {
            if(lifeTokenMesh==null)
            {
                const int sides=40;var outline=new Vector3[sides];
                for(int i=0;i<sides;i++)
                {
                    float t=i*Mathf.PI*2/sides;
                    outline[i]=new Vector3(.5f*Mathf.Pow(Mathf.Sin(t),3),
                        (13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t))/32+.075f,0);
                }
                var vertices=new List<Vector3>();var triangles=new List<int>();
                void Triangle(Vector3 a,Vector3 b,Vector3 c){int at=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(at);triangles.Add(at+1);triangles.Add(at+2);}
                for(int i=0;i<sides;i++)
                {
                    int j=(i+1)%sides;var a=outline[i];var b=outline[j];
                    var frontA=a*.79f+Vector3.back*.13f;var frontB=b*.79f+Vector3.back*.13f;
                    var backA=a*.79f+Vector3.forward*.13f;var backB=b*.79f+Vector3.forward*.13f;
                    Triangle(Vector3.back*.22f,frontA,frontB);
                    Triangle(frontA,a,b);Triangle(frontA,b,frontB);
                    Triangle(Vector3.forward*.22f,backB,backA);
                    Triangle(backA,backB,b);Triangle(backA,b,a);
                }
                lifeTokenMesh=new Mesh{name="Shared beveled 3D life heart"};lifeTokenMesh.SetVertices(vertices);lifeTokenMesh.SetTriangles(triangles,0);lifeTokenMesh.RecalculateNormals();lifeTokenMesh.RecalculateBounds();artMeshes.Add(lifeTokenMesh);
            }
            var node=Group(name,parent,Vector3.zero);node.localScale=Vector3.one*scale;
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=lifeTokenMesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=LifeTokenMaterial(true);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return node;
        }
        Material LifeTokenMaterial(bool alive)
        {
            if(alive&&activeLifeMaterial!=null)return activeLifeMaterial;
            if(!alive&&depletedLifeMaterial!=null)return depletedLifeMaterial;
            var mat=Mat(alive?"Life crystal coral":"Life crystal depleted",alive?new Color(1,.13f,.26f):new Color(.06f,.09f,.12f));
            if(alive){mat.SetFloat("_Metallic",.45f);mat.SetFloat("_Glossiness",.86f);mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.45f,.025f,.055f));}
            if(alive)activeLifeMaterial=mat;else depletedLifeMaterial=mat;
            return mat;
        }
        void PresentLifeToken(CreatureView avatar,Fighter fighter,ArenaState state)
        {
            if(avatar.lifeToken==null)avatar.lifeToken=BuildLifeToken(avatar.root,"One life / 3D heart",.27f);
            bool alive=fighter.alive&&fighter.lives>0;
            if(avatar.lifeObserved&&avatar.lifeAlive&&!alive)avatar.lifeLostAt=state.clock;
            if(!avatar.lifeObserved)avatar.lifeLostAt=state.clock-2;
            avatar.lifeObserved=true;avatar.lifeAlive=alive;
            float loss=alive?0:Mathf.Clamp01((state.clock-avatar.lifeLostAt)/.55f);
            avatar.lifeToken.gameObject.SetActive(alive||loss<1);
            // Above each model's own nameplate, safely outside the face and
            // first-person view. The same local life is visible on the board.
            float top=avatar.label!=null?avatar.label.transform.localPosition.y:2.2f;
            avatar.lifeToken.localPosition=new Vector3(0,top+.32f+.018f*Mathf.Sin(state.clock*1.7f+fighter.id)+loss*.3f,0);
            avatar.lifeToken.localRotation=Quaternion.Euler(0,Mathf.Sin(state.clock*.85f+fighter.id)*23+loss*130,loss*25);
            avatar.lifeToken.localScale=Vector3.one*(.27f*(1-loss));
        }
    }
}
