using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    // Only the explicit creation menu generates geometry. Existing prefab edits are preserved.
    public sealed class GimmickPrefabBuilder
    {
        public const string Root = "Assets/_Project/Bounce/Content/Gimmicks";
        private Transform transform;
        private Material source;
        private sealed class Target
        {
            public GimmickSpec spec;
            public Transform root, furnace;
            public Vector3 origin;
            public BoxCollider collider;
            public Renderer[] furnaceGlow;
            public readonly List<GameObject> pieces = new List<GameObject>();
            public readonly List<Vector3> cells = new List<Vector3>();
        }

        [MenuItem("BK Kit/Gimmicks/Create Missing Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
            var presentation = AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var builder = new GimmickPrefabBuilder { source = presentation.arenaMaterial };
            var catalog = AssetDatabase.LoadAssetAtPath<GimmickPrefabCatalog>(Root + "/GimmickPrefabs.asset");
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<GimmickPrefabCatalog>(); AssetDatabase.CreateAsset(catalog, Root + "/GimmickPrefabs.asset"); }
            catalog.tube = builder.Bake("MetalTube", new GimmickSpec {type="tube",length=3});
            catalog.cupboard = builder.Bake("Cupboard", new GimmickSpec {type="cupboard"});
            catalog.panel = builder.Bake("MovingPanel", new GimmickSpec {type="panel",width=2,travel=2});
            catalog.mine = builder.Bake("SeaMine", new GimmickSpec {type="mine"});
            catalog.switchButton = builder.Bake("Switch", new GimmickSpec {type="switch"});
            catalog.door = builder.Bake("Gate", new GimmickSpec {type="door"});
            catalog.combo = builder.Bake("DoubleHitBlock", new GimmickSpec {type="combo"});
            catalog.armor = builder.Bake("DirectionalArmor", new GimmickSpec {type="armor"});
            catalog.tubeSegment = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/TubeSegment.prefab");
            catalog.shard = builder.Effect("PorcelainShard", false);
            catalog.blast = builder.Effect("MineBurst", true);
            catalog.link = builder.Link(presentation.trajectoryMaterial);
            EditorUtility.SetDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<IngameConfig>("Assets/_Project/Bounce/Content/IngameConfig.asset");
            config.gimmickPrefabs = catalog; EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        private GimmickView Bake(string name, GimmickSpec spec)
        {
            string path = Root + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GimmickView>(path);
            if (existing != null) return existing;
            var go = new GameObject(name); transform = go.transform;
            var view = go.AddComponent<GimmickView>();
            var t = new Target { spec = spec, root = transform };
            if (spec.type == "tube")
            {
                BuildTube(t);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/TubeSegment.prefab") == null)
                    PrefabUtility.SaveAsPrefabAsset(t.pieces[0], Root + "/TubeSegment.prefab");
                view.furnace = t.furnace; view.furnaceGlow = t.furnaceGlow;
            }
            else if (spec.type == "cupboard") BuildCupboard(t);
            else if (spec.type == "panel")
            {
                t.root = new GameObject("Moving body").transform; t.root.SetParent(transform, false);
                BuildPanel(t, 9); view.movingBody = t.root;
                view.rail = transform.Find("Panel travel rail");
                var stops = new List<Transform>();
                foreach (Transform child in transform) if (child.name == "Rail stop") stops.Add(child);
                view.railStops = stops.ToArray();
            }
            else BuildSpecial(t, view);
            view.solid = t.collider; view.pieces = t.pieces.ToArray();
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return saved.GetComponent<GimmickView>();
        }

        private Material Paint(Color color, bool metal = false)
        {
            string path = Root + "/Materials/" + ColorUtility.ToHtmlStringRGBA(color) + (metal ? "_Metal" : "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(source) { color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal ? .65f : 0);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .45f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 2;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        private static BoxCollider Solid(GameObject go, Vector3 size, Vector3 center = default)
        {
            go.layer = 10; var box = go.AddComponent<BoxCollider>(); box.size = size; box.center = center; return box;
        }

        private void BuildTube(Target t)
        {
            var steel = Paint(new Color(.56f, .65f, .76f), true);
            var brass = Paint(new Color(.33f, .40f, .53f), true);
            var dark = Paint(new Color(.055f, .13f, .16f));
            BuildFurnace(t);
            for (int i = 0; i < Mathf.Max(1, t.spec.length); i++)
            {
                var cell = new GameObject("Tube segment " + i); cell.transform.SetParent(t.root, false);
                cell.transform.localPosition = t.spec.vertical ? Vector3.back * i : Vector3.right * i;
                Solid(cell, t.spec.vertical ? new Vector3(.76f, .85f, 1) : new Vector3(1, .85f, .76f));
                var visual = new GameObject("Pipe visual").transform; visual.SetParent(cell.transform, false);
                var body = Shape(visual, "Steel pipe", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.7f, .5f, .7f), steel);
                body.transform.localRotation = Quaternion.Euler(t.spec.vertical ? 90 : 0, 0, t.spec.vertical ? 0 : 90);
                for (int end = -1; end <= 1; end += 2)
                {
                    var offset = (t.spec.vertical ? Vector3.forward : Vector3.right) * (end * .43f);
                    var band = Shape(visual, "Steel seam", PrimitiveType.Cylinder, offset, new Vector3(.8f, .045f, .8f), brass);
                    band.transform.localRotation = body.transform.localRotation;
                }
                var cap = Shape(visual, "Dark bore", PrimitiveType.Cylinder,
                    (t.spec.vertical ? Vector3.back : Vector3.right) * .495f, new Vector3(.51f, .006f, .51f), dark);
                cap.transform.localRotation = body.transform.localRotation;
                t.pieces.Add(cell); t.cells.Add(cell.transform.position);
            }
        }

        private void BuildFurnace(Target t)
        {
            var housing = Paint(new Color(.29f, .09f, .56f), true);
            var trim = Paint(new Color(.61f, .35f, .85f), true);
            var black = Paint(new Color(.07f, .025f, .12f));
            var fire = Paint(new Color(1, .24f, .015f));
            fire.EnableKeyword("_EMISSION"); fire.SetColor("_EmissionColor", new Color(1, .24f, .015f)); EditorUtility.SetDirty(fire);
            t.furnace = new GameObject("Furnace").transform;
            t.furnace.SetParent(t.root, false);
            t.furnace.localPosition = t.spec.vertical ? Vector3.forward : Vector3.left;
            t.cells.Add(t.furnace.position);
            t.collider = Solid(t.furnace.gameObject, new Vector3(.96f, 1, .96f));
            // Rounded purple housing with three orange vents, visible from the gameplay camera.
            Shape(t.furnace, "Rounded furnace shell", PrimitiveType.Cube, Vector3.zero, new Vector3(.86f, .75f, .86f), housing);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                Shape(t.furnace, "Rounded corner", PrimitiveType.Cylinder, new Vector3(x * .34f, 0, z * .34f), new Vector3(.22f, .39f, .22f), trim);
            Shape(t.furnace, "Vent recess", PrimitiveType.Cube, new Vector3(0, .39f, 0), new Vector3(.69f, .04f, .75f), black);
            var glows = new List<Renderer>();
            for (int i = -1; i <= 1; i++)
            {
                glows.Add(Shape(t.furnace, "Molten vent", PrimitiveType.Capsule, new Vector3(0, .43f, i * .23f), new Vector3(.15f, .24f, .15f), fire).GetComponent<Renderer>());
                glows[glows.Count - 1].transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            var mouth = Shape(t.furnace, "Hot intake", PrimitiveType.Cylinder, t.spec.vertical ? Vector3.back * .46f : Vector3.right * .46f,
                new Vector3(.67f, .045f, .67f), fire);
            mouth.transform.localRotation = Quaternion.Euler(t.spec.vertical ? 90 : 0, 0, t.spec.vertical ? 0 : 90);
            glows.Add(mouth.GetComponent<Renderer>()); t.furnaceGlow = glows.ToArray();
        }

        private void BuildCupboard(Target t)
        {
            // Three columns by two rows: ten perimeter cells, one for each visible plate.
            var wood = Paint(new Color(.30f, .14f, .07f));
            var edge = Paint(new Color(.77f, .45f, .19f));
            var porcelain = Paint(new Color(.94f, .98f, .92f));
            var glaze = Paint(new Color(.07f, .60f, .68f));
            for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
                t.cells.Add(t.origin + new Vector3(col, 0, -row));
            t.collider = Solid(t.root.gameObject, new Vector3(2.96f, 1, 1.96f), new Vector3(1, 0, -.5f));
            Shape(t.root, "Cabinet backing", PrimitiveType.Cube, new Vector3(1, -.31f, -.5f), new Vector3(2.95f, .35f, 1.95f), wood);
            for (int side = -1; side <= 1; side += 2)
            {
                Shape(t.root, "Side frame", PrimitiveType.Cube, new Vector3(1 + side * 1.43f, .05f, -.5f), new Vector3(.14f, .8f, 1.95f), edge);
                Shape(t.root, "End frame", PrimitiveType.Cube, new Vector3(1, .05f, -.5f + side * .93f), new Vector3(2.95f, .8f, .14f), edge);
            }
            Shape(t.root, "Middle shelf", PrimitiveType.Cube, new Vector3(1, .02f, -.5f), new Vector3(2.8f, .65f, .09f), edge);
            for (int i = 0; i < 10; i++)
            {
                var plate = new GameObject("Porcelain plate " + i); plate.transform.SetParent(t.root, false);
                plate.transform.localPosition = new Vector3(-.1f + i % 5 * .55f, .18f, -.03f - i / 5 * .95f);
                Shape(plate.transform, "Plate rim", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.48f, .055f, .48f), porcelain);
                Shape(plate.transform, "Glazed centre", PrimitiveType.Cylinder, new Vector3(0, .06f, 0), new Vector3(.32f, .012f, .32f), glaze);
                t.pieces.Add(plate);
            }
        }

        private void BuildPanel(Target t, int columns)
        {
            float width = Mathf.Clamp(t.spec.width, .5f, columns - .5f);
            t.spec.width = width;
            float room = Mathf.Max(0, columns * .5f - Mathf.Abs(t.origin.x) - width * .5f - .1f);
            t.spec.travel = Mathf.Clamp(t.spec.travel, 0, room);
            var brass = Paint(new Color(.96f, .69f, .22f), true);
            var teal = Paint(new Color(.06f, .34f, .39f), true);
            Shape(transform, "Panel travel rail", PrimitiveType.Cube, t.origin + new Vector3(0, -.44f, 0), new Vector3(t.spec.travel * 2 + width, .09f, .16f), teal);
            for (int side = -1; side <= 1; side += 2)
                Shape(transform, "Rail stop", PrimitiveType.Cylinder, t.origin + new Vector3(side * (t.spec.travel + width * .5f), -.36f, 0), new Vector3(.22f, .12f, .22f), brass);
            t.collider = Solid(t.root.gameObject, new Vector3(width, 1, .4f));
            Shape(t.root, "Moving brass plate", PrimitiveType.Cube, Vector3.zero, new Vector3(width, .85f, .4f), brass);
            Shape(t.root, "Teal inset", PrimitiveType.Cube, new Vector3(0, .44f, 0), new Vector3(width - .18f, .03f, .28f), teal);
        }

        private void BuildSpecial(Target t, GimmickView view)
        {
            var steel = Paint(new Color(.18f,.28f,.40f), true);
            var gold = Paint(new Color(.98f,.67f,.15f), true);
            var red = Paint(new Color(1,.24f,.12f));
            var cyan = Paint(new Color(.1f,.9f,.9f));
            var dark = Paint(new Color(.06f,.1f,.18f));
            var purple = Paint(new Color(.5f,.2f,.8f),true);
            t.collider = Solid(t.root.gameObject, new Vector3(.96f,1,.96f));
            var visual = new GameObject("Visual").transform; visual.SetParent(t.root,false);
            t.pieces.Add(visual.gameObject);
            if(t.spec.type == "mine")
            {
                Shape(visual,"Mine shell",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.78f,steel);
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    var spike=Shape(visual,"Detonator spike",PrimitiveType.Capsule,new Vector3(Mathf.Sin(angle)*.35f,0,Mathf.Cos(angle)*.35f),new Vector3(.13f,.22f,.13f),gold);
                    spike.transform.rotation=Quaternion.LookRotation(new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle)))*Quaternion.Euler(90,0,0);
                }
                Shape(visual,"Red detonator",PrimitiveType.Sphere,new Vector3(0,.38f,0),new Vector3(.38f,.13f,.38f),red);
                Shape(visual,"Warning stripe",PrimitiveType.Cube,new Vector3(0,.24f,-.3f),new Vector3(.5f,.12f,.12f),gold);
            }
            else if(t.spec.type == "switch")
            {
                Shape(visual,"Switch socket",PrimitiveType.Cylinder,new Vector3(0,-.17f,0),new Vector3(.92f,.22f,.92f),steel);
                view.inactiveVisual=Shape(visual,"Unlit button",PrimitiveType.Cylinder,new Vector3(0,.1f,0),new Vector3(.65f,.13f,.65f),red);
                view.activeVisual=Shape(visual,"Lit button",PrimitiveType.Cylinder,new Vector3(0,.04f,0),new Vector3(.65f,.10f,.65f),cyan);
                view.activeVisual.SetActive(false);
            }
            else if(t.spec.type == "door")
            {
                for(int side=-1;side<=1;side+=2)
                    Shape(t.root,"Gate post",PrimitiveType.Cube,new Vector3(side*.4f,0,0),new Vector3(.17f,1.2f,.42f),steel);
                Shape(t.root,"Gate lintel",PrimitiveType.Cube,new Vector3(0,.6f,0),new Vector3(.97f,.15f,.44f),gold);
                for(int i=-1;i<=1;i++)Shape(visual,"Gate bar",PrimitiveType.Cube,new Vector3(i*.23f,0,0),new Vector3(.12f,1,.22f),gold);
                view.inactiveVisual=visual.gameObject;
                view.activeVisual=Shape(t.root,"Open lamp",PrimitiveType.Sphere,new Vector3(0,.72f,0),new Vector3(.2f,.1f,.2f),cyan);
                view.activeVisual.SetActive(false);
            }
            else if(t.spec.type == "combo")
            {
                view.inactiveVisual=new GameObject("Closed shell");view.inactiveVisual.transform.SetParent(visual,false);
                Shape(view.inactiveVisual.transform,"Sealed core",PrimitiveType.Cube,Vector3.zero,new Vector3(.88f,.8f,.88f),purple);
                Shape(view.inactiveVisual.transform,"Core seam",PrimitiveType.Cube,new Vector3(0,.41f,0),new Vector3(.08f,.035f,.88f),gold);
                view.activeVisual=new GameObject("Open shell");view.activeVisual.transform.SetParent(visual,false);
                Shape(view.activeVisual.transform,"Exposed glowing core",PrimitiveType.Sphere,Vector3.zero,new Vector3(.55f,.7f,.55f),cyan);
                for(int i=0;i<4;i++)
                {
                    float angle=i*Mathf.PI*.5f;
                    var petal=Shape(view.activeVisual.transform,"Open armor petal",PrimitiveType.Cube,new Vector3(Mathf.Sin(angle)*.35f,0,Mathf.Cos(angle)*.35f),new Vector3(.3f,.65f,.12f),purple);
                    petal.transform.localRotation=Quaternion.Euler(20,i*90,0);
                }
                view.activeVisual.SetActive(false);
            }
            else if(t.spec.type == "armor")
            {
                Shape(visual,"Armor core",PrimitiveType.Cube,Vector3.zero,new Vector3(.75f,.7f,.75f),dark);
                Shape(visual,"Back armor",PrimitiveType.Cube,new Vector3(0,0,.4f),new Vector3(.93f,.95f,.17f),steel);
                for(int side=-1;side<=1;side+=2)Shape(visual,"Side armor",PrimitiveType.Cube,new Vector3(side*.4f,0,0),new Vector3(.17f,.95f,.9f),steel);
                Shape(visual,"Exposed weak face",PrimitiveType.Cube,new Vector3(0,0,-.39f),new Vector3(.6f,.6f,.1f),red);
                for(int side=-1;side<=1;side+=2)
                {
                    var arrow=Shape(visual,"Weak side arrow",PrimitiveType.Cube,new Vector3(side*.12f,.39f,-.12f),new Vector3(.1f,.04f,.4f),gold);
                    arrow.transform.localRotation=Quaternion.Euler(0,side*40,0);
                }
            }
        }

        private GameObject Effect(string name,bool burst)
        {
            string path=Root+"/"+name+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var go=new GameObject(name);go.layer=2;
            if(!burst)Shape(go.transform,"Shard",PrimitiveType.Cube,Vector3.zero,new Vector3(.13f,.045f,.1f),Paint(new Color(.94f,.98f,.92f)));
            else
            {
                var hot=Paint(new Color(1,.58f,.05f));
                Shape(go.transform,"Flash",PrimitiveType.Sphere,Vector3.zero,new Vector3(1,.2f,1),hot);
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    var ray=Shape(go.transform,"Blast ray",PrimitiveType.Cube,new Vector3(Mathf.Sin(angle)*.65f,0,Mathf.Cos(angle)*.65f),new Vector3(.1f,.1f,.6f),hot);
                    ray.transform.localRotation=Quaternion.Euler(0,i*45,0);
                }
            }
            var saved=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);return saved;
        }

        private GameObject Link(Material material)
        {
            string path=Root+"/SwitchLink.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var go=new GameObject("SwitchLink",typeof(LineRenderer));go.layer=2;
            var line=go.GetComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.startWidth=line.endWidth=.045f;line.positionCount=2;
            var saved=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);return saved;
        }
    }
}
