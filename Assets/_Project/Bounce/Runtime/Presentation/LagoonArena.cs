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
        private static readonly Color Stone=new Color(.96f,.86f,.65f);
        private static readonly Color Gold=new Color(.94f,.65f,.25f);
        private static readonly Color Teal=new Color(.10f,.40f,.40f);

        public void Build(int columns,Material material,int gridRows=13)
        {
            source=material;
            bevelBox=CreateBevelBox();meshes.Add(bevelBox);
            decor=new GameObject("Lagoon terrace • scenery").transform;decor.SetParent(transform,false);
            float half=columns*.5f, extra=gridRows-13, top=gridRows-5;
            Shape("Lagoon",PrimitiveType.Cube,new Vector3(0,-.26f,extra*.5f),new Vector3(200,.2f,200),new Color(.10f,.48f,.59f));
            Shape("Terrace foundation",PrimitiveType.Cube,new Vector3(0,-.18f,1+extra*.5f),new Vector3(columns+2.8f,.3f,gridRows+3.2f),Teal);
            Shape("Tile grout",PrimitiveType.Cube,new Vector3(0,-.08f,.25f+extra*.5f),new Vector3(columns,.1f,gridRows+1.5f),new Color(.24f,.57f,.54f));
            for(int row=0;row<gridRows+2;row++)
                for(int col=0;col<columns;col++)
                    Shape("Glazed sea tile",PrimitiveType.Cube,new Vector3(col-(columns-1)*.5f,-.035f,-6.5f+row),
                        new Vector3(.98f,.055f,.98f),(row+col)%2==0?new Color(.32f,.66f,.59f):new Color(.35f,.69f,.62f));
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(half+.5f);
                Shape("Gold wall inlay",PrimitiveType.Cube,new Vector3(x,1.215f,1.25f+extra*.5f),new Vector3(.13f,.035f,gridRows+1.5f),Gold);
                for(int i=0;i<Mathf.CeilToInt((gridRows+1)/1.8f);i++)
                {
                    float z=-5.5f+i*1.8f;
                    Shape("Pillar foot",PrimitiveType.Cube,new Vector3(x,.72f,z),new Vector3(1.16f,1.45f,.52f),Stone);
                    Shape("Pillar cap",PrimitiveType.Cube,new Vector3(x,1.47f,z),new Vector3(1.23f,.24f,.64f),Stone);
                    Shape("Pillar inset",PrimitiveType.Cube,new Vector3(x,1.61f,z),new Vector3(.64f,.035f,.3f),Gold);
                }
                for(int i=0;i<Mathf.CeilToInt((gridRows-1)/2.8f);i++)
                {
                    float z=-4.8f+i*2.8f;
                    Shape("Planter",PrimitiveType.Cylinder,new Vector3(side*(half+1.22f),.14f,z),new Vector3(.95f,.25f,.95f),Stone);
                    for(int leaf=0;leaf<5;leaf++)
                    {
                        float a=leaf*Mathf.PI*2/5;
                        Shape("Succulent",PrimitiveType.Sphere,new Vector3(side*(half+1.22f)+Mathf.Cos(a)*.25f,.5f,z+Mathf.Sin(a)*.25f),
                            new Vector3(.34f,.32f,.7f),leaf%2==0?new Color(.15f,.48f,.35f):new Color(.30f,.65f,.40f),-a*Mathf.Rad2Deg);
                    }
                    Shape("Coral blossom",PrimitiveType.Sphere,new Vector3(side*(half+1.22f),.76f,z),new Vector3(.32f,.25f,.32f),new Color(1,.48f,.38f));
                }
            }
            Shape("Upper coping",PrimitiveType.Cube,new Vector3(0,1.22f,top),new Vector3(columns+2.3f,.16f,1.2f),Stone);
            Shape("Upper gold inlay",PrimitiveType.Cube,new Vector3(0,1.32f,top),new Vector3(columns+2.3f,.045f,.12f),Gold);
            Shape("Sun medallion rim",PrimitiveType.Cylinder,new Vector3(0,1.4f,top),new Vector3(1.35f,.09f,1.35f),Gold);
            Shape("Sun medallion",PrimitiveType.Cylinder,new Vector3(0,1.51f,top),new Vector3(1.08f,.035f,1.08f),Teal);
            Shape("Compass jewel",PrimitiveType.Cube,new Vector3(0,1.58f,top),new Vector3(.42f,.08f,.42f),Stone,45);
            // A visible shoreline corresponds to the preview and returning-ball cutoff.
            Shape("Shoreline lip",PrimitiveType.Cube,new Vector3(0,-.01f,BounceModule.DeadZoneZ),new Vector3(columns,.12f,.13f),Stone);
            Shape("Launcher dock rim",PrimitiveType.Cylinder,new Vector3(0,-.035f,-8.6f),new Vector3(3.3f,.13f,3.3f),Gold);
            Shape("Launcher dock",PrimitiveType.Cylinder,new Vector3(0,.10f,-8.6f),new Vector3(2.95f,.08f,2.95f),Stone);
            for(int i=0;i<28;i++)
            {
                float x=Mathf.Sin(i*7.13f)*(half+3);
                float z=-7.5f-(i%6)*.7f;
                if(Mathf.Abs(x)<1.9f && z>-10.5f)continue;
                Shape("Water glint",PrimitiveType.Cube,new Vector3(x,-.145f,z),new Vector3(.35f+(i%3)*.2f,.018f,.04f),new Color(.29f,.67f,.72f),-12);
            }
            CombineScenery();
        }

        private void Shape(string label,PrimitiveType type,Vector3 position,Vector3 scale,Color color,float yaw=0)
        {
            var go=GameObject.CreatePrimitive(type);go.name=label;go.layer=2;
            go.transform.SetParent(decor,false);go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            if(type==PrimitiveType.Cube)go.GetComponent<MeshFilter>().sharedMesh=bevelBox;
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
