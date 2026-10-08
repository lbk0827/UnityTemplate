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
        private Sprite panelSprite, buttonSprite;

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
            panelSprite = presentation.settings.GetComponentsInChildren<Image>(true).First(i => i.name == "UI_Setting_01").sprite;
            var win = presentation.win.GetComponentsInChildren<VisualBindings>(true).First(b => b.role == "StageClearPopup");
            buttonSprite = (win.Get<Button>("basic.claimButton").targetGraphic as Image)?.sprite;

            var dim = MakeImage(transform, "Dim", Vector2.zero, Vector2.zero, null, new Color(0, 0, 0, .001f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one; dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            dim.raycastTarget = true;
            var dimButton = dim.gameObject.AddComponent<Button>(); dimButton.transition = Selectable.Transition.None; dimButton.onClick.AddListener(RequestClose);
            Panel = MakeImage(transform, "Panel", PanelSize, Vector2.zero, panelSprite, new Color(.45f, .4f, 1)).rectTransform;
            Panel.GetComponent<Image>().raycastTarget = true;
            Build();
            Feedback = Label(Panel, "Feedback", "", new Vector2(760, 50), new Vector2(0, -PanelSize.y / 2 + 130), 24);
            CloseButton = MakeButton(Panel, "Close", "Close", new Vector2(0, -PanelSize.y / 2 + 65), new Vector2(360, 96), RequestClose);
            return base.OnInitializeAsync(cancellationToken);
        }

        /// <summary>Idempotent close through the UI service; ignored before the open transition completes.</summary>
        public void RequestClose()
        {
            if (closing || !IsOpen) return;
            closing = true;
            ui.CloseAsync(this).Forget();
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
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
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
            var image = MakeImage(parent, name, size, position, buttonSprite, Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => callback());
            Label(image.transform, "Label", text, size - new Vector2(30, 16), Vector2.zero, Mathf.Min(32, size.y * .4f));
            return button;
        }

        protected static TMP_Text LabelOf(Button button) => button.GetComponentInChildren<TMP_Text>(true);
    }
}
