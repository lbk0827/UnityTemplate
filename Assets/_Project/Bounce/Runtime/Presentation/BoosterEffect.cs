using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    // Presentation only. Inventory and board changes remain owned by BounceModule.
    public sealed class BoosterEffect : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();

        public IEnumerator Play(GamePresentation presentation, BoosterKind kind, Vector3 origin, Vector3 target, Action impact)
        {
            var source = presentation.ingame.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "BTN_" + kind);
            var sprite = source == null ? null : source.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name == "IMG_Item")?.sprite;
            Color color = kind == BoosterKind.Laser ? new Color(.25f, .9f, 1)
                : kind == BoosterKind.Bomb ? new Color(1, .45f, .12f) : new Color(1, .85f, .2f);
            var material = new Material(presentation.trajectoryMaterial);
            material.color = color;
            materials.Add(material);
            target.y = origin.y = 1.6f;

            if (kind == BoosterKind.ExtraBall)
            {
                var balls = new Transform[BoosterCatalog.ExtraBallAmount];
                for (int i = 0; i < balls.Length; i++) balls[i] = Icon(sprite, "Extra ball " + i, .65f);
                float elapsed = 0;
                while (elapsed < .45f)
                {
                    elapsed += Time.deltaTime;
                    for (int i = 0; i < balls.Length; i++)
                    {
                        float t = Mathf.Clamp01(elapsed / .45f);
                        var start = origin + new Vector3((i - 2) * .85f, 0, 3);
                        balls[i].position = Vector3.Lerp(start, origin, t * t) + Vector3.forward * Mathf.Sin(t * Mathf.PI) * .7f;
                        balls[i].localScale = Vector3.one * Mathf.Lerp(1, .35f, t);
                    }
                    yield return null;
                }
                foreach (var ball in balls) ball.gameObject.SetActive(false);
                impact();
                yield return Ring(origin, 1.2f, material);
            }
            else if (kind == BoosterKind.Laser)
            {
                origin.x = target.x;
                target.z = 8;
                var beam = Line("Laser", material, .28f);
                var core = Line("Laser core", presentation.trajectoryMaterial, .1f);
                float elapsed = 0;
                while (elapsed < .2f)
                {
                    elapsed += Time.deltaTime;
                    var tip = Vector3.Lerp(origin, target, Mathf.Clamp01(elapsed / .2f));
                    Segment(beam, origin, tip); Segment(core, origin + Vector3.up * .02f, tip + Vector3.up * .02f);
                    yield return null;
                }
                impact();
                elapsed = 0;
                while (elapsed < .3f)
                {
                    elapsed += Time.deltaTime;
                    float width = Mathf.Max(0, 1 - elapsed / .3f);
                    beam.startWidth = beam.endWidth = .45f * width;
                    core.startWidth = core.endWidth = .13f * width;
                    yield return null;
                }
            }
            else
            {
                var projectile = Icon(sprite, kind + " projectile", kind == BoosterKind.Bomb ? 1.4f : 1.1f);
                var trail = Line("Flight trail", material, .1f);
                float elapsed = 0;
                while (elapsed < .4f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / .4f);
                    var position = Vector3.Lerp(origin, target, t);
                    // Screen-space arc across the top-down board, independent of a physics projectile.
                    position.x += Mathf.Sin(t * Mathf.PI) * (kind == BoosterKind.Bomb ? 2 : .7f);
                    projectile.position = position;
                    var direction = target - origin;
                    projectile.localRotation = Quaternion.Euler(90, 0, kind == BoosterKind.Bomb ? -t * 300
                        : 45 - Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
                    Segment(trail, Vector3.Lerp(origin, position, .75f), position);
                    yield return null;
                }
                projectile.gameObject.SetActive(false); trail.gameObject.SetActive(false);
                impact();
                yield return Ring(target, kind == BoosterKind.Bomb ? 1.8f : .8f, material);
            }
        }

        private Transform Icon(Sprite sprite, string label, float size)
        {
            var root = new GameObject(label, typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            root.transform.rotation = Quaternion.Euler(90, 0, 0);
            var rect = (RectTransform)root.transform; rect.sizeDelta = Vector2.one * size;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true; canvas.sortingOrder = 10;
            var image = new GameObject("Original icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(rect, false); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.sizeDelta = Vector2.zero;
            return rect;
        }

        private LineRenderer Line(string label, Material material, float width)
        {
            var line = new GameObject(label, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(transform, false); line.sharedMaterial = material;
            line.startWidth = line.endWidth = width; line.numCapVertices = 4;
            return line;
        }

        private static void Segment(LineRenderer line, Vector3 start, Vector3 end)
        {
            line.positionCount = 2; line.SetPosition(0, start); line.SetPosition(1, end);
        }

        private IEnumerator Ring(Vector3 center, float radius, Material material)
        {
            var ring = Line("Impact ring", material, .2f); ring.loop = true; ring.positionCount = 32;
            float elapsed = 0;
            while (elapsed < .25f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / .25f);
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI * 2 / 32;
                    ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius * Mathf.Lerp(.2f, 1, t));
                }
                ring.startWidth = ring.endWidth = .25f * (1 - t);
                yield return null;
            }
        }

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
