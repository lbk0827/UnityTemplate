using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Editor
{
    /// <summary>
    /// 플레이스홀더 UI 를 코드로 조립하기 위한 최소 도구. 유니티 내장 스프라이트와 기본 폰트만 씁니다.
    /// 모든 메서드는 새 GameObject 를 만들어 parent 아래에 붙이고 돌려줍니다.
    /// 메서드 이름에 Make 접두사를 붙인 것은 using static 시 uGUI 타입 이름과 충돌하지 않게 하기 위함입니다.
    /// </summary>
    internal static class UIBuilder
    {
        public static readonly Color Panel = new(0.16f, 0.17f, 0.22f);
        public static readonly Color PanelLight = new(0.24f, 0.25f, 0.32f);
        public static readonly Color Accent = new(0.3f, 0.6f, 0.95f);
        public static readonly Color Positive = new(0.35f, 0.7f, 0.4f);
        public static readonly Color Dim = new(0f, 0f, 0f, 0.65f);
        private const string ArtRoot = "Assets/_Project/Art/Lobby";

        private static Font _font;
        public static Font DefaultFont => _font ??= AssetDatabase.LoadAssetAtPath<Font>(ArtRoot + "/Font/NotoSans-Black.ttf")
                                             ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        public static Sprite CircleSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        public static Sprite CheckSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        public static Sprite CoinSprite => Sprite("IMG_Coin") ?? Sprite("Goods_Coin_01");
        public static Sprite HeartSprite => Sprite("IMG_Heart");

        public static Sprite Sprite(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { ArtRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                    continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        public static RectTransform MakeRect(Transform parent, string name, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var type in components)
                go.AddComponent(type);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>가로로 스트레치, 세로는 고정 높이. anchorY 0 = 아래 붙임, 1 = 위 붙임.</summary>
        public static RectTransform Bar(RectTransform rt, float anchorY, float height, float offset = 0)
        {
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.anchoredPosition = new Vector2(0f, anchorY > 0.5f ? -offset : offset);
            rt.sizeDelta = new Vector2(0f, height);
            return rt;
        }

        public static Image MakeImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var image = MakeRect(parent, name, typeof(Image)).GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        public static Text MakeText(Transform parent, string name, string content, int size,
            TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            var text = MakeRect(parent, name, typeof(Text)).GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.alignment = align;
            text.color = color ?? Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string label, Color color, int fontSize, out Text labelText)
        {
            var sprite = Sprite("BTN_CommonGreen") ?? RoundedSprite;
            var image = MakeImage(parent, name, sprite != null ? Color.white : color, sprite);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            labelText = MakeText(image.transform, "Label", label, fontSize);
            Stretch(labelText.rectTransform);
            return button;
        }

        public static Button MakeButton(Transform parent, string name, string label, Color color, int fontSize = 32)
            => MakeButton(parent, name, label, color, fontSize, out _);

        /// <summary>투명한 전면 버튼. 배경 딤이나 "아무 곳이나 탭" 용.</summary>
        public static Button InvisibleButton(Transform parent, string name, Color color)
        {
            var image = MakeImage(parent, name, color);
            Stretch(image.rectTransform);
            var button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        public static Toggle MakeToggle(Transform parent, string name, Vector2 size, out Image background, out Image checkmark)
        {
            background = MakeImage(parent, name, PanelLight, RoundedSprite);
            background.rectTransform.sizeDelta = size;
            var toggle = background.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;

            checkmark = MakeImage(background.transform, "Checkmark", Accent, RoundedSprite);
            Stretch(checkmark.rectTransform, 4, 4, 4, 4);
            toggle.graphic = checkmark;
            return toggle;
        }

        public static Toggle MakeToggle(Transform parent, string name, Vector2 size)
            => MakeToggle(parent, name, size, out _, out _);

        public static VerticalLayoutGroup VerticalLayout(RectTransform rt, float spacing, RectOffset padding = null, bool controlChildHeight = false)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = controlChildHeight;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup HorizontalLayout(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter, bool controlChildWidth = false)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = controlChildWidth;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static GridLayoutGroup MakeGrid(RectTransform rt, Vector2 cell, Vector2 spacing, int columns)
        {
            var layout = rt.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cell;
            layout.spacing = spacing;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            return layout;
        }

        public static ContentSizeFitter FitHeight(RectTransform rt)
        {
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return fitter;
        }

        public static LayoutElement Layout(RectTransform rt, float height = -1, float width = -1)
        {
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.preferredWidth = width;
            return element;
        }

        /// <summary>세로 스크롤 뷰. content 는 VerticalLayoutGroup + 높이 맞춤이 붙은 상태로 돌려줍니다.</summary>
        public static ScrollRect ScrollView(Transform parent, string name, out RectTransform content, float spacing, RectOffset padding)
        {
            var root = MakeRect(parent, name, typeof(ScrollRect));
            var viewport = MakeRect(root, "Viewport", typeof(RectMask2D), typeof(Image));
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            Stretch(viewport);

            content = MakeRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VerticalLayout(content, spacing, padding);
            FitHeight(content);

            var scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            return scroll;
        }

        /// <summary>아이콘 + 이니셜 + 수량 텍스트. GoodsItemView 의 표준 형태.</summary>
        public static GoodsItemView GoodsItem(Transform parent, string name, Vector2 size, int countSize = 26)
        {
            var root = MakeRect(parent, name, typeof(GoodsItemView));
            root.sizeDelta = size;

            var iconSize = Mathf.Min(size.x, size.y * 0.65f);
            var icon = MakeImage(root, "Icon", Color.white, CircleSprite);
            icon.type = Image.Type.Simple;
            Place(icon.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(iconSize, iconSize));

            var label = MakeText(icon.transform, "Initial", "G", Mathf.RoundToInt(iconSize * 0.45f), TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.1f));
            Stretch(label.rectTransform);

            var count = MakeText(root, "Count", "0", countSize);
            Bar(count.rectTransform, 0f, size.y - iconSize);

            var so = new SerializedObject(root.GetComponent<GoodsItemView>());
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_iconLabel").objectReferenceValue = label;
            so.FindProperty("_countText").objectReferenceValue = count;
            so.FindProperty("_goldSprite").objectReferenceValue = CoinSprite;
            so.FindProperty("_heartSprite").objectReferenceValue = HeartSprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.GetComponent<GoodsItemView>();
        }

        /// <summary>프레임(원형 배경) + 아바타(원형). ProfileBadgeView 의 표준 형태.</summary>
        public static ProfileBadgeView ProfileBadge(Transform parent, string name, float size)
        {
            var root = MakeRect(parent, name, typeof(ProfileBadgeView));
            root.sizeDelta = new Vector2(size, size);

            var frame = MakeImage(root, "Frame", Color.white, Sprite("IMG_ProfileFrame_0") ?? CircleSprite);
            frame.type = Image.Type.Simple;
            Stretch(frame.rectTransform);

            var avatar = MakeImage(root, "Avatar", Color.white, Sprite("IMG_Avatar_0") ?? CircleSprite);
            avatar.type = Image.Type.Simple;
            avatar.preserveAspect = true;
            Stretch(avatar.rectTransform, size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);

            var so = new SerializedObject(root.GetComponent<ProfileBadgeView>());
            so.FindProperty("_frame").objectReferenceValue = frame;
            so.FindProperty("_avatar").objectReferenceValue = avatar;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.GetComponent<ProfileBadgeView>();
        }

        /// <summary>팝업 골격: 전체 스트레치 루트 + 딤 버튼 + 중앙 패널 + 타이틀 + 닫기 X.</summary>
        public static RectTransform PopupShell(GameObject root, Vector2 panelSize, out Button dim, out Button close, out Text title)
        {
            Stretch((RectTransform)root.transform);
            dim = InvisibleButton(root.transform, "Dim", Dim);

            var panel = MakeImage(root.transform, "Panel", Color.white, Sprite("IMG_PopBox") ?? RoundedSprite);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);

            title = MakeText(panel.transform, "Title", "Title", 44);
            Bar(title.rectTransform, 1f, 90f);

            var closeImage = MakeImage(panel.transform, "CloseButton", Color.white, Sprite("BTN_Close_Red") ?? RoundedSprite);
            close = closeImage.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImage;
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(72f, 72f));
            return panel.rectTransform;
        }

        public static void Wire(Object target, System.Action<SerializedObject> configure)
        {
            var so = new SerializedObject(target);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(SerializedProperty array, params Object[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
