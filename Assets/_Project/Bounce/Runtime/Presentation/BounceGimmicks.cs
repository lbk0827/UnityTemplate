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
        public string link = "A";
        public int weakSide; // 0 launcher-facing, 1 right, 2 away from launcher, 3 left.
    }

    public sealed class BounceGimmicks : MonoBehaviour
    {
        public sealed class Target
        {
            public GimmickSpec spec;
            public GimmickView view;
            public Transform root;
            public readonly List<GameObject> pieces = new List<GameObject>();
            public readonly List<Vector3> cells = new List<Vector3>();
            public readonly HashSet<int> armedShots = new HashSet<int>();
            public readonly List<LineRenderer> links = new List<LineRenderer>();
            public BoxCollider collider;
            public Vector3 origin;
            public float phase, feed;
            public Transform furnace;
            public Renderer[] furnaceGlow;
            public bool completed, activated;
            public int Remaining => pieces.Count;
            public bool IsObjective => spec.type != "panel" && spec.type != "switch" && spec.type != "door";
        }

        private readonly List<Target> targets = new List<Target>();
        private readonly Queue<Target> explosions = new Queue<Target>();
        private bool processingExplosions;
        private GimmickPrefabCatalog catalog;
        private MaterialPropertyBlock tubeProperties;
        private Action changed;
        private Action<Vector3> blastBlocks;
        public IReadOnlyList<Target> Targets => targets;
        public int ObjectivesRemaining => targets.Count(t => t.IsObjective && t.Remaining > 0);
        public IEnumerable<Vector3> ObjectivePositions => targets.Where(t => t.IsObjective && t.Remaining > 0)
            .Select(t => t.spec.type == "tube" ? t.pieces[t.pieces.Count - 1].transform.position : t.root.position);
        public static Vector3 Cell(int col, int row, int columns, int gridRows = 13) => new Vector3(col - (columns - 1) * .5f, .5f, gridRows - 6 - row);
        public static Vector3 WeakNormal(int side) => side == 1 ? Vector3.right : side == 2 ? Vector3.forward : side == 3 ? Vector3.left : Vector3.back;

        public void Build(GimmickSpec[] specs, int columns, GimmickPrefabCatalog prefabs, Action onChanged, Action<Vector3> onBlast = null, int gridRows = 13)
        {
            changed = onChanged; blastBlocks = onBlast; catalog = prefabs; tubeProperties = new MaterialPropertyBlock();
            if (specs == null || specs.Length == 0) return;
            if (catalog == null) throw new InvalidOperationException("Assign GimmickPrefabs in IngameConfig. Use BK Kit/Gimmicks/Create Missing Prefabs.");
            foreach (var spec in specs)
            {
                var prefab = catalog.Find(spec.type);
                if (prefab == null) throw new InvalidOperationException("Missing gimmick prefab: " + spec.type);
                var view = Instantiate(prefab, transform);
                view.name = spec.type + "_" + spec.col + "_" + spec.row;
                view.transform.position = Cell(spec.col, spec.row, columns, gridRows);
                var t = new Target { spec = spec, view = view, root = view.transform, origin = view.transform.position, collider = view.solid };
                targets.Add(t);
                t.pieces.AddRange(view.pieces ?? Array.Empty<GameObject>());
                foreach (var cell in BounceLevelLayout.Cells(spec)) t.cells.Add(Cell(cell.x, cell.y, columns, gridRows));
                if (spec.type == "tube")
                {
                    view.transform.localRotation = Quaternion.Euler(0, spec.vertical ? 90 : 0, 0);
                    t.furnace = view.furnace; t.furnaceGlow = view.furnaceGlow;
                    int length = Mathf.Max(1, spec.length);
                    while (t.pieces.Count > length)
                    {
                        var last = t.pieces[t.pieces.Count - 1]; t.pieces.RemoveAt(t.pieces.Count - 1);
                        last.SetActive(false); Destroy(last);
                    }
                    while (t.pieces.Count < length)
                    {
                        var piece = Instantiate(catalog.tubeSegment, t.root);
                        piece.transform.localPosition = Vector3.right * t.pieces.Count;
                        piece.transform.localRotation = Quaternion.identity; t.pieces.Add(piece);
                    }
                }
                else if (spec.type == "panel")
                {
                    t.root = view.movingBody;
                    float width = Mathf.Clamp(spec.width, .5f, columns - .5f);
                    float room = Mathf.Max(0, columns * .5f - Mathf.Abs(t.origin.x) - width * .5f - .1f);
                    // Authored data is not mutated while playing.
                    t.root.localScale = new Vector3(width / 2, 1, 1);
                    float travel = Mathf.Clamp(spec.travel, 0, room);
                    view.rail.localScale = new Vector3(travel * 2 + width, .09f, .16f);
                    for (int i = 0; i < view.railStops.Length; i++) view.railStops[i].localPosition = new Vector3((i == 0 ? -1 : 1) * (travel + width * .5f), -.36f, 0);
                }
                else if (spec.type == "armor") view.transform.localRotation = Quaternion.Euler(0, -90 * spec.weakSide, 0);
            }
            foreach (var button in targets.Where(t => t.spec.type == "switch"))
                foreach (var door in targets.Where(t => t.spec.type == "door" && t.spec.link == button.spec.link))
                {
                    var line = Instantiate(catalog.link, transform).GetComponent<LineRenderer>();
                    line.SetPosition(0, button.origin + Vector3.up * .57f); line.SetPosition(1, door.origin + Vector3.up * .57f);
                    line.startColor = line.endColor = new Color(.2f, .55f, .6f); button.links.Add(line);
                }
            Physics.SyncTransforms();
        }

        public void Tick(float deltaTime)
        {
            foreach (var t in targets)
            {
                if (t.spec.type != "panel") continue;
                t.phase += deltaTime * Mathf.Max(0, t.spec.speed);
                // Rail length encodes the validated/clamped travel distance.
                float travel = Mathf.Max(0, (t.view.rail.localScale.x - t.collider.bounds.size.x) * .5f);
                t.root.position = t.origin + Vector3.right * (Mathf.Sin(t.phase) * travel);
            }
            Physics.SyncTransforms();
        }

        private void LateUpdate()
        {
            foreach (var t in targets)
            {
                if (t.spec.type != "tube") continue;
                t.feed = Mathf.Max(0, t.feed - Time.deltaTime / .24f);
                foreach (var piece in t.pieces) piece.transform.GetChild(0).localPosition = Vector3.right * t.feed;
                var heat = t.Remaining == 0 ? new Color(.16f, .045f, .025f) : Color.Lerp(new Color(1, .16f, .015f), new Color(1, .72f, .08f), t.feed);
                tubeProperties.SetColor("_BaseColor", heat);
                tubeProperties.SetColor("_EmissionColor", heat * (t.Remaining == 0 ? 0 : 1 + t.feed * 2));
                foreach (var glow in t.furnaceGlow) glow.SetPropertyBlock(tubeProperties);
            }
        }

        public bool Hit(Collider collider, int shotId = 0, Vector3 incoming = default, Vector3 normal = default)
        {
            var t = targets.FirstOrDefault(x => !x.completed && collider.transform.IsChildOf(x.root));
            if (t == null) return false;
            switch (t.spec.type)
            {
                case "tube": RemovePiece(t); return true;
                case "mine": Detonate(t); return true;
                case "switch": Activate(t); return true;
                case "combo":
                    if (shotId <= 0) return false;
                    if (!t.armedShots.Add(shotId)) Clear(t);
                    else t.view.ShowActive(true);
                    return true;
                case "armor":
                    var weak = WeakNormal(t.spec.weakSide);
                    if (Vector3.Dot(normal, weak) < .7f || Vector3.Dot(incoming, weak) >= 0) return false;
                    Clear(t); return true;
                default: return false;
            }
        }

        public void ShotEnded(int shotId)
        {
            foreach (var t in targets)
                if (t.spec.type == "combo" && !t.completed && t.armedShots.Remove(shotId) && t.armedShots.Count == 0) t.view.ShowActive(false);
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

        private bool InArea(Target t, Func<Vector3, bool> contains) => t.spec.type == "tube"
            ? t.Remaining > 0 && (contains(t.furnace.position) || t.pieces.Any(p => contains(p.transform.position)))
            : t.cells.Any(contains);

        public void HitArea(Func<Vector3, bool> contains)
        {
            // Snapshot targets, not individual cells: a long pipe takes one hit per explosion.
            foreach (var t in targets.Where(t => !t.completed && InArea(t, contains)).ToArray()) PowerHit(t);
        }

        private void PowerHit(Target t)
        {
            if (t.completed) return;
            switch (t.spec.type)
            {
                case "tube": case "cupboard": if (t.Remaining > 0) RemovePiece(t); break;
                case "mine": Detonate(t); break;
                case "switch": Activate(t); break;
                case "combo": case "armor": Clear(t); break;
            }
        }

        private void Detonate(Target mine)
        {
            if (mine.completed) return;
            Clear(mine); explosions.Enqueue(mine);
            if (processingExplosions) return;
            processingExplosions = true;
            try
            {
                while (explosions.Count > 0)
                {
                    var source = explosions.Dequeue();
                    var effect = Instantiate(catalog.blast, source.origin + Vector3.up * .1f, Quaternion.identity, transform);
                    StartCoroutine(Burst(effect));
                    bool Contains(Vector3 p) => Mathf.Abs(p.x - source.origin.x) <= 1.01f && Mathf.Abs(p.z - source.origin.z) <= 1.01f;
                    HitArea(Contains); blastBlocks?.Invoke(source.origin);
                }
            }
            finally { processingExplosions = false; }
            changed?.Invoke();
        }

        private void Activate(Target button)
        {
            if (button.activated) return;
            button.activated = true; button.view.ShowActive(true);
            foreach (var line in button.links) line.startColor = line.endColor = new Color(.3f, 1, .8f);
            foreach (var door in targets.Where(t => t.spec.type == "door" && t.spec.link == button.spec.link && !t.activated))
            {
                door.activated = true; door.collider.enabled = false;
                door.view.activeVisual.SetActive(true); StartCoroutine(RaiseGate(door.view.inactiveVisual));
            }
            Physics.SyncTransforms(); changed?.Invoke();
        }

        private void Clear(Target t)
        {
            if (t.completed) return;
            t.completed = true; t.armedShots.Clear();
            foreach (var collider in t.view.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var piece in t.pieces) { piece.transform.SetParent(transform, true); StartCoroutine(Shrink(piece)); }
            t.pieces.Clear(); Physics.SyncTransforms(); changed?.Invoke();
        }

        private IEnumerator Shrink(GameObject visual)
        {
            var scale = visual.transform.localScale;
            for (float time = 0; time < .2f; time += Time.deltaTime)
            { visual.transform.localScale = scale * Mathf.Max(.01f, 1 - time / .2f); yield return null; }
            Destroy(visual);
        }

        private IEnumerator Burst(GameObject visual)
        {
            for (float time = 0; time < .3f; time += Time.deltaTime)
            { visual.transform.localScale = Vector3.one * (Mathf.Sin(time / .3f * Mathf.PI) * 1.6f); yield return null; }
            Destroy(visual);
        }

        private IEnumerator RaiseGate(GameObject bars)
        {
            var start = bars.transform.localPosition;
            for (float time = 0; time < .3f; time += Time.deltaTime)
            { bars.transform.localPosition = start + Vector3.up * (time / .3f); bars.transform.localScale = new Vector3(1, 1 - time / .3f, 1); yield return null; }
            bars.SetActive(false);
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
            if (t.Remaining == 0) { t.completed = true; if (t.collider != null) t.collider.enabled = false; }
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
            var fragments = new List<GameObject>();
            for (int i = 0; i < 5; i++)
                fragments.Add(Instantiate(catalog.shard, piece.transform.position, Quaternion.identity, transform));
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

    }
}
