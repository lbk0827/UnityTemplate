using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace BK.Kit.Editor
{
    // Explicit, reversible presentation swap. Original Box assets remain available.
    public static class RippleBoxSetup
    {
        public const string Root="Assets/_Project/Bounce/Content/RippleBox";
        public const string Source="Assets/_Project/Bounce/Content/Imported/Application/Bundles/VFXs/Prefab/Block/Object/";
        static readonly string[] Colours={"G","B","P","Y"};
        [Serializable] sealed class Data
        {
            public string name;
            public float[] vertices,normals,uv,center;
            public int[] triangles;
        }

        [MenuItem("BK/Integration/Build Ripple Box Preview")]
        public static void Build()
        {
            const string input="TestResults/RippleBox/";
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var whole=Import(input+"RippleBox.json",out _);
            var shards=new Mesh[3];var centres=new Vector3[3];
            for(int i=0;i<3;i++)shards[i]=Import(input+"RippleShard"+i+".json",out centres[i]);
            string atlasPath=Root+"/RippleBox_AlbedoAO.png";
            File.Copy(input+"RippleBox_AlbedoAO.png",atlasPath,true);
            AssetDatabase.ImportAsset(atlasPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=512;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var blocks=new GameObject[4];var fragments=new GameObject[4];
            for(int c=0;c<Colours.Length;c++)
            {
                var original=Original(c);
                var reference=original.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                string path=Root+"/Ripple_"+Colours[c]+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(reference.shader);AssetDatabase.CreateAsset(material,path);}
                material.SetTexture("_BaseMap",atlas);
                material.SetTexture("_MatCapTex",reference.GetTexture("_MatCapTex"));
                foreach(string key in new[]{"_BaseColor","_HColor","_SColor"})
                    material.SetColor(key,reference.GetColor(key));
                material.SetColor("_MatCapColor",new Color(.60f,.60f,.60f,1));
                material.SetFloat("_UseMatCap",1);
                material.SetFloat("_RampThreshold",reference.GetFloat("_RampThreshold"));
                material.SetFloat("_RampSmoothing",reference.GetFloat("_RampSmoothing"));
                material.SetFloat("_EmissionStrength",0);material.SetColor("_EmissionColor",Color.black);
                EditorUtility.SetDirty(material);
                var root=new GameObject("RippleBox_"+Colours[c]);
                try
                {
                    Part(root.transform,"Enamel droplet box",whole,material,Vector3.zero);
                    // Keep the original convex collision surface, independent of the new art.
                    var source=original.GetComponentInChildren<MeshCollider>();
                    var collision=new GameObject("Gameplay collision");collision.transform.SetParent(root.transform,false);
                    collision.transform.localPosition=original.transform.InverseTransformPoint(source.transform.position);
                    collision.transform.localRotation=Quaternion.Inverse(original.transform.rotation)*source.transform.rotation;
                    collision.transform.localScale=source.transform.lossyScale;
                    var collider=collision.AddComponent<MeshCollider>();collider.sharedMesh=source.sharedMesh;
                    collider.convex=source.convex;collider.cookingOptions=source.cookingOptions;
                    collider.sharedMaterial=source.sharedMaterial;collider.contactOffset=source.contactOffset;
                    blocks[c]=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+root.name+".prefab");
                }
                finally{Object.DestroyImmediate(root);}
                root=new GameObject("RippleBoxFragments_"+Colours[c]);
                try
                {
                    for(int i=0;i<3;i++)Part(root.transform,"Shard "+i,shards[i],material,centres[i]);
                    fragments[c]=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+root.name+".prefab");
                }
                finally{Object.DestroyImmediate(root);}
            }
            presentation.blocks=blocks;presentation.brokenBlocks=fragments;
            EditorUtility.SetDirty(presentation);AssetDatabase.SaveAssets();
            Capture(blocks,"after");
            Debug.Log("RIPPLE_BOX_READY: four colours, own mesh/atlas/fragments; original collision preserved.");
        }

        public static GameObject Original(int index)=>AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Box_Rocket_Square_"+Colours[index]+".prefab");
        public static void CaptureBefore()=>Capture(new[]{Original(0),Original(1),Original(2),Original(3)},"before");
        static void Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);part.transform.localPosition=position;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        static Mesh Import(string path,out Vector3 centre)
        {
            var data=JsonUtility.FromJson<Data>(File.ReadAllText(path));
            centre=data.center==null?Vector3.zero:new Vector3(data.center[0],data.center[1],data.center[2]);
            string asset=Root+"/"+data.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,asset);}else mesh.Clear();
            var vertices=new Vector3[data.vertices.Length/3];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
                normals[i]=new Vector3(data.normals[i*3],data.normals[i*3+1],data.normals[i*3+2]);
                uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);
            }
            mesh.name=data.name;mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;
            mesh.triangles=data.triangles;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
        static void Capture(GameObject[] prefabs,string suffix)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Box review camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1.55f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.87f,.89f);
            camera.transform.position=new Vector3(0,6,-3.464f);camera.transform.LookAt(Vector3.zero);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Soft studio key").AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-35,0);
            for(int i=0;i<prefabs.Length;i++)
            {
                var block=Object.Instantiate(prefabs[i]);block.transform.position=Vector3.right*(i-1.5f)*1.1f;
            }
            Directory.CreateDirectory("TestResults/RippleBoxReview");
            var target=new RenderTexture(1200,800,24){antiAliasing=4};target.Create();
            var previous=RenderTexture.active;camera.targetTexture=target;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target;var image=new Texture2D(1200,800,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
                File.WriteAllBytes("TestResults/RippleBoxReview/box-"+suffix+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);}
        }
    }
}
