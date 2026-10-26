using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BK.Kit.Editor
{
    // Original, editable game geometry. No imported cannon or ball meshes are used.
    public static class PearlArtBuilder
    {
        public const string Root="Assets/_Project/Bounce/Content/Pearl";
        private static Material teal,gold,pearl;
        private static Mesh sphere;

        [MenuItem("BK/Integration/Build Cannon And Pearl Ball")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            teal=Material("Lagoon enamel",p.arenaMaterial,new Color(.035f,.38f,.39f),.5f,.15f);
            gold=Material("Champagne gold",p.arenaMaterial,new Color(1,.67f,.24f),.5f,.55f);
            pearl=Material("Pearl lustre",p.arenaMaterial,new Color(1,.96f,.85f),.72f,.18f);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(primitive);
            CannonArtBuilder.Build();
            var root=new GameObject("Lagoon Pearl");
            try
            {
                Part(root.transform,"Pearl",sphere,pearl,Vector3.zero,Vector3.one);
                // Band follows the sphere, staying within the radius-.5 gameplay silhouette.
                Part(root.transform,"Wave band",SaveMesh(Band(),"PearlBand"),teal,Vector3.zero,Vector3.one);
                Part(root.transform,"Pearl seal",sphere,gold,new Vector3(0,0,-.49f),new Vector3(.16f,.16f,.025f));
                p.ball=PrefabUtility.SaveAsPrefabAsset(root,Root+"/PearlBall.prefab");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
            var hit=Particles("Pearl splash",sphere,pearl,7,.28f,1.7f,.12f);
            var shape=hit.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.03f;
            var shrink=hit.sizeOverLifetime;shrink.enabled=true;shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            p.ballHitParticle=PrefabUtility.SaveAsPrefabAsset(hit.gameObject,Root+"/PearlHit.prefab");
            UnityEngine.Object.DestroyImmediate(hit.gameObject);
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();
            Debug.Log("PEARL_ART_BUILT: cannon and pearl ball assets.");
        }

        private static Material Material(string name,Material source,Color color,float smooth,float metal)
        {
            var path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) {m=new Material(source);AssetDatabase.CreateAsset(m,path);}
            m.name=name;m.color=color;m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);EditorUtility.SetDirty(m);return m;
        }
        private static GameObject Part(Transform parent,string name,Mesh mesh,Material material,Vector3 pos,Vector3 scale,Quaternion? rotation=null)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;go.transform.localRotation=rotation??Quaternion.identity;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            var path=Root+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);mesh.name=name;
            if(existing==null) {AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
        }
        private static Mesh Surface(int u,int v,Func<float,float,Vector3> point)
        {
            var vertices=new Vector3[(u+1)*(v+1)];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int j=0;j<=v;j++)for(int i=0;i<=u;i++)
            {
                int n=j*(u+1)+i;vertices[n]=point((float)i/u,(float)j/v);uv[n]=new Vector2((float)i/u,(float)j/v);
                if(i<u && j<v) {triangles.AddRange(new[]{n,n+u+1,n+1,n+1,n+u+1,n+u+2});}
            }
            var m=new Mesh();m.vertices=vertices;m.uv=uv;m.triangles=triangles.ToArray();m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        private static Mesh Band()=>Surface(48,4,(u,v)=>
        {
            float a=u*Mathf.PI*2,y=(v-.5f)*.12f,r=Mathf.Sqrt(.501f*.501f-y*y);
            return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
        });
        private static ParticleSystem Particles(string name,Mesh mesh,Material material,int amount,float lifetime,float speed,float size)
        {
            var ps=new GameObject(name).AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=lifetime;main.startLifetime=lifetime;
            main.startSpeed=speed;main.startSize=size;main.maxParticles=amount;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)amount)});
            var shape=ps.shape;shape.enabled=false;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=mesh;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return ps;
        }
    }
}
