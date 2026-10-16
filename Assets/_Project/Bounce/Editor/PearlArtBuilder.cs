using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace BK.Kit.Editor
{
    // Original, editable game geometry. No imported cannon or ball meshes are used.
    public static class PearlArtBuilder
    {
        public const string Root="Assets/_Project/Bounce/Content/Pearl";
        private static Material ivory,teal,gold,dark,pearl;
        private static Mesh sphere,ring;

        [MenuItem("BK/Integration/Build Pearl Cannon And Ball")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            ivory=Material("Shell porcelain",p.arenaMaterial,new Color(1,.88f,.72f),.28f,.05f);
            teal=Material("Lagoon enamel",p.arenaMaterial,new Color(.035f,.38f,.39f),.5f,.15f);
            gold=Material("Champagne gold",p.arenaMaterial,new Color(1,.67f,.24f),.5f,.55f);
            dark=Material("Barrel interior",p.arenaMaterial,new Color(.018f,.075f,.085f),.1f,0);
            pearl=Material("Pearl lustre",p.arenaMaterial,new Color(1,.96f,.85f),.72f,.18f);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(primitive);
            ring=SaveMesh(Torus(.42f,.055f),"Ring");
            var root=new GameObject("Pearl Cannon");
            try
            {
                Part(root.transform,"Foot",sphere,teal,new Vector3(0,.08f,-.1f),new Vector3(1.05f,.16f,.85f));
                Part(root.transform,"Foot trim",ring,gold,new Vector3(0,.11f,-.1f),new Vector3(1,.5f,.82f),Quaternion.Euler(90,0,0));
                var shell=new GameObject("Shell").transform;shell.SetParent(root.transform,false);
                Part(shell,"Scalloped shell",SaveMesh(Shell(),"Scallop"),ivory,Vector3.zero,Vector3.one);
                for(int i=0;i<9;i++)
                {
                    float a=(-88+i*22)*Mathf.Deg2Rad;
                    Part(shell,"Rim pearl "+i,sphere,gold,new Vector3(Mathf.Sin(a)*.77f,.22f,.05f-Mathf.Cos(a)*.77f),Vector3.one*.09f);
                }
                var barrel=new GameObject("Barrel").transform;barrel.SetParent(root.transform,false);
                barrel.localPosition=new Vector3(0,.278f,0);
                Part(barrel,"Enamel barrel",SaveMesh(Tube(.22f,.165f,-.31f,.47f),"Barrel"),teal,Vector3.zero,Vector3.one);
                Part(barrel,"Muzzle rim",ring,gold,new Vector3(0,0,.47f),Vector3.one*.49f);
                Part(barrel,"Rear collar",ring,gold,new Vector3(0,0,-.22f),Vector3.one*.53f);
                Part(barrel,"Dark bore",sphere,dark,new Vector3(0,0,.17f),new Vector3(.32f,.32f,.025f));
                // A pearl nested in a wave ring repeats on both the launcher and ammunition.
                Part(barrel,"Wave crest",ring,gold,new Vector3(0,.211f,-.06f),Vector3.one*.23f,Quaternion.Euler(90,0,0));
                Part(barrel,"Crest pearl",sphere,pearl,new Vector3(0,.225f,-.06f),Vector3.one*.13f);
                var muzzle=Particles("CannonExplodeParticle",ring,pearl,1,.2f,0,.55f);
                muzzle.transform.SetParent(barrel,false);muzzle.transform.localPosition=new Vector3(0,0,.5f);
                var size=muzzle.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,1.4f));
                var count=new GameObject("TXT_BallCount").AddComponent<TextMeshPro>();
                count.transform.SetParent(root.transform,false);count.transform.localPosition=new Vector3(0,.3f,-.68f);
                count.transform.localRotation=Quaternion.Euler(60,0,0);count.fontSize=2.6f;count.alignment=TextAlignmentOptions.Center;
                count.rectTransform.sizeDelta=new Vector2(.8f,.3f);count.color=new Color(.025f,.18f,.19f);count.text="20";
                var fonts=AssetDatabase.FindAssets("LilitaOne t:TMP_FontAsset");
                if(fonts.Length>0)count.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fonts[0]));
                root.AddComponent<Animator>().runtimeAnimatorController=Controller();
                p.cannon=PrefabUtility.SaveAsPrefabAsset(root,Root+"/PearlCannon.prefab");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
            root=new GameObject("Lagoon Pearl");
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
            Debug.Log("PEARL_ART_BUILT: cannon, ball, recoil, muzzle ring and impact splash.");
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
        private static Mesh Torus(float radius,float width)=>Surface(48,10,(u,v)=>
        {
            float a=u*Mathf.PI*2,b=-v*Mathf.PI*2,r=radius+width*Mathf.Cos(b);
            return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*width);
        });
        private static Mesh Tube(float outer,float inner,float back,float front)=>Surface(48,4,(u,v)=>
        {
            int edge=Mathf.RoundToInt(v*4);float r=edge==0 || edge==1 || edge==4?outer:inner;
            float z=edge==1 || edge==2?front:back;float a=-u*Mathf.PI*2;
            return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z);
        });
        private static Mesh Band()=>Surface(48,4,(u,v)=>
        {
            float a=u*Mathf.PI*2,y=(v-.5f)*.12f,r=Mathf.Sqrt(.501f*.501f-y*y);
            return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
        });
        private static Mesh Shell()=>Surface(80,24,(u,v)=>
        {
            // Closed folded fan: outer scallops, radial flutes, and a curved underside.
            float a=(.5f-u)*Mathf.PI*1.13f,t=v<=.5f?v*2:(1-v)*2;
            float rib=.5f+.5f*Mathf.Cos(u*Mathf.PI*18),r=t*(.76f+.075f*rib);
            float y=.16f+Mathf.Sin(t*Mathf.PI*.85f)*.14f+(v<=.5f?.065f*rib*t:-.06f*t);
            return new Vector3(Mathf.Sin(a)*r,y,.05f-Mathf.Cos(a)*r);
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
        private static RuntimeAnimatorController Controller()
        {
            var clip=new AnimationClip {name="Pearl recoil"};
            clip.SetCurve("Barrel",typeof(Transform),"m_LocalPosition.x",AnimationCurve.Constant(0,.25f,0));
            clip.SetCurve("Barrel",typeof(Transform),"m_LocalPosition.y",AnimationCurve.Constant(0,.25f,.278f));
            clip.SetCurve("Barrel",typeof(Transform),"m_LocalPosition.z",new AnimationCurve(new Keyframe(0,0),new Keyframe(.045f,-.1f),new Keyframe(.22f,0)));
            clip.SetCurve("Shell",typeof(Transform),"m_LocalScale.x",new AnimationCurve(new Keyframe(0,1),new Keyframe(.065f,.92f),new Keyframe(.25f,1)));
            var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Recoil.anim");
            if(old==null) {AssetDatabase.CreateAsset(clip,Root+"/Recoil.anim");old=clip;} else {EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);}
            var path=Root+"/Pearl.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(path);
                var machine=controller.layers[0].stateMachine;var idle=machine.AddState("Idle");machine.defaultState=idle;
                var fire=machine.AddState("CannonRigPreshoot");fire.motion=old;
                var transition=fire.AddTransition(idle);transition.hasExitTime=true;transition.exitTime=1;transition.duration=0;
            }
            return controller;
        }
    }
}
