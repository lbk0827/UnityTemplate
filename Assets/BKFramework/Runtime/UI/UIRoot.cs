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
        public const string DimCanvas = "PopupDim";
        public const string CoverCanvas = "Cover";

        private readonly Dictionary<UILayer, Transform> _layerRoots = new();
        private readonly Dictionary<string, Transform> _auxRoots = new();

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

        /// <summary>
        /// A canvas outside the view stacks (dim, cover). Created on first use with the
        /// same scaler as the layers; the sorting order decides where it sits between them.
        /// </summary>
        public Transform GetAuxiliaryRoot(string name, int sortingOrder)
        {
            if (_auxRoots.TryGetValue(name, out var existing))
                return existing;

            var root = BuildCanvas(name, sortingOrder);
            _auxRoots.Add(name, root);
            return root;
        }

        private void BuildLayers()
        {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                _layerRoots.Add(layer, BuildCanvas(layer.ToString(), (int)layer));
        }

        private Transform BuildCanvas(string name, int sortingOrder)
        {
            var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0f;

            return canvasGo.transform;
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
