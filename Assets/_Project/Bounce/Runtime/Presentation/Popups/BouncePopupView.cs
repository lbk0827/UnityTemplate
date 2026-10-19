using System;
using System.Linq;
using System.Threading;
using BK.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BK.Kit
{
    /// <summary>
    /// Base for Bounce's BK.UI popups. The generated prefab carries only this component and the presentation
    /// reference; the panel is skinned from the imported art at runtime (same approach as KitDialog). Opens on the
    /// Popup layer with the framework dim, closes on the dim, the close button and the back gesture.
    /// </summary>
    public abstract class BouncePopupView : UIViewBase
    {
        public GamePresentation presentation;

        private IUIService ui;
        private bool closing;
        private TMP_FontAsset font;
        private Material fontMaterial;
        protected Sprite Skin(string name) => presentation.refinedSprites.FirstOrDefault(s => s != null && s.name == name);

        protected KitApp App { get; private set; }
        protected RectTransform Panel { get; private set; }
        protected TMP_Text Feedback { get; private set; }
        public Button CloseButton { get; private set; }

        protected abstract Vector2 PanelSize { get; }
        protected abstract void Build();

        [Inject]
        public void ConstructBase(IUIService ui) => this.ui = ui;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            App = KitApp.Instance;
            var source = presentation.settings.GetComponentInChildren<TMP_Text>(true);
            font = source.font; fontMaterial = source.fontSharedMaterial;

            var dim = MakeImage(transform, "Dim", Vector2.zero, Vector2.zero, null, new Color(0, 0, 0, .001f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one; dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            dim.raycastTarget = true;
            var dimButton = dim.gameObject.AddComponent<Button>(); dimButton.transition = Selectable.Transition.None; dimButton.onClick.AddListener(RequestClose);
            var nativePanel = presentation.heart.GetComponentsInChildren<Image>(true).First(i => i.name == "IMG_Popup");
            Panel = MakeImage(transform, "Panel", PanelSize, Vector2.zero, nativePanel.sprite, Color.white).rectTransform;
            Panel.GetComponent<Image>().raycastTarget = true;
            FitPanel();
            Build();
            Feedback = Label(Panel, "Feedback", "", new Vector2(700, 40), new Vector2(0, -PanelSize.y / 2 + 40), 24);
            var template = presentation.heart.GetComponentsInChildren<Button>(true).First(b => b.name == "BTN_Close");
            CloseButton = Instantiate(template, Panel, false);
            CloseButton.name = "Close";
            Center((RectTransform)CloseButton.transform, new Vector2(PanelSize.x / 2 - 50, PanelSize.y / 2 - 45), new Vector2(120, 120));
            CloseButton.onClick = new Button.ButtonClickedEvent();
            CloseButton.onClick.AddListener(RequestClose);
            CloseButton.interactable = true;
            if (CloseButton.targetGraphic != null) CloseButton.targetGraphic.raycastTarget = true;
            return base.OnInitializeAsync(cancellationToken);
        }

        private void OnRectTransformDimensionsChange() => FitPanel();
        private void FitPanel()
        {
            if (Panel == null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var bounds = ((RectTransform)canvas.rootCanvas.transform).rect;
            Panel.localScale = Vector3.one * Mathf.Min(bounds.width / (PanelSize.x + 80), bounds.height / (PanelSize.y + 180));
        }

        /// <summary>Idempotent close through the UI service; ignored before the open transition completes.</summary>
        public void RequestClose() => CloseAsync().Forget();

        /// <summary>Completes once the view is released; completed at once when a close is already under way.</summary>
        protected UniTask CloseAsync()
        {
            if (closing || !IsOpen) return UniTask.CompletedTask;
            closing = true;
            return ui.CloseAsync(this);
        }

        public override bool OnBackRequested()
        {
            RequestClose();
            return true;
        }

        // ----- skinned building blocks -----

        protected Image MakeImage(Transform parent, string name, Vector2 size, Vector2 position, Sprite sprite, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position;
            image.sprite = sprite; image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; image.color = color; image.raycastTarget = false;
            return image;
        }

        protected TMP_Text Label(Transform parent, string name, string text, Vector2 size, Vector2 position, float fontSize, Color? color = null)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false); label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position;
            label.font = font; label.fontSharedMaterial = fontMaterial; label.fontSize = fontSize; label.text = text;
            label.enableAutoSizing = true; label.fontSizeMin = fontSize * .6f; label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.richText = false;
            if (color.HasValue) label.color = color.Value;
            return label;
        }

        protected Button MakeButton(Transform parent, string name, string text, Vector2 position, Vector2 size, Action callback)
        {
            var template = presentation.retry.GetComponentsInChildren<VisualBindings>(true).First(b => b.role == "UIRetryPopup").Get<Button>("retryButton");
            var button = Instantiate(template, parent, false); button.name = name; button.gameObject.SetActive(true);
            Center((RectTransform)button.transform, position, size);
            foreach (var image in button.GetComponentsInChildren<Image>(true)) image.pixelsPerUnitMultiplier *= 176f / size.y;
            foreach (var sequence in button.GetComponentsInChildren<VisualSequence>(true)) sequence.enabled = false;
            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            { label.text = text; label.enableAutoSizing = true; label.fontSizeMin = 32; label.fontSizeMax = 60; }
            button.targetGraphic.raycastTarget = true;
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => callback());
            return button;
        }

        protected static void Center(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; }

        protected static TMP_Text LabelOf(Button button) => button.GetComponentInChildren<TMP_Text>(true);
    }
}
