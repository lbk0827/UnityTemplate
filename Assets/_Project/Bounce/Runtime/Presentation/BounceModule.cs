using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BK.Kit
{
    public sealed class BounceModule : GameModule
    {
        [Serializable] public sealed class Level { public int balls; public string[] rows; }
        [Serializable] public sealed class Levels { public Level[] levels; }
        [Serializable] private sealed class Block { public Transform root; public GameObject visual; public int hp; }
        private sealed class Shot { public GameObject visual; public Vector3 position, direction; public float age; }
        public GamePresentation presentation;
        public int BallsRemaining { get; private set; }
        public int BlocksRemaining => blocks.Count;
        public int ActiveBalls => shots.Count;
        public bool IsUsingBooster { get; private set; }
        public event Action CountersChanged;
        private readonly List<Block> blocks = new List<Block>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Material> materials = new List<Material>();
        private GameObject rig, cannon;
        private Camera camera;
        private LineRenderer line;
        private AudioSource audioSource;
        private Action<bool> completed;
        private bool playing, holding, lost;
        private int columns;
        private Vector3 aim = Vector3.forward;
        private const int SolidLayer = 10;
        private const float Radius = .25f, Height = .5f, Speed = 18f;

        public override void Begin(int level, Action<bool> onCompleted)
        {
            End();
            completed = onCompleted;
            var library = JsonUtility.FromJson<Levels>(presentation.levels.text);
            var data = library.levels[(Mathf.Max(1, level)-1)%library.levels.Length];
            columns = data.rows[0].Length;
            BallsRemaining = data.balls;
            rig = new GameObject("Bounce_Rig");
            camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max((columns*.5f+1)/Mathf.Max(.3f,camera.aspect),9.5f)+1;
            camera.transform.SetPositionAndRotation(new Vector3(0,20,1),Quaternion.Euler(90,0,0));
            camera.backgroundColor = new Color(.25f,.55f,.95f);
            audioSource = rig.AddComponent<AudioSource>();
            rig.AddComponent<KitAudioChannel>().Initialize(audioSource,KitAudioChannel.Category.Effects);
            BuildArena();
            for (int row=0;row<data.rows.Length;row++)
                for (int col=0;col<columns;col++)
                {
                    int hp=data.rows[row][col]-'0';
                    if (hp<1 || hp>9) continue;
                    var root=new GameObject("Block_"+col+"_"+row).transform;
                    root.SetParent(rig.transform,false);
                    root.position=new Vector3(col-(columns-1)*.5f,0,7-row);
                    var block=new Block {root=root,hp=hp};
                    blocks.Add(block); BuildBlock(block);
                }
            cannon=Instantiate(presentation.cannon,rig.transform);
            cannon.name="Bounce_Cannon";
            cannon.transform.position=new Vector3(0,0,-8.5f);
            cannon.transform.localScale=Vector3.one*1.8f;
            PrepareVisual(cannon,false);
            line=new GameObject("Aim trajectory",typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(rig.transform,false);
            line.sharedMaterial=presentation.trajectoryMaterial;
            line.startWidth=line.endWidth=.055f;
            line.startColor=line.endColor=new Color(1,1,1,.75f);
            line.enabled=false;
            playing=true;
            Physics.SyncTransforms();
            PushCounts();
        }

        private void BuildArena()
        {
            float half=columns*.5f;
            Color wall=new Color(.85f,.8f,.65f);
            Box("Wall left",new Vector3(-half-.5f,.6f,1.25f),new Vector3(1,1.2f,14.5f),0,wall,true);
            Box("Wall right",new Vector3(half+.5f,.6f,1.25f),new Vector3(1,1.2f,14.5f),0,wall,true);
            Box("Wall top",new Vector3(0,.6f,8),new Vector3(columns+2,1.2f,1),0,wall,true);
            float delta=1.5f*Mathf.Sqrt(.5f);
            Box("Bumper left",new Vector3(-half+delta,.6f,-6-delta),new Vector3(3,1.2f,.6f),45,wall,true);
            Box("Bumper right",new Vector3(half-delta,.6f,-6-delta),new Vector3(3,1.2f,.6f),135,wall,true);
            Box("Grass",new Vector3(0,-.08f,1.25f),new Vector3(columns+4,.1f,16.5f),0,new Color(.45f,.78f,.3f),false);
            Box("Water",new Vector3(0,-.1f,-10),new Vector3(columns+4,.1f,6),0,new Color(.25f,.55f,.95f),false);
        }
        private void Box(string name,Vector3 position,Vector3 scale,float yaw,Color color,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            go.transform.SetParent(rig.transform,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=scale;
            go.layer=solid?SolidLayer:2;
            if(!solid) Destroy(go.GetComponent<Collider>());
            var material=new Material(presentation.arenaMaterial);material.color=color;materials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        private void BuildBlock(Block block)
        {
            if(block.visual!=null) { block.visual.SetActive(false);Destroy(block.visual); }
            block.visual=Instantiate(presentation.blocks[Mathf.Clamp(block.hp-1,0,presentation.blocks.Length-1)],block.root);
            block.visual.transform.localPosition=Vector3.zero;block.visual.transform.localRotation=Quaternion.identity;
            PrepareVisual(block.visual,true);
        }
        private static void PrepareVisual(GameObject go,bool solid)
        {
            foreach(var body in go.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true;Destroy(body); }
            foreach(var joint in go.GetComponentsInChildren<Joint>(true)) Destroy(joint);
            foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=solid?SolidLayer:2;
            if(!solid) foreach(var collider in go.GetComponentsInChildren<Collider>(true)) {collider.enabled=false;Destroy(collider);}
        }
        private void Update()
        {
            if(!playing || IsUsingBooster || KitApp.Instance==null || KitApp.Instance.IsLoading || Time.timeScale==0) { if(line!=null)line.enabled=false;holding=false;return; }
            if(Input.GetMouseButtonDown(0))
                holding=EventSystem.current==null || !(Input.touchCount>0?EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId):EventSystem.current.IsPointerOverGameObject());
            if(!holding)return;
            var plane=new Plane(Vector3.up,new Vector3(0,Height,0));
            bool valid=false;
            var ray=camera.ScreenPointToRay(Input.mousePosition);
            if(plane.Raycast(ray,out var distance))
            {
                var direction=ray.GetPoint(distance)-new Vector3(0,Height,-8.5f);direction.y=0;
                valid=direction.z>0 && direction.sqrMagnitude>.001f;
                if(valid)
                {
                    float angle=Mathf.Clamp(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-80,80);
                    aim=Quaternion.Euler(0,angle,0)*Vector3.forward;
                    cannon.transform.rotation=Quaternion.Euler(0,angle,0);ShowTrajectory();
                }
            }
            line.enabled=valid;
            if(Input.GetMouseButtonUp(0)) { holding=false;line.enabled=false;if(valid)Fire(aim); }
        }
        public bool Fire(Vector3 direction)
        {
            if(!playing || IsUsingBooster || BallsRemaining<=0 || KitApp.Instance.IsLoading || Time.timeScale==0)return false;
            direction.y=0;if(direction.z<=0 || direction.sqrMagnitude<.001f)return false;direction.Normalize();
            var visual=Instantiate(presentation.ball,rig.transform);PrepareVisual(visual,false);
            var position=new Vector3(0,Height,-8.5f)+direction*.9f;
            visual.transform.position=position;
            shots.Add(new Shot {visual=visual,position=position,direction=direction});
            BallsRemaining--;PushCounts();
            KitHaptics.Pulse();
            if(presentation.shotSound!=null)audioSource.PlayOneShot(presentation.shotSound);
            return true;
        }
        public bool CanUseBooster(BoosterKind kind)
        {
            var app=KitApp.Instance;
            return playing && !IsUsingBooster && app!=null && !app.IsLoading && app.Session.State==SessionState.Playing && Time.timeScale>0 && blocks.Count>0
                && BoosterCatalog.Find(kind)!=null && (kind!=BoosterKind.ExtraBall || BallsRemaining<=int.MaxValue-BoosterCatalog.ExtraBallAmount);
        }
        public bool TryUseBooster(BoosterKind kind)
        {
            if(!CanUseBooster(kind) || !KitApp.Instance.TryConsumeBooster(kind))return false;
            IsUsingBooster=true;holding=false;line.enabled=false;
            var center=new Vector3(0,Height,-8.5f);
            Block[] victims=Array.Empty<Block>();
            if(kind!=BoosterKind.ExtraBall)
            {
                Block target;
                if(kind==BoosterKind.Bomb)target=blocks.OrderByDescending(b=>blocks.Count(n=>Mathf.Abs(n.root.position.x-b.root.position.x)<=1.01f && Mathf.Abs(n.root.position.z-b.root.position.z)<=1.01f)).First();
                else if(kind==BoosterKind.Laser)target=blocks.OrderByDescending(b=>blocks.Count(n=>Mathf.Abs(n.root.position.x-b.root.position.x)<.1f)).First();
                else target=blocks.OrderByDescending(b=>b.hp).First();
                center=target.root.position;center.y=Height;
                victims=blocks.Where(b=>kind==BoosterKind.Missile?b==target:kind==BoosterKind.Laser?Mathf.Abs(b.root.position.x-center.x)<.1f:Mathf.Abs(b.root.position.x-center.x)<=1.01f && Mathf.Abs(b.root.position.z-center.z)<=1.01f).ToArray();
            }
            StartCoroutine(UseBooster(kind,center,victims));PushCounts();return true;
        }
        private IEnumerator UseBooster(BoosterKind kind,Vector3 center,Block[] victims)
        {
            var effect=new GameObject("Booster_"+kind).AddComponent<BoosterEffect>();
            effect.transform.SetParent(rig.transform,false);
            if(presentation.shotSound!=null)audioSource.PlayOneShot(presentation.shotSound,.7f);
            yield return effect.Play(presentation,kind,new Vector3(0,Height,-8.5f),center,()=>
            {
                KitHaptics.Pulse();
                if(kind==BoosterKind.ExtraBall)BallsRemaining+=BoosterCatalog.ExtraBallAmount;
                else foreach(var victim in victims)
                    if(victim.root!=null)RemoveBlock(victim);
                Physics.SyncTransforms();PushCounts();
            });
            Destroy(effect.gameObject);IsUsingBooster=false;
            if(blocks.Count==0)Resolve(true);
            PushCounts();
        }
        private void ShowTrajectory()
        {
            var points=new List<Vector3>();var pos=new Vector3(0,Height,-8.5f)+aim*.9f;var direction=aim;float length=30;
            points.Add(pos);
            for(int i=0;i<4;i++)
            {
                if(Physics.SphereCast(pos,Radius,direction,out var hit,length,1<<SolidLayer,QueryTriggerInteraction.Ignore))
                {pos+=direction*hit.distance;points.Add(pos);length-=hit.distance;direction=Reflect(direction,hit.normal);pos+=direction*.001f;}
                else {points.Add(pos+direction*length);break;}
            }
            line.positionCount=points.Count;line.SetPositions(points.ToArray());
        }
        private static Vector3 Reflect(Vector3 direction,Vector3 normal)
        {
            var d=Vector3.Reflect(direction,normal);d.y=0;d.Normalize();
            float minimum=Mathf.Sin(5*Mathf.Deg2Rad);
            if(Mathf.Abs(d.x)<minimum)d.x=(d.x<0?-1:1)*minimum;
            if(Mathf.Abs(d.z)<minimum)d.z=(d.z<0?-1:1)*minimum;
            return d.normalized;
        }
        private void FixedUpdate()
        {
            if(!playing || IsUsingBooster)return;
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];shot.age+=Time.fixedDeltaTime;float remaining=Speed*Time.fixedDeltaTime;
                for(int bounce=0;bounce<4 && remaining>.00001f;bounce++)
                {
                    if(Physics.SphereCast(shot.position,Radius,shot.direction,out var hit,remaining,1<<SolidLayer,QueryTriggerInteraction.Ignore))
                    {
                        shot.position+=shot.direction*hit.distance;remaining-=hit.distance;
                        shot.direction=Reflect(shot.direction,hit.normal);shot.position+=shot.direction*.001f;
                        for(int b=blocks.Count-1;b>=0;b--)if(hit.collider.transform.IsChildOf(blocks[b].root)) {HitBlock(b);break;}
                    }
                    else {shot.position+=shot.direction*remaining;break;}
                }
                shot.visual.transform.position=shot.position;
                if(shot.position.z<-10 || shot.age>20) {Destroy(shot.visual);shots.RemoveAt(i);}
            }
            if(blocks.Count==0)Resolve(true);else if(BallsRemaining==0 && shots.Count==0)Resolve(false);
        }
        private void HitBlock(int index)
        {
            var block=blocks[index];block.hp--;
            if(block.hp==0)RemoveBlock(block);
            else {BuildBlock(block);StartCoroutine(Punch(block.root));}
            PushCounts();
        }
        private void RemoveBlock(Block block)
        {
            // Detach before deleting the gameplay root. The cosmetic mesh never participates in casts.
            var visual=block.visual;
            visual.transform.SetParent(rig.transform,true);
            visual.name="Breaking block";
            PrepareVisual(visual,false);
            visual.AddComponent<BlockBreakEffect>();
            block.root.gameObject.SetActive(false);Destroy(block.root.gameObject);blocks.Remove(block);
        }
        private IEnumerator Punch(Transform target)
        {
            float time=0;
            while(target!=null && time<.15f) {time+=Time.deltaTime;target.localScale=Vector3.one*(1+Mathf.Sin(time/.15f*Mathf.PI*4)*.12f*(1-time/.15f));yield return null;}
            if(target!=null)target.localScale=Vector3.one;
        }
        private void PushCounts()
        {
            if(cannon!=null)foreach(var text in cannon.GetComponentsInChildren<TMP_Text>(true))if(text.name=="TXT_BallCount")text.text=BallsRemaining.ToString();
            CountersChanged?.Invoke();
        }
        private void Resolve(bool won) {if(!playing)return;playing=false;holding=false;lost=!won;line.enabled=false;StartCoroutine(Report(won));}
        public override bool CanContinue=>lost && rig!=null && blocks.Count>0 && !IsUsingBooster;
        public override void Continue(int extraBalls)
        {
            if(!CanContinue)return;
            lost=false;BallsRemaining+=Mathf.Max(0,extraBalls);playing=true;holding=false;PushCounts();
        }
        private IEnumerator Report(bool won) {if(won)yield return new WaitForSeconds(.5f);completed?.Invoke(won);}
        public override void End()
        {
            StopAllCoroutines();playing=false;holding=false;lost=false;IsUsingBooster=false;completed=null;
            if(rig!=null)Destroy(rig);rig=null;shots.Clear();blocks.Clear();
            foreach(var material in materials)Destroy(material);materials.Clear();
        }
        private void OnDestroy()=>End();
    }
}
