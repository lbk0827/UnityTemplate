using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BK.Kit
{
    // Cosmetic geometry only. The gameplay walls remain owned by BounceModule.
    public sealed class LagoonArena : MonoBehaviour
    {
        private readonly Dictionary<Color,Material> palette=new Dictionary<Color,Material>();
        private readonly List<Mesh> meshes=new List<Mesh>();
        private Material source;
        private Transform decor;
        private Mesh bevelBox;
        private static readonly Color Stone=new Color(.78f,.67f,.45f);
        private static readonly Color Gold=new Color(.82f,.49f,.12f);
        private static readonly Color Teal=new Color(.10f,.40f,.40f);

        public void Build(int columns,Material material,int gridRows=13)
        {
            source=material;
            bevelBox=CreateBevelBox();meshes.Add(bevelBox);
            decor=new GameObject("Garden harbour scenery").transform;decor.SetParent(transform,false);
            float half=columns*.5f,extra=gridRows-13,top=gridRows-5;
            Shape("Lagoon water",PrimitiveType.Cube,new Vector3(0,-.3f,0),new Vector3(200,.2f,200),new Color(.035f,.43f,.7f));
            Shape("Garden foundation",PrimitiveType.Cube,new Vector3(0,-.25f,.5f+extra*.5f),new Vector3(columns+4.5f,.4f,gridRows+2.5f),new Color(.25f,.32f,.12f));
            Shape("Lawn bed",PrimitiveType.Cube,new Vector3(0,-.12f,.25f+extra*.5f),new Vector3(columns,.1f,gridRows+1.5f),new Color(.22f,.44f,.035f));
            for(int row=0;row<gridRows+1;row++)
                for(int col=0;col<columns;col++)
                    Shape("Soft lawn square",PrimitiveType.Cube,new Vector3(col-(columns-1)*.5f,-.04f,-6.5f+row),
                        new Vector3(1,.035f,1),(row+col)%2==0?new Color(.36f,.61f,.055f):new Color(.41f,.66f,.075f));
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(half+.18f);
                Shape("Limestone wall face",PrimitiveType.Cube,new Vector3(x,.5f,1.25f+extra*.5f),new Vector3(.36f,1,gridRows+1.5f),new Color(.66f,.54f,.34f));
                Shape("Limestone rail",PrimitiveType.Cube,new Vector3(x,1.07f,1.25f+extra*.5f),new Vector3(.44f,.16f,gridRows+1.5f),Stone);
                Shape("Rail light edge",PrimitiveType.Cube,new Vector3(x-side*.14f,1.17f,1.25f+extra*.5f),new Vector3(.08f,.05f,gridRows+1.5f),new Color(.94f,.82f,.55f));
                Shape("Garden soil",PrimitiveType.Cube,new Vector3(side*(half+.76f),.1f,1.25f+extra*.5f),new Vector3(.85f,.25f,gridRows+1.5f),new Color(.14f,.25f,.04f));
                for(int i=0;i<Mathf.CeilToInt((gridRows+1)/2.4f);i++)
                {
                    float z=-5.8f+i*2.4f;
                    Shape("Stone rail joint",PrimitiveType.Cube,new Vector3(x,1.18f,z),new Vector3(.48f,.08f,.09f),new Color(.65f,.53f,.32f));
                    Bush(side*(half+.78f),z,i);
                    Flower(side*(half+.60f),z+.62f,i);
                    Flower(side*(half+.89f),z+.96f,i+1);
                }
            }
            Shape("Garden upper coping",PrimitiveType.Cube,new Vector3(0,1.22f,top),new Vector3(columns+2.3f,.2f,1.2f),Stone);
            Shape("Upper brass trim",PrimitiveType.Cube,new Vector3(0,1.36f,top),new Vector3(columns+2.3f,.06f,.16f),Gold);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(half+.5f);
                Shape("Gate pillar",PrimitiveType.Cube,new Vector3(x,1.3f,top),new Vector3(.72f,1.8f,.85f),Stone);
                Shape("Gate finial",PrimitiveType.Sphere,new Vector3(x,2.25f,top),new Vector3(.68f,.68f,.68f),Gold);
            }
            Shape("Compass rim",PrimitiveType.Cylinder,new Vector3(0,1.43f,top),new Vector3(1.1f,.08f,1.1f),Gold);
            Shape("Compass face",PrimitiveType.Cylinder,new Vector3(0,1.53f,top),new Vector3(.87f,.04f,.87f),Teal);
            Shape("Compass diamond",PrimitiveType.Cube,new Vector3(0,1.6f,top),new Vector3(.34f,.06f,.34f),Stone,45);
            Shape("Garden shoreline",PrimitiveType.Cube,new Vector3(0,-.01f,BounceModule.DeadZoneZ),new Vector3(columns,.12f,.17f),Stone);
            // Timber wharf and a separate narrow water channel frame the launcher.
            for(int i=0;i<columns+4;i++)
            {
                float x=i-(columns+3)*.5f;
                Shape("Dock plank",PrimitiveType.Cube,new Vector3(x,-.14f,-9.85f),new Vector3(.97f,.24f,3.35f),i%2==0?new Color(.51f,.24f,.07f):new Color(.61f,.31f,.10f));
                Shape("Plank grain",PrimitiveType.Cube,new Vector3(x+.3f,-.005f,-9.9f),new Vector3(.025f,.015f,2.8f),new Color(.35f,.15f,.04f));
            }
            Shape("Dock brass edge",PrimitiveType.Cube,new Vector3(0,.02f,-8.16f),new Vector3(columns+4,.16f,.17f),Gold);
            Shape("Launcher plinth",PrimitiveType.Cylinder,new Vector3(0,-.04f,-8.6f),new Vector3(3.1f,.05f,3.1f),new Color(.15f,.27f,.30f));
            Shape("Launcher brass ring",PrimitiveType.Cylinder,new Vector3(0,-.01f,-8.6f),new Vector3(2.8f,.025f,2.8f),Gold);
            Shape("Launcher inset",PrimitiveType.Cylinder,new Vector3(0,.025f,-8.6f),new Vector3(2.55f,.015f,2.55f),Teal);
            for(int i=0;i<columns*3;i++)
            {
                float x=Mathf.Sin(i*7.13f)*(half+2),z=-7.25f-(i%4)*.22f;
                Shape("Water ripple",PrimitiveType.Cube,new Vector3(x,-.18f,z),new Vector3(.35f+i%3*.13f,.015f,.035f),new Color(.16f,.7f,.87f),-8);
            }
            CombineScenery();
        }
        private void Bush(float x,float z,int index)
        {
            for(int i=0;i<4;i++)
            {
                float a=i*2.4f;
                Shape("Rounded hedge",PrimitiveType.Sphere,new Vector3(x+Mathf.Cos(a)*.23f,.48f+(i%2)*.16f,z+Mathf.Sin(a)*.3f),
                    new Vector3(.74f,.8f,.9f),i%2==0?new Color(.12f,.40f,.055f):new Color(.22f,.53f,.065f));
            }
        }
        private void Flower(float x,float z,int index)
        {
            var colors=new[]{new Color(.98f,.28f,.22f),new Color(.67f,.25f,.85f),new Color(1,.71f,.09f),new Color(.96f,.91f,.72f)};
            Shape("Flower leaf",PrimitiveType.Sphere,new Vector3(x,.55f,z),new Vector3(.65f,.25f,.7f),new Color(.17f,.46f,.05f));
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5;
                Shape("Flower petal",PrimitiveType.Sphere,new Vector3(x+Mathf.Cos(a)*.17f,.77f,z+Mathf.Sin(a)*.17f),new Vector3(.25f,.13f,.28f),colors[index%4]);
            }
            Shape("Flower heart",PrimitiveType.Sphere,new Vector3(x,.84f,z),new Vector3(.19f,.13f,.19f),new Color(1,.77f,.17f));
        }

        private void Shape(string label,PrimitiveType type,Vector3 position,Vector3 scale,Color color,float yaw=0)
        {
            var go=GameObject.CreatePrimitive(type);go.name=label;go.layer=2;
            go.transform.SetParent(decor,false);go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            if(type==PrimitiveType.Cube && label!="Soft lawn square" && label!="Lawn bed" && label!="Lagoon water")go.GetComponent<MeshFilter>().sharedMesh=bevelBox;
            if(!palette.TryGetValue(color,out var material))
            {
                material=new Material(source){color=color};palette.Add(color,material);
            }
            go.GetComponent<Renderer>().sharedMaterial=material;
        }

        private static Mesh CreateBevelBox()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var outline=new[] {new Vector2(-.42f,-.5f),new Vector2(.42f,-.5f),new Vector2(.5f,-.42f),new Vector2(.5f,.42f),
                new Vector2(.42f,.5f),new Vector2(-.42f,.5f),new Vector2(-.5f,.42f),new Vector2(-.5f,-.42f)};
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
            }
            for(int i=0;i<8;i++)
            {
                var a=outline[i];var b=outline[(i+1)%8];
                var topA=new Vector3(a.x*.86f,.5f,a.y*.86f);var topB=new Vector3(b.x*.86f,.5f,b.y*.86f);
                var rimA=new Vector3(a.x,.22f,a.y);var rimB=new Vector3(b.x,.22f,b.y);
                var baseA=new Vector3(a.x,-.5f,a.y);var baseB=new Vector3(b.x,-.5f,b.y);
                Triangle(new Vector3(0,.5f,0),topB,topA);
                Triangle(topA,topB,rimB);Triangle(topA,rimB,rimA);
                Triangle(rimA,rimB,baseB);Triangle(rimA,baseB,baseA);
                Triangle(new Vector3(0,-.5f,0),baseA,baseB);
            }
            var mesh=new Mesh {name="Beveled terrace stone"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private void CombineScenery()
        {
            // One draw per palette colour rather than one draw per tile or leaf.
            var filters=decor.GetComponentsInChildren<MeshFilter>();
            foreach(var material in palette.Values)
            {
                var instances=new List<CombineInstance>();
                foreach(var filter in filters)
                    if(filter.GetComponent<Renderer>().sharedMaterial==material)
                        instances.Add(new CombineInstance {mesh=filter.sharedMesh,transform=decor.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                var mesh=new Mesh {name="Lagoon scenery",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(instances.ToArray());meshes.Add(mesh);
                var batch=new GameObject("Lagoon palette batch",typeof(MeshFilter),typeof(MeshRenderer));batch.layer=2;
                batch.transform.SetParent(decor,false);batch.GetComponent<MeshFilter>().sharedMesh=mesh;
                batch.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            foreach(var filter in filters) {filter.gameObject.SetActive(false);Destroy(filter.gameObject);}
        }
        private void OnDestroy()
        {
            foreach(var material in palette.Values)Destroy(material);
            foreach(var mesh in meshes)Destroy(mesh);
        }
    }
}
