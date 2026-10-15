using System.Collections.Generic;
using UnityEngine;

namespace Staredown
{
    public sealed partial class StareVisuals
    {
        public int CurrentArenaTheme { get; private set; } = -1;
        Material moonlitSky, daylightSky;
        Transform championshipLandscape;
        Color nightSkyAmbient, nightEquatorAmbient, nightGroundAmbient, nightFog;
        float nightFogDensity;
        readonly List<ArenaMaterialPalette> arenaPalette = new List<ArenaMaterialPalette>();
        readonly List<ArenaRidgePalette> ridgePalette = new List<ArenaRidgePalette>();
        readonly List<ArenaLightPalette> lightPalette = new List<ArenaLightPalette>();

        sealed class ArenaMaterialPalette
        {
            public Material material;
            public Color night, emission, day;
            public bool emissive;
            public readonly Dictionary<string,float> values = new Dictionary<string,float>();
        }
        sealed class ArenaRidgePalette { public Mesh mesh; public Color[] night, day; public Vector3[] nightVertices,dayVertices,nightNormals,dayNormals; }
        sealed class ArenaLightPalette { public Light light; public Color color; public float intensity; public Quaternion rotation; }

        void CaptureArenaPalette()
        {
            moonlitSky = RenderSettings.skybox;
            daylightSky = Resources.Load<Material>("Environment/FestivalSky");
            nightSkyAmbient=RenderSettings.ambientSkyColor;nightEquatorAmbient=RenderSettings.ambientEquatorColor;
            nightGroundAmbient=RenderSettings.ambientGroundColor;nightFog=RenderSettings.fogColor;nightFogDensity=RenderSettings.fogDensity;
            foreach(var entry in materials)
            {
                string key=entry.Key;
                // UI and contestant materials remain independent of the environment palette.
                if(!key.StartsWith("Reference ")&&!key.StartsWith("Crown / ")&&key!="reference floor")continue;
                var m=entry.Value;if(!m.HasProperty("_Color"))continue;
                Color day=m.color;
                switch(key)
                {
                    case "reference floor": day=new Color(.24f,.48f,.56f);break;
                    case "Reference grandstand graphite": day=new Color(.18f,.39f,.61f);break;
                    case "Reference warm titanium": day=new Color(.88f,.45f,.20f);break;
                    case "Reference trim": day=new Color(.75f,.85f,.91f);break;
                    case "Reference recess": day=new Color(.055f,.15f,.24f);break;
                    case "Reference ice": day=new Color(.14f,.83f,.97f);break;
                    case "Reference amber": day=new Color(1,.70f,.13f);break;
                    case "Reference violet": day=new Color(1,.32f,.23f);break;
                    case "Reference floor champagne": day=new Color(1,.84f,.30f);break;
                    case "Reference pad rim": day=new Color(.11f,.25f,.34f);break;
                    case "Crown / satin titanium": day=new Color(.79f,.86f,.91f);break;
                    case "Crown / ceramic graphite": day=new Color(.14f,.34f,.53f);break;
                    case "Crown / pearl-white light": day=new Color(1,.94f,.76f);break;
                }
                if(key.StartsWith("Reference pad enamel"))day=Color.Lerp(m.color,Color.white,.36f);
                var p=new ArenaMaterialPalette{material=m,night=m.color,day=day,emissive=m.IsKeywordEnabled("_EMISSION"),emission=m.HasProperty("_EmissionColor")?m.GetColor("_EmissionColor"):Color.black};
                foreach(string property in new[]{"_Metallic","_Glossiness","_Smoothness","_Reflectivity"})
                    if(m.HasProperty(property))p.values[property]=m.GetFloat(property);
                arenaPalette.Add(p);
            }
            championshipLandscape=null;
            foreach(Transform child in transform)if(child.name=="Championship / distant alpine amphitheatre"){championshipLandscape=child;break;}
            Color[] low={new Color(.70f,.84f,.88f),new Color(.42f,.69f,.51f),new Color(.32f,.61f,.28f)};
            if(championshipLandscape!=null)
            {
                int layer=0;
                foreach(var filter in championshipLandscape.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var original=mesh.colors;var day=new Color[original.Length];var vertices=mesh.vertices;var dayVertices=(Vector3[])vertices.Clone();var normals=mesh.normals;
                    float peak=mesh.bounds.max.y;
                    float maximum=.001f;foreach(var c in original)maximum=Mathf.Max(maximum,c.grayscale);
                    for(int i=0;i<day.Length;i++)
                    {
                        float shade=Mathf.Lerp(.82f,1,original[i].grayscale/maximum);
                        dayVertices[i].y=vertices[i].y*.46f;
                        float snow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(peak*.72f,peak,vertices[i].y));
                        day[i]=Color.Lerp(low[Mathf.Min(layer,2)].linear,new Color(.94f,.97f,1).linear,snow*.72f)*shade;day[i].a=1;
                    }
                    mesh.vertices=dayVertices;mesh.RecalculateNormals();var dayNormals=mesh.normals;
                    mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
                    ridgePalette.Add(new ArenaRidgePalette{mesh=mesh,night=original,day=day,nightVertices=vertices,dayVertices=dayVertices,nightNormals=normals,dayNormals=dayNormals});layer++;
                }
            }
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional)lightPalette.Add(new ArenaLightPalette{light=light,color=light.color,intensity=light.intensity,rotation=light.transform.rotation});
        }

        public void SetArenaTheme(int theme)
        {
            theme=Mathf.Clamp(theme,0,2);
            if(CurrentArenaTheme==theme)return;
            if(CurrentArenaTheme<0)CaptureArenaPalette();
            bool bright=theme!=1;
            if(bright&&daylightSky==null){Debug.LogError("Bright arena sky is missing");return;}
            if(theme==2&&!EnsureSunstoneArena())return;
            CurrentArenaTheme=theme;
            if(architecture!=null)architecture.gameObject.SetActive(theme!=2);
            if(championshipLandscape!=null)championshipLandscape.gameObject.SetActive(theme!=2);
            if(sunstoneArena!=null)sunstoneArena.gameObject.SetActive(theme==2);
            if(floorReflection!=null)floorReflection.enabled=theme!=2;
            RenderSettings.skybox=bright?daylightSky:moonlitSky;
            RenderSettings.ambientSkyColor=bright?new Color(.63f,.78f,.92f):nightSkyAmbient;
            RenderSettings.ambientEquatorColor=bright?new Color(.44f,.57f,.66f):nightEquatorAmbient;
            RenderSettings.ambientGroundColor=bright?new Color(.23f,.32f,.30f):nightGroundAmbient;
            RenderSettings.fogColor=bright?new Color(.64f,.81f,.89f):nightFog;
            RenderSettings.fogDensity=bright?.0016f:nightFogDensity;
            foreach(var p in arenaPalette)
            {
                var m=p.material;m.color=bright?p.day:p.night;
                if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",m.color);
                if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",bright&&p.emissive?p.day*.65f:p.emission);
                foreach(var v in p.values)m.SetFloat(v.Key,v.Value);
                if(bright)
                {
                    if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",Mathf.Min(p.values["_Metallic"],.26f));
                    if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.48f);
                    if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.60f);
                    if(m.HasProperty("_Reflectivity"))m.SetFloat("_Reflectivity",.16f);
                }
            }
            foreach(var p in ridgePalette){p.mesh.colors=bright?p.day:p.night;p.mesh.vertices=bright?p.dayVertices:p.nightVertices;p.mesh.normals=bright?p.dayNormals:p.nightNormals;p.mesh.RecalculateBounds();}
            foreach(var p in lightPalette)
            {
                bool fill=p.light.name=="Warm rim softbox";
                p.light.color=bright?(fill?new Color(.67f,.84f,1):new Color(1,.95f,.83f)):p.color;
                p.light.intensity=bright?(fill?.42f:1.30f):p.intensity;
                p.light.transform.rotation=bright?Quaternion.Euler(fill?30:55,fill?135:-25,0):p.rotation;
            }
            // One capture after a theme switch; never a per-frame reflection update.
            CancelInvoke(nameof(CaptureArenaReflection));Invoke(nameof(CaptureArenaReflection),.15f);
        }
    }
}
