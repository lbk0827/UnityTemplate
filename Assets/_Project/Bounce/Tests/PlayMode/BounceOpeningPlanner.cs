using System.Collections.Generic;
using System.Linq;
using BK.Kit;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Test-only lookahead for static blocks/mines. Actual solutions always fire production balls.</summary>
internal static class BounceOpeningPlanner
{
    private static readonly Vector3 Origin = new Vector3(0, .5f, -8.5f);

    internal static float? Choose(BounceModule game, HashSet<int> missed)
    {
        Physics.SyncTransforms();
        var board = Object.FindFirstObjectByType<BounceGimmicks>();
        var roots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("Block_") && t.GetComponentsInChildren<Collider>().Any(c => c.enabled)).ToArray();
        var colliders = roots.ToDictionary(t => t, t => t.GetComponentsInChildren<Collider>());
        var targets = board.Targets.Where(t => !t.completed && t.spec.type == "mine").ToArray();
        var scores = new int[301];
        for (int i = 0; i < scores.Length; i++)
            scores[i] = Predict(i * .5f - 75, game, colliders, targets);
        float best = -1; float? result = null;
        for (int i = 1; i < scores.Length - 1; i++)
        {
            float angle = i * .5f - 75;
            if (missed.Contains(Mathf.RoundToInt(angle * 4)) || scores[i] == 0) continue;
            // Prefer useful angular intervals over a single lucky grazing trajectory.
            float score = Mathf.Min(scores[i - 1], Mathf.Min(scores[i], scores[i + 1])) + scores[i] * .3f - Mathf.Abs(angle) * .0001f;
            if (score > best) { best = score; result = angle; }
        }
        return result ?? BounceReleaseFlowTests.FindShot(game, missed);
    }

    private static int Predict(float angle, BounceModule game, Dictionary<Transform, Collider[]> blocks, BounceGimmicks.Target[] mines)
    {
        var ignored = new HashSet<Collider>(); var removed = new HashSet<Transform>();
        var detonated = new HashSet<BounceGimmicks.Target>();
        void Ignore(IEnumerable<Collider> colliders)
        { foreach (var c in colliders) if (c.enabled) ignored.Add(c); }
        void Blast(BounceGimmicks.Target mine)
        {
            if (!detonated.Add(mine)) return;
            Ignore(mine.root.GetComponentsInChildren<Collider>());
            bool Near(Vector3 p) => Mathf.Abs(p.x - mine.origin.x) <= 1.01f && Mathf.Abs(p.z - mine.origin.z) <= 1.01f;
            foreach (var b in blocks) if (Near(b.Key.position) && removed.Add(b.Key)) Ignore(b.Value);
            foreach (var other in mines) if (Near(other.origin)) Blast(other);
        }
        var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
        var position = Origin + direction * .9f; float distance = 0;
        for (int bounce = 0; bounce < 80 && distance < 350; bounce++)
        {
            float limit = direction.z < 0 ? Mathf.Max(0, (BounceModule.DeadZoneZ - position.z) / direction.z) : 40;
            var hits = Physics.SphereCastAll(position, game.presentation.ingameConfig.ballRadius, direction, limit, 1 << 10, QueryTriggerInteraction.Ignore)
                .Where(h => !ignored.Contains(h.collider)).OrderBy(h => h.distance).ToArray();
            if (hits.Length == 0) break;
            var hit = hits[0];
            distance += hit.distance;
            var root = hit.collider.transform;
            while (root != null && !blocks.ContainsKey(root)) root = root.parent;
            if (root != null && removed.Add(root)) Ignore(blocks[root]);
            var mine = mines.FirstOrDefault(t => hit.collider.transform.IsChildOf(t.root));
            if (mine != null) Blast(mine);
            position += direction * hit.distance;
            direction = Vector3.Reflect(direction, hit.normal); direction.y = 0; direction.Normalize();
            float minimum = Mathf.Sin(5 * Mathf.Deg2Rad);
            if (Mathf.Abs(direction.x) < minimum) direction.x = direction.x < 0 ? -minimum : minimum;
            if (Mathf.Abs(direction.z) < minimum) direction.z = direction.z < 0 ? -minimum : minimum;
            direction.Normalize();
            position += direction * .001f;
        }
        return removed.Count + detonated.Count;
    }
}
