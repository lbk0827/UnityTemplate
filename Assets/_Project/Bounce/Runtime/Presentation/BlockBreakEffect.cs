using UnityEngine;

namespace BK.Kit
{
    // SF's authored three-piece box meshes, thrown by a cosmetic ballistic simulation (no physics bodies).
    public sealed class BlockBreakEffect : MonoBehaviour
    {
        private const float Gravity=22f, Restitution=.35f, GroundY=0f, Life=.75f, ShrinkTime=.35f;
        private const float PieceScale=.4f;
        private Transform[] pieces;
        private Vector3[] velocities, spins, scales;
        private float[] restHeights;
        private float age;

        private void Awake()
        {
            var renderers=GetComponentsInChildren<Renderer>();
            pieces=new Transform[renderers.Length];velocities=new Vector3[pieces.Length];spins=new Vector3[pieces.Length];
            scales=new Vector3[pieces.Length];restHeights=new float[pieces.Length];
            for(int i=0;i<pieces.Length;i++)
            {
                pieces[i]=renderers[i].transform;
                // Shrink each shard at its own pivot, preserving the box's break-apart positions.
                pieces[i].localScale*=PieceScale;scales[i]=pieces[i].localScale;
                // Pieces settle on their thinnest side, so rest the pivot that far above the grass.
                var extents=renderers[i].bounds.extents;
                restHeights[i]=GroundY+Mathf.Min(extents.x,Mathf.Min(extents.y,extents.z));
            }
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        }

        // Throws every piece up and away from the block centre, biased along the ball's travel.
        // Pieces shrink out before they fall below neighbouring blocks, which would hide them abruptly.
        public void Launch(Vector3 push)
        {
            push.y=0;
            for(int i=0;i<pieces.Length;i++)
            {
                var outward=pieces[i].position-transform.position;outward.y=0;
                if(outward.sqrMagnitude<.0001f)
                {
                    float angle=i*Mathf.PI*2/pieces.Length;
                    outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                }
                velocities[i]=outward.normalized*Random.Range(1f,1.6f)+push.normalized*Random.Range(1f,1.6f)+Vector3.up*Random.Range(5f,6.5f);
                spins[i]=Random.onUnitSphere*Random.Range(360f,600f);
            }
        }

        private void Update()
        {
            float dt=Time.deltaTime;
            if(dt<=0)return;
            age+=dt;
            float size=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-(Life-ShrinkTime))/ShrinkTime));
            for(int i=0;i<pieces.Length;i++)
            {
                var v=velocities[i];v.y-=Gravity*dt;
                var p=pieces[i].position+v*dt;
                if(p.y<restHeights[i])
                {
                    p.y=restHeights[i];
                    // Bounce while the impact is strong, then slide to a stop.
                    v.y=v.y<-1.5f?-v.y*Restitution:0;
                    v.x*=.6f;v.z*=.6f;spins[i]*=.5f;
                }
                if(v.y==0 && p.y<=restHeights[i])
                {
                    float drag=Mathf.Clamp01(1-6*dt);
                    v.x*=drag;v.z*=drag;spins[i]*=drag;
                }
                velocities[i]=v;
                pieces[i].SetPositionAndRotation(p,Quaternion.Euler(spins[i]*dt)*pieces[i].rotation);
                pieces[i].localScale=scales[i]*size;
            }
            if(age>=Life)Destroy(gameObject);
        }
    }
}
