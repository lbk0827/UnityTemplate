using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BK.Kit
{
    // Owns transient SF presentation under the level rig; no gameplay colliders or callbacks.
    public sealed class BounceEffects : MonoBehaviour
    {
        private GamePresentation presentation;
        private readonly List<GameObject> live=new List<GameObject>();
        private readonly List<ParticleSystem> muzzle=new List<ParticleSystem>();
        private Animator[] animators;
        private static readonly int Preshoot=Animator.StringToHash("CannonRigPreshoot");
        public int LiveCount {get {live.RemoveAll(go=>go==null);return live.Count;}}
        public bool MuzzlePlaying=>muzzle.Exists(ps=>ps!=null && ps.IsAlive(true));

        public void Initialize(GamePresentation assets,GameObject cannon)
        {
            presentation=assets;animators=cannon.GetComponentsInChildren<Animator>();
            foreach(var ps in cannon.GetComponentsInChildren<ParticleSystem>(true))
                if(ps.name=="CannonExplodeParticle" && ps.transform.parent.gameObject.activeInHierarchy)
                {
                    ps.gameObject.SetActive(true);
                    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    muzzle.Add(ps);
                }
        }

        public void Launch(GameObject ball)
        {
            foreach(var animator in animators)
                if(animator.isActiveAndEnabled && animator.HasState(0,Preshoot))animator.Play(Preshoot,0,0);
            foreach(var ps in muzzle) {ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Play(true);}
            ball.AddComponent<BallFlightEffect>().Initialize(presentation.ballTrailMaterial);
        }

        public void Hit(Vector3 point)
        {
            Burst(presentation.ballHitParticle,point+Vector3.up*.35f,FacingCamera(),.55f,null);
        }

        public void Break(GameObject visual,int skin,Vector3 push)
        {
            Color? tint=null;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
                if(renderer.sharedMaterial!=null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                {tint=renderer.sharedMaterial.GetColor("_BaseColor");break;}
            Burst(presentation.blockBreakParticle,visual.transform.position+Vector3.up*.5f,FacingCamera(),.65f,tint);
            if(presentation.brokenBlocks==null || presentation.brokenBlocks.Length==0 || LiveCount>=40)return;
            var prefab=presentation.brokenBlocks[Mathf.Clamp(skin,0,presentation.brokenBlocks.Length-1)];
            if(prefab==null)return;
            var fragments=Instantiate(prefab,visual.transform.position,visual.transform.rotation,transform);
            fragments.name="SF block fragments";fragments.transform.localScale=visual.transform.lossyScale;
            MakeCosmetic(fragments);fragments.AddComponent<BlockBreakEffect>().Launch(push);live.Add(fragments);
        }

        // SF bursts are authored in their XY plane with +Z toward the camera.
        private static Quaternion FacingCamera()
        {
            var camera=Camera.main;
            return camera==null?Quaternion.LookRotation(Vector3.up):Quaternion.LookRotation(-camera.transform.forward,camera.transform.up);
        }

        private void Burst(GameObject prefab,Vector3 position,Quaternion rotation,float scale,Color? tint)
        {
            if(prefab==null || LiveCount>=40)return;
            var fx=Instantiate(prefab,position,rotation,transform);fx.name="SF "+prefab.name;
            fx.transform.localScale*=scale;MakeCosmetic(fx);live.Add(fx);
            var systems=fx.GetComponentsInChildren<ParticleSystem>(true);
            bool anyActive=false;foreach(var ps in systems)anyActive|=ps.gameObject.activeInHierarchy;
            float lifetime=.1f;
            foreach(var ps in systems)
            {
                if(anyActive && !ps.gameObject.activeInHierarchy)continue;
                if(!anyActive)ps.gameObject.SetActive(true);
                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=false;main.useUnscaledTime=false;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                // SF tint rule deliberately leaves smoke and sparks at their authored colours.
                if(tint.HasValue && (ps.name.StartsWith("StonePieces") || ps.name.StartsWith("StoneExplosionPieces")))
                {var color=tint.Value;color.a=main.startColor.color.a;main.startColor=color;}
                lifetime=Mathf.Max(lifetime,main.startDelay.constantMax+main.duration+main.startLifetime.constantMax+.1f);
                ps.Play(false);
            }
            StartCoroutine(Release(fx,Mathf.Min(lifetime,5)));
        }

        private static void MakeCosmetic(GameObject go)
        {
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            foreach(var collider in go.GetComponentsInChildren<Collider>(true)) {collider.enabled=false;Destroy(collider);}
            foreach(var body in go.GetComponentsInChildren<Rigidbody>(true)) {body.isKinematic=true;Destroy(body);}
            foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)) {var collision=ps.collision;collision.enabled=false;}
        }
        private IEnumerator Release(GameObject fx,float lifetime) {yield return new WaitForSeconds(lifetime);if(fx!=null)Destroy(fx);}
    }
}
