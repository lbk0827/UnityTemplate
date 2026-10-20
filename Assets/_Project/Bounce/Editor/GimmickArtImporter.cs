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
            public float[] vertices, normals, colors, uv;
            public int[] triangles;
        }
        static readonly Dictionary<string,Mesh> meshes = new Dictionary<string,Mesh>();
        static Material enamel, heat;
        static readonly Dictionary<string,Material> pilotMaterials = new Dictionary<string,Material>();

        public static void BuildAndCapture(){Build();CaptureCatalog();}

        public static void Build()
        {
            pilotMaterials.Clear();
            BuildFrom("TestResults/GimmickArt",new[]{"MetalTube","TubeSegment","Cupboard","MovingPanel","SeaMine","Switch","Gate","DoubleHitBlock","DirectionalArmor","PorcelainShard"},false);
        }

        [MenuItem("BK/Integration/Build Cabinet And Furnace Pilot")]
        public static void BuildPilot()
        {
            pilotMaterials.Clear();
            BuildFrom("TestResults/GimmickPilot",new[]{"MetalTube","TubeSegment","Cupboard"},true);
            CapturePilot("after");
        }

        public static void CapturePilotBefore()=>CapturePilot("before");

        [MenuItem("BK/Integration/Build Ripple Gimmicks")]
        public static void BuildRippleGimmicks()
        {
            pilotMaterials.Clear();
            BuildFrom("TestResults/RippleGimmicks",new[]{"MovingPanel","SeaMine","Switch","Gate","DoubleHitBlock","DirectionalArmor","PorcelainShard"},true,"Ripple");
            CaptureCatalog();
            CaptureRippleFamily();
        }

        static void BuildFrom(string input,string[] prefabs,bool pilot,string materialFolder="Pilot")
        {
            input=Path.GetFullPath(input);
            if(!File.Exists(Path.Combine(input,"manifest.json")))throw new FileNotFoundException("Run tools/art/build_gimmick_models.py first, exporting to "+input);
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
                if(data.uv!=null && data.uv.Length==vertices.Length*2)
                {
                    var uv=new Vector2[vertices.Length];
                    for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);
                    mesh.uv=uv;
                }
                EditorUtility.SetDirty(mesh);meshes.Add(data.name,mesh);
                if(pilot)pilotMaterials[data.name]=PilotMaterial(input,data.name,materialFolder);
            }
            enamel=Material("Enamel",Color.white,Color.black);
            heat=pilotMaterials.TryGetValue("FurnaceGlow",out var glow)?glow:Material("FurnaceHeat",new Color(1,.24f,.02f),new Color(1,.24f,.02f));
            foreach(string name in prefabs) Upgrade(name);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static Material PilotMaterial(string input,string name,string materialFolder)
        {
            string folder=Root+"/"+materialFolder;Directory.CreateDirectory(folder);
            string texturePath=folder+"/"+name+"_AlbedoAO.png";
            File.Copy(Path.Combine(input,name+"_AlbedoAO.png"),texturePath,true);
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=1024;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            string path=folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("BK/Toon"));AssetDatabase.CreateAsset(material,path);}
            // Use the Box shader and its studio reflection texture. The baked atlas supplies
            // the object-specific cavity detail, while material values express porcelain/metal.
            material.shader=Shader.Find("BK/Toon");
            var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var boxMaterial=presentation.blocks[0].GetComponentInChildren<Renderer>().sharedMaterial;
            material.SetTexture("_MatCapTex",boxMaterial.GetTexture("_MatCapTex"));
            material.SetFloat("_UseMatCap",name=="FurnaceGlow"?0:1);
            float reflection=name=="Plate"?.32f:name=="Pipe"?.55f:.38f;
            material.SetColor("_MatCapColor",new Color(reflection,reflection,reflection,1));
            material.SetFloat("_RampThreshold",boxMaterial.GetFloat("_RampThreshold"));
            material.SetFloat("_RampSmoothing",boxMaterial.GetFloat("_RampSmoothing"));
            material.SetColor("_HColor",Color.white);
            material.SetColor("_SColor",new Color(.45f,.48f,.58f,1));
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor",name=="FurnaceGlow"?new Color(1,.24f,.02f):Color.white);
            material.SetColor("_EmissionColor",name=="FurnaceGlow"?new Color(1,.24f,.02f):Color.black);
            material.SetFloat("_EmissionStrength",name=="FurnaceGlow"?.22f:0);
            EditorUtility.SetDirty(material);return material;
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
            go.AddComponent<MeshRenderer>().sharedMaterial=material??(pilotMaterials.TryGetValue(name,out var pilot)?pilot:enamel);return go;
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
                            var p=piece.transform.localPosition;p.y=.32f;piece.transform.localPosition=p;
                            var plate=Model(piece.transform,"Plate");
                            if(pilotMaterials.ContainsKey("Plate"))
                            {
                                // Keep gameplay / break-effect anchors; inset only the visible dishes
                                // so the thicker frame never clips either end of the row.
                                plate.transform.localPosition=Vector3.right*(1-p.x)*.11f;
                                plate.transform.localScale=Vector3.one*.94f;
                            }
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

        public static void CaptureRippleFamily()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Ripple family camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=2.5f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.77f,.85f,.87f);
            camera.transform.position=new Vector3(0,8,-4.62f);camera.transform.LookAt(new Vector3(0,0,-.30f));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Soft studio key").AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-35,0);
            var names=new[]{"SeaMine","Switch","Gate","DoubleHitBlock","DirectionalArmor","MovingPanel"};
            var views=new List<GimmickView>();
            for(int i=0;i<names.Length;i++)
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+names[i]+".prefab"));
                go.transform.position=new Vector3((i%3-1)*1.8f,0,i<3?1.1f:-.8f);
                var view=go.GetComponent<GimmickView>();views.Add(view);
                // The contact sheet shows the bumper at the same scale as the other props.
                // Its full travel track is reviewed in the individual and gameplay captures.
                if(view.rail!=null)view.rail.gameObject.SetActive(false);
                if(view.railStops!=null)foreach(var stop in view.railStops)stop.gameObject.SetActive(false);
            }
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            for(int i=0;i<p.blocks.Length;i++)
            {
                var box=Object.Instantiate(p.blocks[i]);
                box.transform.position=new Vector3((i-1.5f)*.65f,0,-2.3f);
                box.transform.localScale=Vector3.one*.52f;
            }
            Directory.CreateDirectory("TestResults/RippleGimmickReview");
            foreach(bool active in new[]{false,true})
            {
                views[1].ShowActive(active);views[3].ShowActive(active);
                views[2].pieces[0].SetActive(!active);views[2].activeVisual.SetActive(active);
                var target=new RenderTexture(1600,1100,24){antiAliasing=4};target.Create();
                var previous=RenderTexture.active;camera.targetTexture=target;
                Texture2D image=null;
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                    RenderTexture.active=target;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();
                    File.WriteAllBytes("TestResults/RippleGimmickReview/"+(active?"active":"idle")+".png",image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active=previous;camera.targetTexture=null;
                    if(image!=null)Object.DestroyImmediate(image);
                    target.Release();Object.DestroyImmediate(target);
                }
            }
        }

        static void CapturePilot(string suffix)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var camera=new GameObject("Box comparison camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=2.6f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.77f,.85f,.87f);
            camera.transform.position=new Vector3(0,8,-4.62f);camera.transform.LookAt(Vector3.zero);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Soft studio key").AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-35,0);
            Directory.CreateDirectory("TestResults/GimmickPilotReview");
            foreach(string name in new[]{"Cupboard","MetalTube"})
            {
                var stage=new GameObject("Comparison");
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+".prefab"),stage.transform);
                go.transform.position=name=="Cupboard"?new Vector3(-1.65f,0,.65f):new Vector3(-1.7f,0,.2f);
                for(int i=0;i<3;i++)
                {
                    var box=Object.Instantiate(p.blocks[i%p.blocks.Length],stage.transform);
                    box.transform.position=new Vector3(i*1.05f-1.05f,0,-1.65f);
                }
                var target=new RenderTexture(1200,900,24){antiAliasing=4};target.Create();
                var previous=RenderTexture.active;camera.targetTexture=target;
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                    RenderTexture.active=target;var image=new Texture2D(1200,900,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,1200,900),0,0);image.Apply();
                    File.WriteAllBytes("TestResults/GimmickPilotReview/"+name+"-"+suffix+".png",image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally
                {
                    RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);
                    Object.DestroyImmediate(stage);
                }
            }
        }
    }
}
