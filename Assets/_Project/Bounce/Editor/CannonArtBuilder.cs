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
    /// <summary>Editable naval cannon geometry; keeps the existing prefab GUID and launch anchors.</summary>
    public static class CannonArtBuilder
    {
        private const string Root="Assets/_Project/Bounce/Content/Cannon";
        private static Material steel,brass,wood,woodEdge,dark,ivory;
        private static Mesh sphere,beam;

        [MenuItem("BK/Integration/Build Cannon")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            steel=Material("Blued iron",presentation.arenaMaterial,new Color(.055f,.29f,.47f),.58f,.25f);
            brass=Material("Brushed brass",presentation.arenaMaterial,new Color(.9f,.54f,.11f),.48f,.35f);
            wood=Material("Carriage walnut",presentation.arenaMaterial,new Color(.29f,.105f,.045f),.24f,0);
            woodEdge=Material("Carriage end grain",presentation.arenaMaterial,new Color(.48f,.23f,.085f),.28f,0);
            dark=Material("Bore and tyres",presentation.arenaMaterial,new Color(.018f,.032f,.045f),.22f,.15f);
            ivory=Material("Counter ivory",presentation.arenaMaterial,new Color(1,.91f,.66f),.32f,0);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(primitive);
            beam=SaveMesh(BevelBox(),"Chamfered beam");
            var root=new GameObject("Cannon");
            try
            {
                var carriage=new GameObject("Carriage").transform;carriage.SetParent(root.transform,false);
                Part(carriage,"Timber bed",beam,wood,new Vector3(0,.09f,-.25f),new Vector3(.9f,.16f,1.05f));
                foreach(int side in new[]{-1,1})
                {
                    float x=side*.36f;
                    Part(carriage,"Cheek "+side,beam,woodEdge,new Vector3(x,.22f,-.18f),new Vector3(.17f,.29f,.78f));
                    Part(carriage,"Cheek iron strap "+side,beam,steel,new Vector3(x,.375f,-.12f),new Vector3(.19f,.045f,.2f));
                    Part(carriage,"Axle bearing "+side,sphere,brass,new Vector3(side*.43f,.278f,-.12f),new Vector3(.11f,.16f,.16f));
                    Wheel(carriage,side);
                    for(int i=0;i<3;i++)
                        Part(carriage,"Rivet "+side+" "+i,sphere,brass,new Vector3(x,.374f,-.43f+i*.24f),new Vector3(.055f,.025f,.055f));
                }
                Part(carriage,"Front cross brace",beam,steel,new Vector3(0,.13f,.2f),new Vector3(.92f,.085f,.11f));
                var barrel=new GameObject("Barrel").transform;barrel.SetParent(root.transform,false);
                barrel.localPosition=new Vector3(0,.278f,0);
                // Closed breech, tapered body, flared muzzle and a recessed bore, all along +Z.
                Part(barrel,"Cast iron barrel",SaveMesh(Revolve(new[]{
                    new Vector2(0,-.66f),new Vector2(.16f,-.66f),new Vector2(.25f,-.59f),new Vector2(.28f,-.44f),
                    new Vector2(.265f,-.3f),new Vector2(.222f,.29f),new Vector2(.26f,.38f),new Vector2(.26f,.48f),
                    new Vector2(.17f,.48f),new Vector2(.16f,.18f),new Vector2(0,.18f)}),"Profiled barrel"),steel,Vector3.zero,Vector3.one);
                Collar(barrel,"Muzzle brass lip",.265f,.17f,.415f,.5f);
                Collar(barrel,"Breech band",.289f,.255f,-.48f,-.39f);
                Collar(barrel,"Forward chase band",.239f,.215f,.19f,.235f);
                Part(barrel,"Recessed black bore",sphere,dark,new Vector3(0,0,.205f),new Vector3(.325f,.325f,.025f));
                Part(barrel,"Breech button",sphere,brass,new Vector3(0,0,-.71f),new Vector3(.16f,.16f,.19f));
                Part(barrel,"Front sight",beam,brass,new Vector3(0,.266f,.34f),new Vector3(.055f,.065f,.095f));
                Part(barrel,"Touch hole plate",beam,brass,new Vector3(0,.278f,-.35f),new Vector3(.115f,.022f,.13f));
                Part(barrel,"Touch hole",sphere,dark,new Vector3(0,.293f,-.35f),new Vector3(.045f,.008f,.045f));
                Muzzle(barrel);
                // Keep the count readable on a separate rear plate, away from the barrel silhouette.
                Part(carriage,"Counter brass frame",beam,brass,new Vector3(0,.12f,-.84f),new Vector3(.55f,.075f,.28f));
                Part(carriage,"Counter face",beam,dark,new Vector3(0,.164f,-.84f),new Vector3(.47f,.025f,.22f));
                var count=new GameObject("TXT_BallCount").AddComponent<TextMeshPro>();
                count.transform.SetParent(root.transform,false);count.transform.localPosition=new Vector3(0,.26f,-.87f);
                count.transform.localRotation=Quaternion.Euler(60,0,0);count.fontSize=2;count.alignment=TextAlignmentOptions.Center;
                count.rectTransform.sizeDelta=new Vector2(.45f,.22f);count.color=ivory.color;count.text="20";
                var fonts=AssetDatabase.FindAssets("LilitaOne t:TMP_FontAsset");
                if(fonts.Length>0)count.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fonts[0]));
                root.AddComponent<Animator>().runtimeAnimatorController=Controller();
                // Preserve the serialized reference used by presentation and Addressables.
                presentation.cannon=PrefabUtility.SaveAsPrefabAsset(root,PearlArtBuilder.Root+"/PearlCannon.prefab");
                EditorUtility.SetDirty(presentation);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            Debug.Log("CANNON_ART_BUILT: iron barrel, timber carriage, wheels and recoil.");
        }
        private static void Wheel(Transform parent,int side)
        {
            var wheel=new GameObject("Wheel "+side).transform;wheel.SetParent(parent,false);
            wheel.localPosition=new Vector3(side*.57f,.24f,-.16f);wheel.localRotation=Quaternion.Euler(0,side*90,0);
            Part(wheel,"Iron tyre",SaveMesh(Revolve(new[]{new Vector2(.218f,-.075f),new Vector2(.24f,-.045f),new Vector2(.24f,.045f),new Vector2(.218f,.075f),new Vector2(.19f,.075f),new Vector2(.19f,-.075f),new Vector2(.218f,-.075f)}),"Wheel tyre"),steel,Vector3.zero,Vector3.one);
            Part(wheel,"Wooden felloe",SaveMesh(Revolve(new[]{new Vector2(.208f,-.065f),new Vector2(.208f,.065f),new Vector2(.155f,.065f),new Vector2(.155f,-.065f),new Vector2(.208f,-.065f)}),"Wheel felloe"),woodEdge,Vector3.zero,Vector3.one);
            for(int i=0;i<8;i++)
            {
                float angle=i*45,rad=angle*Mathf.Deg2Rad;
                Part(wheel,"Spoke "+i,beam,woodEdge,new Vector3(Mathf.Cos(rad)*.115f,Mathf.Sin(rad)*.115f,0),new Vector3(.16f,.05f,.09f),Quaternion.Euler(0,0,angle));
            }
            Part(wheel,"Hub",sphere,brass,new Vector3(0,0,.055f),new Vector3(.17f,.17f,.18f));
            Part(wheel,"Axle pin",sphere,steel,new Vector3(0,0,.14f),new Vector3(.075f,.075f,.025f));
        }
        private static void Collar(Transform barrel,string name,float outer,float inner,float back,float front)
            =>Part(barrel,name,SaveMesh(Revolve(new[]{new Vector2(outer-.012f,back),new Vector2(outer,back+.012f),new Vector2(outer,front-.012f),new Vector2(outer-.012f,front),new Vector2(inner,front),new Vector2(inner,back),new Vector2(outer-.012f,back)}),name),brass,Vector3.zero,Vector3.one);
        private static Mesh Revolve(Vector2[] profile)
        {
            const int segments=64;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<profile.Length;j++)for(int i=0;i<=segments;i++)
            {
                float a=-i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(a)*profile[j].x,Mathf.Sin(a)*profile[j].x,profile[j].y));
                if(j<profile.Length-1 && i<segments){int n=j*(segments+1)+i;triangles.AddRange(new[]{n,n+segments+1,n+1,n+1,n+segments+1,n+segments+2});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh BevelBox()
        {
            var corners=new[]{new Vector2(-.4f,-.5f),new Vector2(.4f,-.5f),new Vector2(.5f,-.4f),new Vector2(.5f,.4f),new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(-.5f,.4f),new Vector2(-.5f,-.4f)};
            var v=new List<Vector3>();var t=new List<int>();
            for(int slice=0;slice<4;slice++)foreach(var p in corners)
            {float scale=slice==0||slice==3?.84f:1;v.Add(new Vector3(p.x*scale,p.y*scale,new[]{-.5f,-.44f,.44f,.5f}[slice]));}
            for(int s=0;s<3;s++)for(int i=0;i<8;i++){int a=s*8+i,b=s*8+(i+1)%8;t.AddRange(new[]{a,b,a+8,b,b+8,a+8});}
            for(int i=1;i<7;i++){t.AddRange(new[]{0,i+1,i,24,24+i,25+i});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Muzzle(Transform barrel)
        {
            var ps=new GameObject("CannonExplodeParticle").AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(barrel,false);ps.transform.localPosition=new Vector3(0,0,.5f);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.18f;main.startLifetime=.14f;main.startSpeed=1.5f;
            main.startSize=.14f;main.maxParticles=6;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,6)});
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=18;shape.radius=.06f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=sphere;renderer.sharedMaterial=ivory;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private static RuntimeAnimatorController Controller()
        {
            var clip=new AnimationClip{name="Cannon recoil"};
            clip.SetCurve("Barrel",typeof(Transform),"m_LocalPosition.y",AnimationCurve.Constant(0,.28f,.278f));
            clip.SetCurve("Barrel",typeof(Transform),"m_LocalPosition.z",new AnimationCurve(new Keyframe(0,0),new Keyframe(.045f,-.14f),new Keyframe(.15f,-.045f),new Keyframe(.28f,0)));
            var path=Root+"/Recoil.anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null){AssetDatabase.CreateAsset(clip,path);existing=clip;}else{EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);}
            path=Root+"/Cannon.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;
                var idle=machine.AddState("Idle");machine.defaultState=idle;var fire=machine.AddState("CannonRigPreshoot");fire.motion=existing;
                var transition=fire.AddTransition(idle);transition.hasExitTime=true;transition.exitTime=1;transition.duration=0;
            }
            return controller;
        }
        private static Material Material(string name,Material source,Color color,float smooth,float metal)
        {
            var path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}m.name=name;m.color=color;m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);EditorUtility.SetDirty(m);return m;
        }
        private static GameObject Part(Transform parent,string name,Mesh mesh,Material material,Vector3 pos,Vector3 scale,Quaternion? rotation=null)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.transform.localRotation=rotation??Quaternion.identity;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            var path=Root+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);mesh.name=name;
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
        }
    }
}
