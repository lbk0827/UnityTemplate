using UnityEngine;

namespace BK.Kit
{
    public sealed class BallFlightEffect : MonoBehaviour
    {
        private Vector3 fullScale;
        private float age;
        private TrailRenderer trail;
        public TrailRenderer Trail=>trail;
        public void Initialize(Material material)
        {
            fullScale=transform.localScale;transform.localScale=fullScale*.4f;
            if(material==null)return;
            var go=new GameObject("SF ball flight trail");go.layer=2;go.transform.SetParent(transform,false);
            trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=material;
            trail.time=.13f;trail.minVertexDistance=.08f;trail.startWidth=.22f;trail.endWidth=0;
            trail.startColor=new Color(1,.95f,.78f,.65f);trail.endColor=new Color(.65f,.94f,.9f,0);
            trail.numCapVertices=3;trail.numCornerVertices=2;trail.alignment=LineAlignment.View;
            trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;trail.Clear();
        }
        private void Update()
        {
            age+=Time.deltaTime;
            // SF launch config: 40% at the muzzle, full size after 0.15 seconds.
            transform.localScale=fullScale*Mathf.Lerp(.4f,1,Mathf.Clamp01(age/.15f));
        }
        public void ReleaseTrail()
        {
            if(trail==null)return;
            trail.emitting=false;trail.transform.SetParent(transform.parent,true);
            Destroy(trail.gameObject,trail.time);trail=null;
        }
    }
}
