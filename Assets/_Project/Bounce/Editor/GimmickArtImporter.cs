using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace BK.Kit.Editor
{
    // Explicit art rebuild only. Never runs at import time or overwrites authored prefabs on Play.
    public static class GimmickArtImporter
    {
        const string Root = GimmickPrefabBuilder.Root;
        [Serializable] sealed class ModelData
        {
            public string name;
            public float[] vertices, normals, colors;
            public int[] triangles;
        }
        static readonly Dictionary<string,Mesh> meshes = new Dictionary<string,Mesh>();
        static Material enamel, heat;

        public static void BuildAndCapture(){Build();CaptureCatalog();}

        public static void Build()
        {
            string input=Path.GetFullPath("TestResults/GimmickArt");
            if(!File.Exists(Path.Combine(input,"Furnace.json")))throw new FileNotFoundException("Run tools/art/build_gimmick_models.py first, exporting to TestResults/GimmickArt.");
            Directory.CreateDirectory(Root+"/Models");AssetDatabase.Refresh();meshes.Clear();
            foreach(var path in Directory.GetFiles(input,"*.json"))
            {
                if(Path.GetFileName(path)=="manifest.json")continue;
                var data=JsonUtility.FromJson<ModelData>(File.ReadAllText(path));
                string asset=Root+"/Models/"+data.name+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,asset);}else mesh.Clear();
                mesh.name=data.name;mesh.indexFormat=data.vertices.Length/3>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                var vertices=new Vector3[data.vertices.Length/3];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    vertices[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
                    normals[i]=new Vector3(data.normals[i*3],data.normals[i*3+1],data.normals[i*3+2]);
                    colors[i]=new Color(data.colors[i*4],data.colors[i*4+1],data.colors[i*4+2],data.colors[i*4+3]);
                }
                mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.triangles=data.triangles;mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);meshes.Add(data.name,mesh);
            }
            enamel=Material("Enamel",Color.white,Color.black);
            heat=Material("FurnaceHeat",new Color(1,.24f,.02f),new Color(1,.24f,.02f));
            foreach(string name in new[]{"MetalTube","TubeSegment","Cupboard","MovingPanel","SeaMine","Switch","Gate","DoubleHitBlock","DirectionalArmor","PorcelainShard"}) Upgrade(name);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static Material Material(string name,Color color,Color emission)
        {
            string path=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("BK/Gimmick Enamel"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetColor("_EmissionColor",emission);EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Model(Transform parent,string name,Material material=null)
        {
            var go=new GameObject("Art_"+name);go.layer=2;go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=meshes[name];
            go.AddComponent<MeshRenderer>().sharedMaterial=material??enamel;return go;
        }
        static void Upgrade(string name)
        {
            string path=Root+"/"+name+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Keep gameplay transforms and collider dimensions exactly as authored.
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))Object.DestroyImmediate(renderer);
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(filter);
                // Remove previous generated art so rebuilding is deterministic.
                var oldArt=new List<GameObject>();
                foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("Art_"))oldArt.Add(t.gameObject);
                foreach(var go in oldArt)if(go!=null)Object.DestroyImmediate(go);
                var view=root.GetComponent<GimmickView>();
                switch(name)
                {
                    case "MetalTube":
                        Model(view.furnace,"Furnace");
                        view.furnaceGlow=new[]{Model(view.furnace,"FurnaceGlow",heat).GetComponent<Renderer>()};
                        foreach(var piece in view.pieces)Model(piece.transform.GetChild(0),"Pipe");
                        break;
                    case "TubeSegment":Model(root.transform.GetChild(0),"Pipe");break;
                    case "Cupboard":
                        Model(root.transform,"Cabinet");
                        foreach(var piece in view.pieces)
                        {
                            var p=piece.transform.localPosition;p.y=.32f;piece.transform.localPosition=p;Model(piece.transform,"Plate");
                        }
                        break;
                    case "MovingPanel":
                        Model(view.movingBody,"Panel");Model(view.rail,"Rail");
                        foreach(var stop in view.railStops){stop.localScale=Vector3.one;Model(stop,"RailStop");}
                        break;
                    case "SeaMine":Model(view.pieces[0].transform,"Mine");break;
                    case "Switch":
                        Model(view.pieces[0].transform,"SwitchBase");
                        view.inactiveVisual=Model(view.pieces[0].transform,"SwitchOff");
                        view.activeVisual=Model(view.pieces[0].transform,"SwitchOn");view.ShowActive(false);break;
                    case "Gate":
                        Model(root.transform,"GateFrame");Model(view.pieces[0].transform,"GateBars");
                        view.activeVisual=Model(root.transform,"GateLamp");view.activeVisual.SetActive(false);break;
                    case "DoubleHitBlock":
                        view.inactiveVisual=Model(view.pieces[0].transform,"ComboClosed");
                        view.activeVisual=Model(view.pieces[0].transform,"ComboOpen");view.ShowActive(false);break;
                    case "DirectionalArmor":Model(view.pieces[0].transform,"Armor");break;
                    case "PorcelainShard":Model(root.transform,"Shard");break;
                }
                // Retire empty primitive nodes, leaving a readable artist-facing hierarchy.
                var keep=new HashSet<Transform>{root.transform};
                if(view!=null)
                {
                    foreach(var piece in view.pieces)if(piece!=null)keep.Add(piece.transform);
                    foreach(var t in new[]{view.furnace,view.movingBody,view.rail,
                        view.activeVisual!=null?view.activeVisual.transform:null,view.inactiveVisual!=null?view.inactiveVisual.transform:null})
                        if(t!=null)keep.Add(t);
                    if(view.railStops!=null)foreach(var t in view.railStops)keep.Add(t);
                }
                var hierarchy=root.GetComponentsInChildren<Transform>(true);
                for(int i=hierarchy.Length-1;i>=0;i--)
                {
                    var t=hierarchy[i];
                    if(!keep.Contains(t)&&t.childCount==0&&t.GetComponents<Component>().Length==1)Object.DestroyImmediate(t.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        public static void CaptureCatalog()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Art review camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1.85f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.82f,.89f,.92f);
            camera.transform.position=new Vector3(0,7,-4.1f);camera.transform.LookAt(Vector3.zero);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Softbox").AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-35,0);
            Directory.CreateDirectory("TestResults/GimmickArtPreview");
            foreach(string name in new[]{"MetalTube","Cupboard","MovingPanel","SeaMine","Switch","Gate","DoubleHitBlock","DirectionalArmor"})
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+".prefab"));
                if(name=="MetalTube")go.transform.position=Vector3.left*.5f;
                if(name=="Cupboard")go.transform.position=new Vector3(-1,0,.5f);
                if(name=="MovingPanel")camera.orthographicSize=3.2f;else camera.orthographicSize=name=="MetalTube"?2.2f:name=="Cupboard"?1.85f:.87f;
                var target=new RenderTexture(768,768,24);target.antiAliasing=4;target.Create();
                var previous=RenderTexture.active;camera.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target;var image=new Texture2D(768,768,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
                File.WriteAllBytes("TestResults/GimmickArtPreview/"+name+".png",image.EncodeToPNG());
                RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(go);
            }
        }
    }
}
