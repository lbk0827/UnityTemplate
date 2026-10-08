using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BK.Kit
{
    // Small, replaceable uGUI view factory. No dependency injection or proprietary UI service.
    public static class KitUI
    {
        public static readonly Color Ink = new Color32(22, 33, 57, 255);
        public static readonly Color Muted = new Color32(146, 165, 192, 255);
        public static readonly Color Accent = new Color32(92, 224, 192, 255);

        public static RectTransform Canvas(Transform owner)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(owner, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(owner);
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        public static Text Label(Transform parent, string value, Vector2 size, Vector2 position, int fontSize, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 position, UnityAction clicked, bool primary = true)
        {
            var rect = Box(parent, name, new Vector2(500, 82), position, primary ? Accent : new Color32(42, 59, 85, 255));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(clicked);
            Label(rect, label, new Vector2(480, 76), Vector2.zero, 25, primary ? Ink : Color.white);
            return button;
        }
    }
}
