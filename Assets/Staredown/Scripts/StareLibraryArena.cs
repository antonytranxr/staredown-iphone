using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Staredown
{
    public sealed partial class StareVisuals
    {
        Transform sunstoneArena;
        bool missingSunstoneLogged;

        bool EnsureSunstoneArena()
        {
            if(sunstoneArena!=null)return true;
            var model=Resources.Load<GameObject>("LibraryArena/SunstoneArena");
            var palette=Resources.Load<Texture2D>("LibraryArena/colormap");
            if(model==null||palette==null)
            {
                if(!missingSunstoneLogged)
                {
                    Debug.LogError("Sunstone arena model or palette is missing; keeping the current arena.");
                    missingSunstoneLogged=true;
                }
                return false;
            }
            var root=Group("Sunstone / library arena",transform,Vector3.zero);
            root.gameObject.SetActive(false);
            var shell=Instantiate(model,root);
            shell.name="Sunstone / imported Kenney arena";
            palette.filterMode=FilterMode.Point;
            var material=Mat("Sunstone / stone",Color.white);
            material.mainTexture=palette;
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",palette);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.20f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.20f);
            foreach(var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots=new Material[renderer.sharedMaterials.Length];
                for(int i=0;i<slots.Length;i++)slots[i]=material;
                renderer.sharedMaterials=slots;
                // The environment is static; only contestants and gameplay effects cast attention.
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                var filter=renderer.GetComponent<MeshFilter>();
                if(renderer.name.IndexOf("Sunstone_Floor",StringComparison.OrdinalIgnoreCase)>=0&&filter!=null)
                {
                    var collider=renderer.GetComponent<MeshCollider>();
                    if(collider==null)collider=renderer.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh=filter.sharedMesh;
                }
            }
            sunstoneArena=root;
            missingSunstoneLogged=false;
            return true;
        }
    }
}
