using UnityEngine;

namespace BK.Kit
{
    // SF's authored three-piece box meshes, with cosmetic motion on the top-down board.
    public sealed class BlockBreakEffect : MonoBehaviour
    {
        private Transform[] pieces;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private Renderer[] renderers;
        private MaterialPropertyBlock properties;
        private float age;
        public float Opacity {get;private set;}=1;

        private void Awake()
        {
            properties=new MaterialPropertyBlock();
            renderers=GetComponentsInChildren<Renderer>();
            pieces=new Transform[renderers.Length];positions=new Vector3[pieces.Length];rotations=new Quaternion[pieces.Length];
            for(int i=0;i<pieces.Length;i++)
            {
                pieces[i]=renderers[i].transform;positions[i]=pieces[i].localPosition;rotations[i]=pieces[i].localRotation;
            }
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        }

        private void Update()
        {
            age+=Time.deltaTime;
            // SF common settings: hold for one second, fade over half a second.
            Opacity=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1)/.5f));
            for(int i=0;i<pieces.Length;i++)
            {
                float angle=i*Mathf.PI*2/Mathf.Max(1,pieces.Length)+.4f;
                float travel=Mathf.Min(age,.42f);
                var spread=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                pieces[i].localPosition=positions[i]+spread*(travel*.85f)+Vector3.up*Mathf.Max(-.15f,1.2f*age-4.5f*age*age);
                pieces[i].localRotation=rotations[i]*Quaternion.Euler(travel*80,travel*110,travel*50);
                properties.SetFloat("_Opacity",Opacity);renderers[i].SetPropertyBlock(properties);
            }
            if(age>=1.5f)Destroy(gameObject);
        }
    }
}
