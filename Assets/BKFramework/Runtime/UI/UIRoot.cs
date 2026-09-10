using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BK.UI
{
    /// <summary>
    /// One canvas per <see cref="UILayer"/>, built at runtime so the framework carries
    /// no prefab dependency. Sorting order comes from the enum value, which is why the
    /// enum is spaced: a project can slot custom layers between the defaults.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        private readonly Dictionary<UILayer, Transform> _layerRoots = new();

        public Vector2 ReferenceResolution { get; private set; } = new Vector2(900f, 1600f);

        public static UIRoot Create(Vector2 referenceResolution)
        {
            var go = new GameObject(nameof(UIRoot));
            DontDestroyOnLoad(go);

            var root = go.AddComponent<UIRoot>();
            root.ReferenceResolution = referenceResolution;
            root.BuildLayers();
            root.EnsureEventSystem();
            return root;
        }

        public Transform GetLayerRoot(UILayer layer)
        {
            if (!_layerRoots.TryGetValue(layer, out var layerRoot))
                throw new ArgumentOutOfRangeException(nameof(layer), layer, "No canvas was built for this layer.");
            return layerRoot;
        }

        private void BuildLayers()
        {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var layerGo = new GameObject(layer.ToString(), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                layerGo.transform.SetParent(transform, false);

                var canvas = layerGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = (int)layer;

                var scaler = layerGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.matchWidthOrHeight = 0f;

                _layerRoots.Add(layer, layerGo.transform);
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            // Project ships with Active Input Handling = Input System Package,
            // so StandaloneInputModule would throw at runtime.
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }
    }
}
