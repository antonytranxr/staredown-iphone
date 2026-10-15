using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Staredown
{
    public sealed partial class StareVisuals
    {
        void BuildChampionshipSetting()
        {
            var sky = Resources.Load<Material>("Environment/ChampionshipSky");
            if (sky != null) RenderSettings.skybox = sky;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.GetComponentInParent<OVRCameraRig>() != null)
                { camera.clearFlags = CameraClearFlags.Skybox; camera.farClipPlane = Mathf.Max(camera.farClipPlane, 500); }

            var landscape = Group("Championship / distant alpine amphitheatre", transform, Vector3.zero);
            var backdrop = new Material(Resources.Load<Shader>("StareBackdrop")) {name="Twilight mountain atmosphere"};
            materials["Mountain backdrop"] = backdrop;
            // Three static, vertex-colored silhouettes. No terrain textures,
            // colliders, additional lights, or runtime cloud simulation.
            MountainRange(landscape, 285, 155, -10, new Color(.25f,.30f,.43f), backdrop, 31);
            MountainRange(landscape, 220, 125, -13, new Color(.17f,.22f,.32f), backdrop, 17);
            MountainRange(landscape, 155, 85, -12, new Color(.10f,.145f,.22f), backdrop, 9);

            var crown = Group("Championship / titanium stadium crown", architecture, Vector3.zero);
            var metal = Mat("Crown / satin titanium", new Color(.23f,.27f,.34f));
            metal.SetFloat("_Metallic",.68f); metal.SetFloat("_Glossiness",.52f);
            var dark = Mat("Crown / ceramic graphite",new Color(.043f,.058f,.085f));
            dark.SetFloat("_Metallic",.28f);dark.SetFloat("_Glossiness",.38f);
            var light = Mat("Crown / pearl-white light",new Color(.68f,.81f,.94f),true);
            light.SetColor("_EmissionColor",new Color(.68f,.81f,.94f)*.85f);
            for (int i=0;i<12;i++)
            {
                float a=i*30+15;
                Bar("Splayed structural pylon",crown,Radial(a,26.4f,5.1f),Radial(a,27.6f,12.2f),.38f,metal);
                Bar("Diagonal flying buttress",crown,Radial(a,28.1f,7.8f),Radial(a,25.5f,12.6f),.24f,dark);
                Bar("Inlaid pylon lamp",crown,Radial(a,26.50f,6.3f),Radial(a,27.18f,10.7f),.035f,light);
                var fixture=Group("Precision stadium floodlight",crown,Radial(a,25.65f,12.8f));
                fixture.localRotation=Quaternion.Euler(18,a,0);
                Shape(PrimitiveType.Cube,"Floodlight housing",fixture,Vector3.zero,new Vector3(1.05f,.17f,.45f),dark);
                Shape(PrimitiveType.Cube,"Diffused floodlight lens",fixture,new Vector3(0,-.10f,-.08f),new Vector3(.88f,.025f,.29f),light);
            }
            for(int quarter=0;quarter<4;quarter++)
            {
                float start=quarter*90+8;
                Revolve("Open crown / upper flange",crown,new[]{new Vector2(25.45f,12.18f),new Vector2(25.45f,12.67f),new Vector2(25.52f,12.75f),new Vector2(26.10f,12.75f),new Vector2(26.18f,12.67f),new Vector2(26.18f,12.18f),new Vector2(25.45f,12.18f)},metal,48,start,74);
                Revolve("Open crown / lower reveal",crown,new[]{new Vector2(25.435f,12.27f),new Vector2(25.435f,12.32f),new Vector2(25.51f,12.32f)},light,48,start,74);
            }
            // Batch the manufactured detail by material. Decorative geometry
            // never participates in hand-pointer or locomotion physics.
            BatchCrown(crown);
            for(int i=0;i<4;i++)
            {
                float a=i*90+45;
                var plaque=Group("Arena identity / concourse",architecture,Radial(a,25.18f,7.15f));
                plaque.localRotation=Quaternion.Euler(0,a,0);
                Shape(PrimitiveType.Cube,"Recessed broadcast identity plate",plaque,Vector3.zero,new Vector3(3.6f,.72f,.16f),dark);
                for(int side=-1;side<=1;side+=2)Shape(PrimitiveType.Cube,"Identity plate stanchion",plaque,new Vector3(side*1.5f,-.65f,.1f),new Vector3(.075f,.8f,.075f),metal);
                Text("S T A R E D O W N",plaque,new Vector3(0,.10f,-.09f),.09f,new Color(.77f,.85f,.94f));
                Text("C H A M P I O N S H I P   A R E N A",plaque,new Vector3(0,-.19f,-.09f),.025f,new Color(.46f,.62f,.75f));
            }
        }

        void MountainRange(Transform parent,float radius,float height,float baseY,Color color,Material material,float seed)
        {
            const int n=360,rows=32;
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            for(int i=0;i<=n;i++)
            {
                float a=i*Mathf.PI*2/n,s=Mathf.Sin(a),c=Mathf.Cos(a);
                for(int row=0;row<=rows;row++)
                {
                    float t=row/(float)rows;
                    float r=radius-70+t*140;
                    float px=s*r/24+seed,pz=c*r/24+seed;
                    float mass=Mathf.PerlinNoise(px*.32f,pz*.32f);
                    float ridge=1-Mathf.Abs(Mathf.PerlinNoise(px,pz)*2-1);
                    float erosion=Mathf.PerlinNoise(px*3.5f,pz*3.5f);
                    float envelope=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.85f);
                    float elevation=Mathf.Pow(ridge,2)*.48f+mass*.68f+erosion*.025f;
                    float y=baseY+height*elevation*envelope;
                    vertices.Add(new Vector3(s*r,y,c*r));
                    float lit=.60f+.23f*Mathf.Max(0,s*.5f+c*.65f)+erosion*.22f;
                    var col=color*lit;
                    if(y>baseY+height*.82f)col=Color.Lerp(col,new Color(.39f,.44f,.56f),Mathf.InverseLerp(.82f,1.05f,(y-baseY)/height)*.65f);
                    col.a=1;colors.Add(col.linear);
                }
            }
            for(int i=0;i<n;i++)for(int row=0;row<rows;row++)
            {
                int a=i*(rows+1)+row,b=a+rows+1;
                triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
            var go=MeshObject("Atmospheric alpine ridge / "+seed,parent,vertices,triangles,material,colors);
            var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        void BatchCrown(Transform parent)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            var originals=new List<GameObject>();
            foreach(var filter in parent.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
                if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups[material]=list;}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=parent.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                originals.Add(filter.gameObject);renderer.enabled=false;
            }
            foreach(var entry in groups)
            {
                var mesh=new Mesh{name=entry.Key.name+" / combined",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(entry.Value.ToArray(),true,true);artMeshes.Add(mesh);
                var go=Group(mesh.name,parent,Vector3.zero).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=entry.Key;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            foreach(var go in originals)Destroy(go);
        }
    }
}
