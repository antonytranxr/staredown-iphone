using System.Collections.Generic;
using UnityEngine;
namespace Staredown
{
    // Cosmetic presentation only: the tournament and the player's rig keep their authoritative seats.
    public sealed class StareContestantMotion : MonoBehaviour
    {
        const float ExitDuration=1.05f;
        sealed class Surface {public Renderer renderer;public Material[] original,dissolve;}
        readonly List<Surface> surfaces=new List<Surface>();
        MaterialPropertyBlock properties;
        Transform model;Vector3 baseScale;Quaternion baseRotation;Animator animator;Vector3 lastMotionPosition;bool sampledMotion;string animationState;float movement;SkinnedMeshRenderer[] skins;bool winnerPresentation;
        public string LastAnimation=>animationState;
        Vector3 exitPosition;Quaternion exitRotation;
        float eliminatedAt=-10,advancedAt=-10;bool seen,wasAlive,personal,dissolving;int wins,previousPhase=-1;
        public bool Eliminating=>dissolving;
        public float ExitProgress=>Mathf.Clamp01((Time.unscaledTime-eliminatedAt)/ExitDuration);
        public void Bind(Transform preset,bool meta)
        {properties=new MaterialPropertyBlock();model=preset;baseScale=model.localScale;baseRotation=model.localRotation;personal=meta;animator=model.GetComponent<Animator>();skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);}
        public void Present(Fighter fighter,ArenaState state,Vector3 seat,Quaternion facing)
        {
            if(seen&&wasAlive&&!fighter.alive)
            {
                exitPosition=transform.position;exitRotation=transform.rotation;eliminatedAt=Time.unscaledTime;
                BeginDissolve();
            }
            if(fighter.alive&&(!wasAlive||state.phase==(int)MatchPhase.Lobby))Restore();
            if(seen&&fighter.roundWins>wins)advancedAt=Time.unscaledTime;
            wasAlive=fighter.alive;seen=true;wins=fighter.roundWins;
            float t=ExitProgress;
            if(dissolving&&t>=1)Restore();
            bool social=(state.phase==(int)MatchPhase.Lobby||!fighter.alive)&&fighter.hasRoomPose;
            if(social&&previousPhase==state.phase&&!dissolving)
            {float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);transform.SetPositionAndRotation(Vector3.Lerp(transform.position,seat,blend),Quaternion.Slerp(transform.rotation,facing,blend));}
            else transform.SetPositionAndRotation(dissolving?exitPosition:seat,dissolving?exitRotation:facing);
            previousPhase=state.phase;
            if(model==null)return;
            if(dissolving)
            {
                if(animator!=null)animator.speed=0;
                // Brief impact beat, then a rising dissolve; the avatar never teleports mid-effect.
                float progress=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.13f,.94f,t));
                properties.SetFloat("_Progress",progress);
                foreach(var surface in surfaces)if(surface.renderer!=null)surface.renderer.SetPropertyBlock(properties);
                if(!personal){model.localScale=Vector3.Scale(baseScale,new Vector3(1+.025f*Mathf.Sin(t*12),1-.025f*Mathf.Sin(t*12),1));model.localRotation=baseRotation*Quaternion.Euler(0,0,Mathf.Sin(t*6)*2.5f);}
                return;
            }
            // The preset skeleton also supplies animated hand targets for a dancing Meta avatar.
            if(personal&&!fighter.dancing)return;
            if(animator!=null&&animator.isActiveAndEnabled&&animator.runtimeAnimatorController!=null)
            {
                float dt=Mathf.Max(.005f,Time.unscaledDeltaTime);
                float distance=sampledMotion?Vector3.Distance(transform.position,lastMotionPosition):0;
                lastMotionPosition=transform.position;sampledMotion=true;
                float speed=social&&distance<.75f?distance/dt:0;
                movement=Mathf.MoveTowards(movement,speed,dt*8);
                bool rigChampion=state.phase==(int)MatchPhase.Champion&&state.winner==fighter.id;
                bool celebrate=fighter.bot&&(rigChampion||fighter.dancing||fighter.alive&&Time.unscaledTime-advancedAt<2.4f);
                if(winnerPresentation!=celebrate)
                {winnerPresentation=celebrate;animator.cullingMode=celebrate?AnimatorCullingMode.AlwaysAnimate:AnimatorCullingMode.CullUpdateTransforms;foreach(var skin in skins)skin.updateWhenOffscreen=celebrate;}
                string next=celebrate||fighter.dancing||rigChampion||!fighter.alive&&fighter.bot&&state.phase==(int)MatchPhase.Intermission?"Dance":social&&movement>.15f?"Walk":"Idle";
                if(next=="Dance"&&fighter.dance>0)next="Dance"+fighter.dance;
                if(next!=animationState){if(rigChampion)animator.Play(next,0,0);else animator.CrossFadeInFixedTime(next,.18f,0);animationState=next;}
                animator.speed=next=="Walk"?Mathf.Clamp(movement/1.2f,.65f,1.5f):1;
                model.localScale=baseScale;model.localRotation=baseRotation;return;
            }
            float now=state.clock;
            float seed=fighter.id*1.73f, breath=Mathf.Sin(now*1.8f+seed);
            float reaction=Mathf.Clamp01(1-(Time.unscaledTime-advancedAt)/1.3f);
            float cheer=Mathf.Sin((1-reaction)*Mathf.PI)*reaction;
            bool champion=state.phase==(int)MatchPhase.Champion&&state.winner==fighter.id;
            float sway=fighter.bot?Mathf.Sin(now*.85f+seed)*.75f:0;
            float nod=(fighter.bot?Mathf.Sin(now*1.13f+seed)*.6f:0)-cheer*3;
            if(champion)nod+=Mathf.Sin(now*2.1f)*1.2f;
            model.localRotation=baseRotation*Quaternion.Euler(nod,sway,sway*.5f);
            model.localScale=Vector3.Scale(baseScale,new Vector3(1-breath*.0018f,1+breath*.004f+cheer*.01f,1-breath*.0018f));
        }
        void BeginDissolve()
        {
            if(surfaces.Count==0)
            {
                var shader=Resources.Load<Shader>("StareDissolve");if(shader==null)return;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer is LineRenderer||renderer.GetComponent<TextMesh>()!=null)continue;
                    var original=renderer.sharedMaterials;var replacement=new Material[original.Length];
                    for(int i=0;i<original.Length;i++)
                    {
                        var source=original[i];var mat=new Material(shader){name="Pooled elimination / "+source.name};
                        mat.SetColor("_Color",source.HasProperty("_Color")?source.color:Color.white);
                        if(source.HasProperty("_MainTex")){mat.mainTexture=source.mainTexture;mat.mainTextureScale=source.mainTextureScale;mat.mainTextureOffset=source.mainTextureOffset;}
                        mat.SetColor("_EdgeColor",new Color(1,.22f,.06f));replacement[i]=mat;
                    }
                    surfaces.Add(new Surface{renderer=renderer,original=original,dissolve=replacement});
                }
            }
            properties.Clear();properties.SetFloat("_Progress",0);properties.SetFloat("_FloorY",exitPosition.y);
            foreach(var surface in surfaces)if(surface.renderer!=null){surface.renderer.sharedMaterials=surface.dissolve;surface.renderer.SetPropertyBlock(properties);}
            dissolving=true;
        }
        void Restore()
        {
            if(dissolving)foreach(var surface in surfaces)if(surface.renderer!=null){surface.renderer.sharedMaterials=surface.original;surface.renderer.SetPropertyBlock(null);}
            dissolving=false;if(animator!=null)animator.speed=1;
            if(model!=null){model.localScale=baseScale;model.localRotation=baseRotation;}
        }
        void OnDisable(){Restore();seen=false;sampledMotion=false;movement=0;}
        void OnDestroy(){foreach(var surface in surfaces)foreach(var mat in surface.dissolve)if(mat!=null)Destroy(mat);}
    }
}
