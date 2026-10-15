using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Staredown
{
    // A sculptural audience made for the broad arena terraces. Each creature is
    // low resolution with smooth normals; all surfaces share fourteen batches.
    public static class StareReferenceCrowd
    {
        public static void Build(StareVisuals v, Transform parent)
        {
            var root = new GameObject("Reference audience / 160 terrace creatures").transform;
            root.SetParent(parent, false);
            var owner = root.gameObject.AddComponent<StareReferenceCrowdMeshes>();
            var sculpt = new CrowdSculpt(root, owner);
            Color[] palette = {
                new Color(.11f,.48f,.72f), new Color(.73f,.13f,.47f),
                new Color(.39f,.20f,.69f), new Color(.76f,.51f,.15f),
                new Color(.12f,.55f,.37f), new Color(.69f,.22f,.11f)
            };
            var shells = new Material[6]; var lights = new Material[6];
            for (int i = 0; i < 6; i++)
            {
                shells[i] = Finish(v.Mat("Audience / polished shell " + i, palette[i] * .74f), .43f, .26f);
                lights[i] = v.Mat("Audience / accent " + i, Color.Lerp(palette[i], Color.white, .35f), true);
            }
            var joint = Finish(v.Mat("Audience / graphite joints", new Color(.025f,.035f,.052f)), .62f, .55f);
            var trim = Finish(v.Mat("Audience / pale alloy", new Color(.56f,.65f,.72f)), .74f, .79f);
            int number = 0;
            for (int tier = 0; tier < 5; tier++)
            {
                float radius = 14.3f + 2.4f * tier;
                int rowCount = 24 + 4 * tier;
                for (int cluster = 0; cluster < 8; cluster++)
                {
                    int count = rowCount / 8 + (cluster < rowCount % 8 ? 1 : 0);
                    float angle = (cluster + .5f) * 45f + (tier % 2 == 0 ? 0f : 12f);
                    // The slight radial variation avoids a perfectly regimented row.
                    float a = angle * Mathf.Deg2Rad;
                    var clusterPos = new Vector3(Mathf.Sin(a) * radius, tier + 1.025f, Mathf.Cos(a) * radius);
                    var clusterRotation = Quaternion.Euler(0, angle, 0);
                    int clusterColor = (cluster * 5 + tier * 2) % 6;
                    sculpt.matrix = Matrix4x4.TRS(clusterPos, clusterRotation, Vector3.one);
                    sculpt.Disc(new Vector3(0,.014f,0), .39f + count * .29f, .71f, joint);
                    sculpt.Ring(new Vector3(0,.035f,0), .39f + count * .29f, .71f, .014f, lights[clusterColor]);
                    for (int member = 0; member < count; member++)
                    {
                        int type = (number * 7 + tier + cluster) % 4;
                        float seed = Fraction(Mathf.Sin((number + 1) * 73.17f) * 43758.5453f);
                        float height = .72f + seed * .79f;
                        // Occasional broader mecha and golems anchor each tier.
                        if (number % 23 == 3) { type = 0; height = 1.94f; }
                        else if (type == 0) height = 1.13f + seed * .48f;
                        else if (type == 1) height = 1.20f + seed * .48f;
                        float x = (member - (count - 1) * .5f) * .68f;
                        float z = (member % 2 == 0 ? -.16f : .18f) + (seed - .5f) * .18f;
                        var position = clusterPos + clusterRotation * new Vector3(x, .045f, z);
                        var rotation = clusterRotation * Quaternion.Euler(0, (seed - .5f) * 26f, 0);
                        // The sculpt's nominal height is 1.6 metres.
                        sculpt.matrix = Matrix4x4.TRS(position, rotation, Vector3.one * (height / 1.6f));
                        int color = (clusterColor + (member == 1 ? 2 : 0)) % 6;
                        switch (type)
                        {
                            case 0: Golem(sculpt, shells[color], joint, trim, lights[color]); break;
                            case 1: Mecha(sculpt, shells[color], joint, trim, lights[color], number % 3 == 0); break;
                            case 2: Fox(sculpt, shells[color], joint, trim, lights[color]); break;
                            default: Drake(sculpt, shells[color], joint, trim, lights[color]); break;
                        }
                        number++;
                    }
                }
            }
            sculpt.Finish();
        }

        static Material Finish(Material m, float smooth, float metallic)
        {
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }
        static float Fraction(float x) { return x - Mathf.Floor(x); }

        static void Golem(CrowdSculpt s, Material shell, Material dark, Material trim, Material light)
        {
            s.Oval(new Vector3(0,.86f,0), new Vector3(.77f,.76f,.49f), shell);
            s.Oval(new Vector3(0,.89f,-.215f), new Vector3(.53f,.42f,.10f), trim);
            s.Oval(new Vector3(0,1.28f,0), new Vector3(.42f,.37f,.38f), shell);
            s.Oval(new Vector3(0,1.29f,-.182f), new Vector3(.30f,.085f,.036f), light);
            s.Oval(new Vector3(0,.89f,-.28f), new Vector3(.105f,.105f,.045f), light);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Oval(new Vector3(side*.48f,1.05f,0), new Vector3(.37f,.39f,.43f), shell);
                s.Bone(new Vector3(side*.51f,.91f,0), new Vector3(side*.58f,.48f,-.07f), .13f,.16f, dark);
                s.Oval(new Vector3(side*.57f,.64f,-.05f), new Vector3(.28f,.35f,.31f), shell);
                s.Oval(new Vector3(side*.59f,.43f,-.09f), new Vector3(.25f,.22f,.29f), trim);
                s.Bone(new Vector3(side*.23f,.54f,0), new Vector3(side*.27f,.20f,-.02f), .14f,.13f, dark);
                s.Oval(new Vector3(side*.25f,.26f,-.01f), new Vector3(.28f,.35f,.31f), shell);
                s.Oval(new Vector3(side*.27f,.105f,-.12f), new Vector3(.32f,.19f,.43f), shell);
                s.Oval(new Vector3(side*.48f,1.10f,-.213f), new Vector3(.10f,.10f,.032f), light);
            }
        }

        static void Mecha(CrowdSculpt s, Material shell, Material dark, Material trim, Material light, bool cheer)
        {
            s.Oval(new Vector3(0,.91f,0), new Vector3(.39f,.44f,.29f), shell);
            s.Oval(new Vector3(0,.71f,0), new Vector3(.24f,.20f,.23f), dark);
            s.Oval(new Vector3(0,1.32f,0), new Vector3(.38f,.39f,.34f), shell);
            s.Oval(new Vector3(0,1.36f,-.166f), new Vector3(.28f,.073f,.031f), light);
            s.Bone(new Vector3(0,1.48f,.025f), new Vector3(.05f,1.67f,.045f), .028f,.011f, trim);
            s.Oval(new Vector3(0,.96f,-.153f), new Vector3(.19f,.20f,.04f), trim);
            s.Oval(new Vector3(0,.96f,-.182f), new Vector3(.07f,.095f,.027f), light);
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = new Vector3(side*.25f,1.03f,0);
                var elbow = new Vector3(side*.37f,cheer && side == 1 ? 1.29f : .80f,0);
                var wrist = new Vector3(side*.40f,cheer && side == 1 ? 1.61f : .60f,-.07f);
                s.Oval(shoulder,new Vector3(.24f,.23f,.25f),shell);
                s.Bone(shoulder,elbow,.065f,.055f,dark);
                s.Bone(elbow,wrist,.084f,.06f,shell);
                s.Oval(wrist,new Vector3(.11f,.14f,.12f),trim);
                s.Bone(new Vector3(side*.115f,.69f,0),new Vector3(side*.13f,.39f,0),.09f,.061f,shell);
                s.Oval(new Vector3(side*.13f,.37f,0),new Vector3(.13f,.13f,.14f),dark);
                s.Bone(new Vector3(side*.13f,.34f,0),new Vector3(side*.15f,.10f,0),.075f,.056f,trim);
                s.Oval(new Vector3(side*.15f,.075f,-.075f),new Vector3(.17f,.12f,.29f),shell);
            }
        }

        static void Fox(CrowdSculpt s, Material shell, Material dark, Material trim, Material light)
        {
            s.Oval(new Vector3(0,.74f,0),new Vector3(.37f,.54f,.31f),shell);
            s.Oval(new Vector3(0,1.18f,0),new Vector3(.57f,.46f,.43f),shell);
            s.Oval(new Vector3(0,1.06f,-.204f),new Vector3(.31f,.16f,.25f),trim);
            s.Oval(new Vector3(0,1.09f,-.322f),new Vector3(.09f,.07f,.067f),dark);
            for (int side=-1;side<=1;side+=2)
            {
                s.Leaf(new Vector3(side*.18f,1.34f,0),new Vector3(side*.27f,1.64f,.025f),.20f,.10f,shell);
                s.Oval(new Vector3(side*.146f,1.21f,-.205f),new Vector3(.13f,.085f,.04f),light);
                s.Bone(new Vector3(side*.23f,.91f,0),new Vector3(side*.28f,.56f,-.055f),.065f,.058f,shell);
                s.Bone(new Vector3(side*.11f,.51f,0),new Vector3(side*.14f,.14f,0),.075f,.06f,dark);
                s.Oval(new Vector3(side*.14f,.10f,-.06f),new Vector3(.18f,.16f,.29f),shell);
            }
            s.Bone(new Vector3(0,.54f,.13f),new Vector3(.26f,.40f,.36f),.075f,.14f,shell);
            s.Bone(new Vector3(.26f,.40f,.36f),new Vector3(.48f,.60f,.42f),.14f,.14f,shell);
            s.Bone(new Vector3(.48f,.60f,.42f),new Vector3(.47f,.86f,.30f),.14f,.005f,trim);
        }

        static void Drake(CrowdSculpt s, Material shell, Material dark, Material trim, Material light)
        {
            s.Oval(new Vector3(0,.74f,0),new Vector3(.46f,.63f,.37f),shell);
            s.Oval(new Vector3(0,.76f,-.17f),new Vector3(.28f,.40f,.065f),trim);
            s.Oval(new Vector3(0,1.21f,0),new Vector3(.51f,.46f,.41f),shell);
            s.Oval(new Vector3(0,1.07f,-.21f),new Vector3(.34f,.18f,.25f),shell);
            for(int side=-1;side<=1;side+=2)
            {
                s.Bone(new Vector3(side*.17f,1.38f,.06f),new Vector3(side*.25f,1.62f,.16f),.061f,.003f,trim);
                s.Oval(new Vector3(side*.143f,1.22f,-.195f),new Vector3(.12f,.085f,.042f),light);
                s.Bone(new Vector3(side*.23f,.94f,0),new Vector3(side*.31f,.59f,-.08f),.075f,.063f,shell);
                s.Bone(new Vector3(side*.13f,.49f,0),new Vector3(side*.19f,.13f,-.01f),.11f,.074f,shell);
                s.Oval(new Vector3(side*.19f,.09f,-.10f),new Vector3(.24f,.16f,.33f),shell);
                var shoulder=new Vector3(side*.18f,.97f,.13f);
                var elbow=new Vector3(side*.40f,1.20f,.21f);
                var tip=new Vector3(side*.67f,1.35f,.36f);
                var bottom=new Vector3(side*.58f,.66f,.37f);
                var hip=new Vector3(side*.18f,.64f,.13f);
                s.Wing(new[]{shoulder,elbow,tip,new Vector3(side*.54f,1.00f,.35f),bottom,new Vector3(side*.33f,.78f,.26f),hip},shell);
                s.Bone(shoulder,elbow,.034f,.029f,trim);
                s.Bone(elbow,tip,.029f,.004f,trim);
                s.Bone(elbow,bottom,.018f,.002f,trim);
            }
            s.Bone(new Vector3(0,.49f,.15f),new Vector3(-.23f,.21f,.43f),.078f,.055f,shell);
            s.Bone(new Vector3(-.23f,.21f,.43f),new Vector3(-.48f,.33f,.44f),.055f,.002f,shell);
        }

        sealed class Surface
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            public readonly List<Vector3> normals=new List<Vector3>();
            public readonly List<int> triangles=new List<int>();
            public void Tri(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        }
        sealed class CrowdSculpt
        {
            readonly Transform root; readonly StareReferenceCrowdMeshes owner;
            readonly Dictionary<Material,Surface> surfaces=new Dictionary<Material,Surface>();
            public Matrix4x4 matrix=Matrix4x4.identity;
            public CrowdSculpt(Transform root,StareReferenceCrowdMeshes owner){this.root=root;this.owner=owner;}
            Surface Get(Material m){if(!surfaces.TryGetValue(m,out var s)){s=new Surface();surfaces.Add(m,s);}return s;}
            void Vertex(Surface s,Vector3 p,Vector3 n){s.vertices.Add(matrix.MultiplyPoint3x4(p));s.normals.Add(matrix.MultiplyVector(n).normalized);}
            public void Oval(Vector3 center,Vector3 size,Material m)
            {
                const int cols=8,rows=4;var s=Get(m);int start=s.vertices.Count;
                for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                {
                    float a=Mathf.PI*y/rows,b=2*Mathf.PI*x/cols;
                    var n=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));
                    Vertex(s,center+Vector3.Scale(n,size)*.5f,new Vector3(n.x/size.x,n.y/size.y,n.z/size.z).normalized);
                }
                for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
                {int a=start+y*(cols+1)+x,b=a+cols+1;s.Tri(a,a+1,b);s.Tri(a+1,b+1,b);}
            }
            public void Bone(Vector3 from,Vector3 to,float r0,float r1,Material m)
            {
                const int sides=6;var s=Get(m);int start=s.vertices.Count;
                Vector3 axis=(to-from).normalized;
                Vector3 right=Vector3.Cross(Mathf.Abs(axis.y)>.92f?Vector3.forward:Vector3.up,axis).normalized;
                Vector3 forward=Vector3.Cross(axis,right);
                for(int end=0;end<2;end++)for(int k=0;k<sides;k++)
                {float a=2*Mathf.PI*k/sides;Vector3 n=right*Mathf.Cos(a)+forward*Mathf.Sin(a);Vertex(s,(end==0?from:to)+n*(end==0?r0:r1),n);}
                for(int k=0;k<sides;k++){int a=start+k,b=start+(k+1)%sides;s.Tri(a,b,a+sides);s.Tri(b,b+sides,a+sides);}
                int cap=s.vertices.Count;Vertex(s,from,-axis);Vertex(s,to,axis);
                for(int k=0;k<sides;k++){int a=start+k,b=start+(k+1)%sides;s.Tri(cap,b,a);s.Tri(cap+1,a+sides,b+sides);}
            }
            void Face(Surface s,Vector3 a,Vector3 b,Vector3 c)
            {int n=s.vertices.Count;var normal=Vector3.Cross(b-a,c-a).normalized;Vertex(s,a,normal);Vertex(s,b,normal);Vertex(s,c,normal);s.Tri(n,n+1,n+2);}
            public void Leaf(Vector3 from,Vector3 to,float width,float depth,Material m)
            {
                var s=Get(m);var along=(to-from).normalized;var right=Vector3.Cross(along,Vector3.forward).normalized;
                var normal=Vector3.Cross(right,along).normalized;
                Vector3 a=from-right*width*.5f,b=from+right*width*.5f,c=Vector3.Lerp(from,to,.35f)+normal*depth*.5f,d=Vector3.Lerp(from,to,.35f)-normal*depth*.5f;
                Face(s,a,to,c);Face(s,to,b,c);Face(s,b,a,c);Face(s,to,a,d);Face(s,b,to,d);Face(s,a,b,d);
            }
            public void Wing(Vector3[] edge,Material m)
            {
                Vector3 center=Vector3.zero;foreach(var p in edge)center+=p;center/=edge.Length;var s=Get(m);
                for(int i=0;i<edge.Length;i++){Face(s,center,edge[i],edge[(i+1)%edge.Length]);Face(s,center,edge[(i+1)%edge.Length],edge[i]);}
            }
            public void Disc(Vector3 center,float rx,float rz,Material m)
            {
                const int sides=24;var s=Get(m);int n=s.vertices.Count;Vertex(s,center,Vector3.up);
                for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;Vertex(s,center+new Vector3(Mathf.Sin(a)*rx,0,Mathf.Cos(a)*rz),Vector3.up);}
                for(int i=0;i<sides;i++)s.Tri(n,n+1+i,n+1+(i+1)%sides);
            }
            public void Ring(Vector3 center,float rx,float rz,float width,Material m)
            {
                const int sides=32;var s=Get(m);int n=s.vertices.Count;
                for(int i=0;i<sides;i++)for(int j=0;j<2;j++)
                {float a=i*Mathf.PI*2/sides;float d=j==0?-width:width;Vertex(s,center+new Vector3(Mathf.Sin(a)*(rx+d),0,Mathf.Cos(a)*(rz+d)),Vector3.up);}
                for(int i=0;i<sides;i++){int a=n+i*2,b=n+(i+1)%sides*2;s.Tri(a,a+1,b);s.Tri(a+1,b+1,b);}
            }
            public void Finish()
            {
                foreach(var pair in surfaces)
                {
                    var mesh=new Mesh{name="Terrace audience / "+pair.Key.name};
                    if(pair.Value.vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                    mesh.SetVertices(pair.Value.vertices);mesh.SetNormals(pair.Value.normals);mesh.SetTriangles(pair.Value.triangles,0);mesh.RecalculateBounds();owner.meshes.Add(mesh);
                    var g=new GameObject(pair.Key.name);g.transform.SetParent(root,false);g.isStatic=true;
                    g.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                }
            }
        }
    }
    public sealed class StareReferenceCrowdMeshes : MonoBehaviour
    {
        public readonly List<Mesh> meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
