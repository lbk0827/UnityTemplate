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
        [Serializable] public sealed class Level { public int balls; public string[] rows; public GimmickSpec[] gimmicks; }
        [Serializable] public sealed class Levels { public Level[] levels; }
        [Serializable] private sealed class Block { public Transform root; public GameObject visual; public int hp, skin; }
        private sealed class Shot { public int id; public GameObject visual; public Vector3 position, direction; public float age; }
        public GamePresentation presentation;
        [Range(45,90),Tooltip("Camera tilt from the ground plane. 90 = straight top-down.")]
        public float cameraPitch = 60;
        public int BallsRemaining { get; private set; }
        public int BlocksRemaining => blocks.Count + (gimmicks == null ? 0 : gimmicks.ObjectivesRemaining);
        private BounceGimmicks gimmicks;
        private int nextShotId;
        public int ActiveBalls => shots.Count;
        public bool IsUsingBooster { get; private set; }
        public event Action CountersChanged;
        private readonly List<Block> blocks = new List<Block>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Material> materials = new List<Material>();
        private GameObject rig, cannon;
        private Camera camera;
        private LineRenderer line;
        private Texture2D trajectoryDots;
        private AudioSource audioSource;
        private BounceEffects effects;
        private float nextImpactSound;
        private Action<bool> completed;
        private bool playing, holding;
        private int pointerId = -1;
        private readonly Vector3[] trajectoryPoints = new Vector3[3];
        private int columns;
        private Vector3 aim = Vector3.forward;
        private const int SolidLayer = 10;
        // Blocks are unit cubes centred on their root; Height lifts them so their base rests on the grass top (y=0).
        private const float Height = .5f, Speed = 18f;
        // Authored ball mesh radius at scale 1; visual size matches the gameplay radius.
        private const float BallPrefabRadius = .5f;
        // Default for the static preview; matches IngameConfig.ballRadius's default.
        private const float DefaultRadius = .25f;
        private float Radius => presentation.ingameConfig.ballRadius;
        // The lagoon starts here. Preview rays may leave the launcher, but never enter water.
        public const float DeadZoneZ = -7f;
        private static readonly Vector3 LaunchOrigin = new Vector3(0, Height, -8.5f);

        public override void Begin(int level, Action<bool> onCompleted)
        {
            End();
            nextImpactSound=0;nextShotId=0;
            completed = onCompleted;
            var library = JsonUtility.FromJson<Levels>(presentation.levels.text);
            var data = library.levels[(Mathf.Max(1, level)-1)%library.levels.Length];
            columns = data.rows[0].Length;
            BallsRemaining = data.balls;
            rig = new GameObject("Bounce_Rig");
            camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max((columns*.5f+2)/Mathf.Max(.3f,camera.aspect),11.5f);
            // Orthographic keeps every row the same size; the tilt only reveals block and wall sides.
            var view=Quaternion.Euler(cameraPitch,0,0);
            camera.transform.SetPositionAndRotation(new Vector3(0,0,.3f)-view*Vector3.forward*40,view);
            camera.backgroundColor = new Color(.075f,.34f,.43f);
            audioSource = rig.AddComponent<AudioSource>();
            rig.AddComponent<KitAudioChannel>().Initialize(audioSource,KitAudioChannel.Category.Effects);
            BuildArena();
            for (int row=0;row<data.rows.Length;row++)
                for (int col=0;col<columns;col++)
                {
                    // Level digits select box colours; durability is independent of appearance.
                    int colour=data.rows[row][col]-'0';
                    if (colour<1 || colour>9) continue;
                    var root=new GameObject("Block_"+col+"_"+row).transform;
                    root.SetParent(rig.transform,false);
                    root.position=new Vector3(col-(columns-1)*.5f,Height,7-row);
                    var block=new Block {root=root,hp=1,skin=Mathf.Clamp(colour-1,0,presentation.blocks.Length-1)};
                    blocks.Add(block); BuildBlock(block);
                }
            gimmicks=rig.AddComponent<BounceGimmicks>();
            gimmicks.Build(data.gimmicks,columns,presentation.ingameConfig.gimmickPrefabs,PushCounts,BlastBlocks);
            cannon=Instantiate(presentation.cannon,rig.transform);
            cannon.name="Bounce_Cannon";
            cannon.transform.position=new Vector3(0,0,-8.5f);
            cannon.transform.localScale=Vector3.one*1.8f;
            PrepareVisual(cannon,false);
            RestOnGround(cannon);
            effects=rig.AddComponent<BounceEffects>();effects.Initialize(presentation,cannon);
            line=new GameObject("Aim trajectory",typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(rig.transform,false);
            var guideMaterial=new Material(presentation.trajectoryMaterial);
            materials.Add(guideMaterial);
            trajectoryDots=new Texture2D(64,16,TextureFormat.RGBA32,false) {name="Aim beads",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            var pixels=new Color[64*16];
            for(int y=0;y<16;y++)for(int x=0;x<64;x++)
                pixels[y*64+x]=new Color(.9f,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,7,Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,8)))));
            trajectoryDots.SetPixels(pixels);trajectoryDots.Apply(false,true);
            guideMaterial.SetTexture("_BaseMap",trajectoryDots);
            guideMaterial.SetFloat("_AlphaClip",1);guideMaterial.SetFloat("_Cutoff",.4f);guideMaterial.EnableKeyword("_ALPHATEST_ON");
            line.sharedMaterial=guideMaterial;line.textureMode=LineTextureMode.Tile;line.textureScale=new Vector2(2.5f,1);
            line.startWidth=line.endWidth=.13f;
            line.numCapVertices=4;line.numCornerVertices=4;
            line.startColor=new Color(.87f,1,1,1);line.endColor=new Color(.6f,.95f,1,1);
            line.enabled=false;
            playing=true;
            Physics.SyncTransforms();
            PushCounts();
        }

        private void BuildArena()
        {
            float half=columns*.5f;
            Color wall=new Color(.96f,.86f,.65f);
            Box("Wall left",new Vector3(-half-.5f,.6f,1.25f),new Vector3(1,1.2f,14.5f),0,wall,true);
            Box("Wall right",new Vector3(half+.5f,.6f,1.25f),new Vector3(1,1.2f,14.5f),0,wall,true);
            Box("Wall top",new Vector3(0,.6f,8),new Vector3(columns+2,1.2f,1),0,wall,true);
            float delta=1.5f*Mathf.Sqrt(.5f);
            Box("Bumper left",new Vector3(-half+delta,.6f,-6-delta),new Vector3(3,1.2f,.6f),45,wall,true);
            Box("Bumper right",new Vector3(half-delta,.6f,-6-delta),new Vector3(3,1.2f,.6f),135,wall,true);
            rig.AddComponent<LagoonArena>().Build(columns,presentation.arenaMaterial);
        }
        private void Box(string name,Vector3 position,Vector3 scale,float yaw,Color color,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            go.transform.SetParent(rig.transform,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=scale;
            go.layer=solid?SolidLayer:2;
            if(!solid) {go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());}
            var material=new Material(presentation.arenaMaterial);material.color=color;materials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        private void BuildBlock(Block block)
        {
            if(block.visual!=null) { block.visual.SetActive(false);Destroy(block.visual); }
            block.visual=Instantiate(presentation.blocks[block.skin],block.root);
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
        // Lifts a visual so its lowest mesh sits on the grass top (y=0). Text meshes are skipped: they are empty until TMP builds them.
        private static void RestOnGround(GameObject go)
        {
            float bottom=float.MaxValue;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
                if((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.GetComponent<TMP_Text>()==null)bottom=Mathf.Min(bottom,renderer.bounds.min.y);
            if(bottom!=float.MaxValue)go.transform.position+=Vector3.up*-bottom;
        }
        private void Update()
        {
            if(!playing || IsUsingBooster || BallsRemaining<=0 || KitApp.Instance==null || KitApp.Instance.IsLoading || Time.timeScale==0) { CancelAim();return; }
            Vector2 screen=Input.mousePosition;
            bool began=Input.GetMouseButtonDown(0),released=Input.GetMouseButtonUp(0),canceled=false;
            int id=-1;
            if(Input.touchCount>0)
            {
                bool found=false;
                for(int i=0;i<Input.touchCount;i++)
                {
                    var touch=Input.GetTouch(i);
                    if(holding ? touch.fingerId!=pointerId : touch.phase!=TouchPhase.Began)continue;
                    found=true;id=touch.fingerId;screen=touch.position;
                    began=touch.phase==TouchPhase.Began;released=touch.phase==TouchPhase.Ended;
                    canceled=touch.phase==TouchPhase.Canceled;break;
                }
                if(!found) {if(holding)CancelAim();return;}
            }
            else if(holding && pointerId>=0) {CancelAim();return;}
            if(canceled) {CancelAim();return;}
            if(began)
            {
                holding=EventSystem.current==null || !EventSystem.current.IsPointerOverGameObject(id);
                pointerId=id;
            }
            if(!holding)return;
            var plane=new Plane(Vector3.up,new Vector3(0,Height,0));
            bool valid=false;
            var ray=camera.ScreenPointToRay(screen);
            if(plane.Raycast(ray,out var distance))
            {
                var direction=ray.GetPoint(distance)-LaunchOrigin;direction.y=0;
                valid=TryGetAim(direction,out var nextAim) && camera.pixelRect.Contains(screen);
                if(valid)
                {
                    aim=nextAim;
                    cannon.transform.rotation=Quaternion.LookRotation(aim);ShowTrajectory();
                }
            }
            line.enabled=valid;
            if(released)
            {
                bool overUI=EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject(pointerId);
                CancelAim();if(valid && !overUI)Fire(aim);
            }
        }
        private void CancelAim() {holding=false;pointerId=-1;if(line!=null)line.enabled=false;}
        private void OnApplicationFocus(bool focused) {if(!focused)CancelAim();}
        private void OnApplicationPause(bool paused) {if(paused)CancelAim();}
        public static bool TryGetAim(Vector3 offset,out Vector3 direction)
        {
            direction=Vector3.forward;offset.y=0;
            // A small dead band prevents the barrel flipping when the finger crosses its pivot.
            if(float.IsNaN(offset.x) || float.IsNaN(offset.z) || float.IsInfinity(offset.x) || float.IsInfinity(offset.z)
                || offset.z<=.15f || offset.sqrMagnitude<.36f)return false;
            float angle=Mathf.Clamp(Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg,-75,75);
            direction=Quaternion.Euler(0,angle,0)*Vector3.forward;return true;
        }
        public bool Fire(Vector3 direction)
        {
            if(!playing || IsUsingBooster || BallsRemaining<=0 || KitApp.Instance==null || KitApp.Instance.IsLoading || Time.timeScale==0)return false;
            if(!TryGetAim(direction,out direction))return false;
            var visual=Instantiate(presentation.ball,rig.transform);PrepareVisual(visual,false);
            visual.transform.localScale=Vector3.one*(Radius/BallPrefabRadius);
            var position=LaunchOrigin+direction*.9f;
            visual.transform.position=position;
            effects.Launch(visual);
            shots.Add(new Shot {id=++nextShotId,visual=visual,position=position,direction=direction});
            BallsRemaining--;PushCounts();
            KitHaptics.Pulse();
            if(presentation.shotSound!=null)audioSource.PlayOneShot(presentation.shotSound);
            return true;
        }
        public bool CanUseBooster(BoosterKind kind)
        {
            var app=KitApp.Instance;
            return playing && !IsUsingBooster && app!=null && !app.IsLoading && app.Session.State==SessionState.Playing && Time.timeScale>0 && BlocksRemaining>0
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
                Block target=null;
                if(blocks.Count==0) center=gimmicks.ObjectivePositions.First();
                else
                {
                if(kind==BoosterKind.Bomb)target=blocks.OrderByDescending(b=>blocks.Count(n=>Mathf.Abs(n.root.position.x-b.root.position.x)<=1.01f && Mathf.Abs(n.root.position.z-b.root.position.z)<=1.01f)).First();
                else if(kind==BoosterKind.Laser)target=blocks.OrderByDescending(b=>blocks.Count(n=>Mathf.Abs(n.root.position.x-b.root.position.x)<.1f)).First();
                else target=blocks.OrderByDescending(b=>b.hp).First();
                center=target.root.position;center.y=Height;
                }
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
                else
                {
                    foreach(var victim in victims)
                        if(victim.root!=null && blocks.Contains(victim))
                        { gimmicks.NotifyBlockHit(victim.root.position); RemoveBlock(victim); }
                    gimmicks.HitArea(p=>kind==BoosterKind.Missile ? (p-center).sqrMagnitude<.1f :
                        kind==BoosterKind.Laser ? Mathf.Abs(p.x-center.x)<.1f :
                        Mathf.Abs(p.x-center.x)<=1.01f && Mathf.Abs(p.z-center.z)<=1.01f);
                }
                Physics.SyncTransforms();PushCounts();
            });
            Destroy(effect.gameObject);IsUsingBooster=false;
            if(BlocksRemaining==0)Resolve(true);
            PushCounts();
        }
        private void ShowTrajectory()
        {
            int count=TraceTrajectory(aim,trajectoryPoints,Radius);
            line.positionCount=count;
            for(int i=0;i<count;i++)line.SetPosition(i,trajectoryPoints[i]);
        }
        // At most two segments: incoming ray and the first reflection. Uses the ball's cast radius.
        public static int TraceTrajectory(Vector3 direction,Vector3[] points,float radius=DefaultRadius)
        {
            if(points==null || points.Length<3)throw new ArgumentException("Three trajectory points are required.",nameof(points));
            if(!TryGetAim(direction,out direction))return 0;
            var pos=LaunchOrigin+direction*.9f;
            // Cast through the launch area as well, so an unseen bumper cannot change the prediction.
            float entry=(DeadZoneZ-pos.z)/direction.z;
            if(entry>0)
            {
                if(Physics.SphereCast(pos,radius,direction,out _,entry,1<<SolidLayer,QueryTriggerInteraction.Ignore))return 0;
                pos+=direction*entry;
            }
            int count=1;points[0]=pos;float length=30;
            for(int segment=0;segment<2;segment++)
            {
                float limit=direction.z<0?Mathf.Min(length,Mathf.Max(0,(DeadZoneZ-pos.z)/direction.z)):length;
                if(limit<=.0001f)break;
                if(!Physics.SphereCast(pos,radius,direction,out var hit,limit,1<<SolidLayer,QueryTriggerInteraction.Ignore))
                {points[count++]=pos+direction*limit;break;}
                pos+=direction*hit.distance;points[count++]=pos;length-=hit.distance;
                var board=hit.collider.GetComponentInParent<BounceGimmicks>();
                if(board!=null && board.IsMovingPanel(hit.collider))break;
                direction=Reflect(direction,hit.normal);pos+=direction*.001f;
            }
            return count;
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
            gimmicks.Tick(Time.fixedDeltaTime);
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];shot.age+=Time.fixedDeltaTime;float remaining=Speed*Time.fixedDeltaTime;
                gimmicks.SeparateFromPanels(ref shot.position,ref shot.direction,Radius);
                bool drained=false;
                for(int bounce=0;bounce<4 && remaining>.00001f;bounce++)
                {
                    float toWater=shot.direction.z<0?Mathf.Max(0,(DeadZoneZ-shot.position.z)/shot.direction.z):float.PositiveInfinity;
                    float travel=Mathf.Min(remaining,toWater);
                    if(Physics.SphereCast(shot.position,Radius,shot.direction,out var hit,travel,1<<SolidLayer,QueryTriggerInteraction.Ignore))
                    {
                        shot.position+=shot.direction*hit.distance;remaining-=hit.distance;
                        effects.Hit(hit.point);
                        var incoming=shot.direction;
                        shot.direction=Reflect(shot.direction,hit.normal);shot.position+=shot.direction*.001f;
                        if(gimmicks.Hit(hit.collider,shot.id,incoming,hit.normal))PlayImpactSound(presentation.blockHitSound);
                        for(int b=blocks.Count-1;b>=0;b--)if(hit.collider.transform.IsChildOf(blocks[b].root)) {HitBlock(b,incoming);break;}
                    }
                    else {shot.position+=shot.direction*travel;drained=toWater<=remaining;break;}
                }
                shot.visual.transform.position=shot.position;
                if(drained || shot.age>20) {gimmicks.ShotEnded(shot.id);shot.visual.GetComponent<BallFlightEffect>()?.ReleaseTrail();Destroy(shot.visual);shots.RemoveAt(i);}
            }
            if(BlocksRemaining==0)Resolve(true);else if(BallsRemaining==0 && shots.Count==0)Resolve(false);
        }
        private void BlastBlocks(Vector3 center)
        {
            var victims=blocks.Where(b=>Mathf.Abs(b.root.position.x-center.x)<=1.01f && Mathf.Abs(b.root.position.z-center.z)<=1.01f).ToArray();
            foreach(var victim in victims)
            {
                int index=blocks.IndexOf(victim);
                if(index>=0)HitBlock(index,(victim.root.position-center).normalized);
            }
        }
        private void HitBlock(int index,Vector3 incoming)
        {
            var block=blocks[index];gimmicks.NotifyBlockHit(block.root.position);block.hp--;
            if(block.hp==0)RemoveBlock(block,incoming);
            else {PlayImpactSound(presentation.blockHitSound);BuildBlock(block);StartCoroutine(Punch(block.root));}
            PushCounts();
        }
        private void RemoveBlock(Block block,Vector3 push=default)
        {
            PlayImpactSound(presentation.blockBreakSound);
            effects.Break(block.visual,block.skin,push);
            block.root.gameObject.SetActive(false);Destroy(block.root.gameObject);blocks.Remove(block);
        }
        private void PlayImpactSound(AudioClip clip)
        {
            if(clip==null || Time.time<nextImpactSound)return;
            nextImpactSound=Time.time+.04f;audioSource.PlayOneShot(clip,.65f);
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
        private void Resolve(bool won) {if(!playing)return;playing=false;holding=false;line.enabled=false;StartCoroutine(Report(won));}
        // Board intact (blocks left, rig alive): a bought continue can resume whether the loss came from the physics or from the hub.
        public override bool CanContinue=>rig!=null && BlocksRemaining>0 && !IsUsingBooster;
        public override void Continue(int extraBalls)
        {
            if(!CanContinue)return;
            BallsRemaining+=Mathf.Max(0,extraBalls);playing=true;holding=false;PushCounts();
        }
        private IEnumerator Report(bool won) {if(won)yield return new WaitForSeconds(.5f);completed?.Invoke(won);}
        public override void End()
        {
            StopAllCoroutines();playing=false;holding=false;IsUsingBooster=false;completed=null;
            if(rig!=null) {rig.SetActive(false);Destroy(rig);}rig=null;gimmicks=null;shots.Clear();blocks.Clear();
            foreach(var material in materials)Destroy(material);materials.Clear();
            if(trajectoryDots!=null)Destroy(trajectoryDots);trajectoryDots=null;
        }
        private void OnDestroy()=>End();
    }
}
