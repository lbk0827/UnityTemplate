using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BK.Kit
{
    [Serializable]
    public sealed class GimmickSpec
    {
        public string type;
        public int col, row, length = 3;
        public bool vertical;
        public float travel = 2, speed = 1.2f, width = 2;
    }

    // Owns board objectives separately from ordinary blocks. All durability is physical geometry.
    public sealed class BounceGimmicks : MonoBehaviour
    {
        public sealed class Target
        {
            public GimmickSpec spec;
            public Transform root;
            public readonly List<GameObject> pieces = new List<GameObject>();
            public readonly List<Vector3> cells = new List<Vector3>();
            public BoxCollider collider;
            public Vector3 origin;
            public float phase, feed;
            public Transform furnace;
            public Renderer[] furnaceGlow;
            public int Remaining => pieces.Count;
        }

        private readonly List<Target> targets = new List<Target>();
        private readonly List<Material> materials = new List<Material>();
        private Material source;
        private MaterialPropertyBlock tubeProperties;
        private Action changed;
        public IReadOnlyList<Target> Targets => targets;
        public int ObjectivesRemaining => targets.Count(t => t.spec.type != "panel" && t.Remaining > 0);
        public IEnumerable<Vector3> ObjectivePositions => targets.Where(t => t.spec.type != "panel" && t.Remaining > 0)
            .Select(t => t.spec.type == "tube" ? t.pieces[t.pieces.Count - 1].transform.position : t.root.position);

        public static Vector3 Cell(int col, int row, int columns) => new Vector3(col - (columns - 1) * .5f, .5f, 7 - row);

        public void Build(GimmickSpec[] specs, int columns, Material material, Action onChanged)
        {
            source = material; changed = onChanged; tubeProperties = new MaterialPropertyBlock();
            if (specs == null) return;
            foreach (var spec in specs)
            {
                if (spec.type != "tube" && spec.type != "cupboard" && spec.type != "panel")
                    throw new ArgumentException("Unknown gimmick: " + spec.type);
                var root = new GameObject(spec.type + "_" + spec.col + "_" + spec.row).transform;
                root.SetParent(transform, false); root.position = Cell(spec.col, spec.row, columns);
                var target = new Target { spec = spec, root = root, origin = root.position };
                targets.Add(target);
                if (spec.type == "tube") BuildTube(target);
                else if (spec.type == "cupboard") BuildCupboard(target);
                else BuildPanel(target, columns);
            }
        }

        private Material Paint(Color color, bool metal = false)
        {
            var material = new Material(source) { color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal ? .65f : 0);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .45f);
            materials.Add(material); return material;
        }

        private GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 2;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Collider>().enabled = false;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
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
            fire.EnableKeyword("_EMISSION"); fire.SetColor("_EmissionColor", new Color(1, .24f, .015f));
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

        public void Tick(float deltaTime)
        {
            foreach (var t in targets)
            {
                if (t.spec.type != "panel") continue;
                t.phase += deltaTime * Mathf.Max(0, t.spec.speed);
                t.root.position = t.origin + Vector3.right * (Mathf.Sin(t.phase) * t.spec.travel);
            }
            Physics.SyncTransforms();
        }

        private void LateUpdate()
        {
            foreach (var t in targets)
            {
                if (t.spec.type == "tube")
                {
                    t.feed = Mathf.Max(0, t.feed - Time.deltaTime / .24f);
                    var axis = t.spec.vertical ? Vector3.back : Vector3.right;
                    foreach (var piece in t.pieces) piece.transform.GetChild(0).localPosition = axis * t.feed;
                    var properties = tubeProperties;
                    var heat = t.Remaining == 0 ? new Color(.16f, .045f, .025f) : Color.Lerp(new Color(1, .16f, .015f), new Color(1, .72f, .08f), t.feed);
                    properties.SetColor("_BaseColor", heat);
                    properties.SetColor("_EmissionColor", heat * (t.Remaining == 0 ? 0 : 1 + t.feed * 2));
                    foreach (var glow in t.furnaceGlow) glow.SetPropertyBlock(properties);
                }
            }
        }

        public bool Hit(Collider collider)
        {
            foreach (var t in targets)
                if (t.Remaining > 0 && t.spec.type == "tube" && collider.transform.IsChildOf(t.root))
                { RemovePiece(t); return true; }
            return false;
        }

        public static bool IsAdjacent(Vector3 cell, Vector3 other)
        {
            var delta = cell - other;
            return (Mathf.Abs(delta.x) < .05f && Mathf.Abs(Mathf.Abs(delta.z) - 1) < .05f)
                || (Mathf.Abs(delta.z) < .05f && Mathf.Abs(Mathf.Abs(delta.x) - 1) < .05f);
        }

        public void NotifyBlockHit(Vector3 cell)
        {
            foreach (var t in targets)
                if (t.spec.type == "cupboard" && t.Remaining > 0 && t.cells.Any(c => IsAdjacent(c, cell))) RemovePiece(t);
        }

        // A booster hits each objective once, even when several tube cells are in its area.
        public void HitArea(Func<Vector3, bool> contains)
        {
            foreach (var t in targets)
                if (t.spec.type != "panel" && t.Remaining > 0 &&
                    (t.spec.type == "tube" ? contains(t.furnace.position) || t.pieces.Any(p => contains(p.transform.position)) : t.cells.Any(contains))) RemovePiece(t);
        }

        private void RemovePiece(Target t)
        {
            var piece = t.pieces[t.pieces.Count - 1]; t.pieces.RemoveAt(t.pieces.Count - 1);
            foreach (var collider in piece.GetComponentsInChildren<Collider>()) collider.enabled = false;
            piece.transform.SetParent(transform, true);
            if (t.spec.type == "tube")
            {
                t.feed = 1;
                piece.transform.position = t.origin;
                piece.transform.GetChild(0).localPosition = Vector3.zero;
                StartCoroutine(Melt(piece, t.furnace.position));
            }
            else StartCoroutine(BreakPlate(piece));
            if (t.Remaining == 0 && t.collider != null) t.collider.enabled = false;
            Physics.SyncTransforms(); changed?.Invoke();
        }

        private IEnumerator Melt(GameObject piece, Vector3 furnace)
        {
            var scale = piece.transform.localScale;
            var start = piece.transform.position;
            var renderers = piece.GetComponentsInChildren<Renderer>();
            var property = new MaterialPropertyBlock();
            for (float time = 0; time < .3f; time += Time.deltaTime)
            {
                float progress = time / .3f;
                property.SetColor("_BaseColor", Color.Lerp(new Color(1, .46f, .07f), new Color(1, .13f, .02f), progress));
                foreach (var renderer in renderers) renderer.SetPropertyBlock(property);
                piece.transform.localScale = scale * Mathf.Max(.01f, 1 - progress);
                piece.transform.position = Vector3.Lerp(start, furnace, progress);
                yield return null;
            }
            Destroy(piece);
        }

        private IEnumerator BreakPlate(GameObject piece)
        {
            var material = piece.GetComponentInChildren<Renderer>().sharedMaterial;
            var fragments = new List<GameObject>();
            for (int i = 0; i < 5; i++)
                fragments.Add(Shape(transform, "Porcelain shard", PrimitiveType.Cube, piece.transform.position,
                    new Vector3(.13f, .045f, .10f), material));
            piece.SetActive(false); Destroy(piece);
            for (float time = 0; time < .4f; time += Time.deltaTime)
            {
                for (int i = 0; i < fragments.Count; i++)
                {
                    var direction = new Vector3(Mathf.Cos(i * 1.256f), 1.5f - time * 8, Mathf.Sin(i * 1.256f));
                    fragments[i].transform.position += direction * Time.deltaTime;
                    fragments[i].transform.Rotate(new Vector3(120, 70, 30) * Time.deltaTime);
                    fragments[i].transform.localScale *= Mathf.Exp(-Time.deltaTime * 3);
                }
                yield return null;
            }
            foreach (var fragment in fragments) Destroy(fragment);
        }

        // Recover balls swept into a moving panel between physics steps. Static geometry still uses SphereCast.
        public void SeparateFromPanels(ref Vector3 position, ref Vector3 direction, float radius)
        {
            foreach (var t in targets)
            {
                if (t.spec.type != "panel") continue;
                var bounds = t.collider.bounds; bounds.Expand(radius * 2);
                if (!bounds.Contains(position)) continue;
                float left = position.x - bounds.min.x, right = bounds.max.x - position.x;
                float back = position.z - bounds.min.z, front = bounds.max.z - position.z;
                float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(back, front));
                Vector3 normal = nearest == left ? Vector3.left : nearest == right ? Vector3.right : nearest == back ? Vector3.back : Vector3.forward;
                position += normal * (nearest + .002f);
                if (Vector3.Dot(direction, normal) < 0) direction = Vector3.Reflect(direction, normal).normalized;
            }
        }

        public bool IsMovingPanel(Collider collider) => targets.Any(t => t.spec.type == "panel" && t.collider == collider);

        private void OnDestroy()
        {
            foreach (var material in materials) Destroy(material);
        }
    }
}
