using System.IO;
using BK.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Editor
{
    /// <summary>Builds the framework's own UI prefabs (message popup, toast) and registers them as Addressables.</summary>
    public static class FrameworkUISetup
    {
        public const string Folder = "Assets/BKFramework/Content/UI";
        public const string GroupName = "BK Framework";
        private static readonly Color PanelColor = new(0.13f, 0.14f, 0.18f, 1f);
        private static readonly Color ButtonColor = new(0.30f, 0.55f, 0.95f, 1f);
        private static readonly Color ButtonMutedColor = new(0.35f, 0.36f, 0.42f, 1f);

        [MenuItem("BK/Framework/Generate UI Prefabs")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            var popup = SavePrefab(BuildMessagePopup(), Folder + "/MessagePopup.prefab");
            var toast = SavePrefab(BuildToast(), Folder + "/Toast.prefab");
            Register((popup, "BK/UI/MessagePopup"), (toast, "BK/UI/Toast"));
            AssetDatabase.SaveAssets();
            Debug.Log("BK_FRAMEWORK_UI_OK");
        }

        private static GameObject BuildMessagePopup()
        {
            var root = Root("MessagePopup");
            var view = root.AddComponent<MessagePopupView>();
            view.SetLayer(UILayer.System);
            view.SetDimLevel(DimLevel.Soft);

            var background = MakeImage(root.transform, "Background", new Color(0f, 0f, 0f, 0.001f));
            Stretch(background.rectTransform);
            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;

            var panel = MakeImage(root.transform, "Panel", PanelColor);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 460f));

            var title = MakeText(panel.transform, "Title", 44f, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(640f, 70f));
            var body = MakeText(panel.transform, "Body", 32f, FontStyles.Normal);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(640f, 200f));

            var confirm = MakeButton(panel.transform, "Confirm", ButtonColor, out var confirmLabel);
            Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(150f, 50f), new Vector2(260f, 90f));
            var cancel = MakeButton(panel.transform, "Cancel", ButtonMutedColor, out var cancelLabel);
            Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 50f), new Vector2(260f, 90f));

            view.Bind(title, body, confirm, confirmLabel, cancel, cancelLabel, backgroundButton);
            return root;
        }

        private static GameObject BuildToast()
        {
            var root = Root("Toast");
            var view = root.AddComponent<ToastView>();
            view.SetLayer(UILayer.System);
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;

            var panel = MakeImage(root.transform, "Panel", new Color(0f, 0f, 0f, 0.75f));
            panel.raycastTarget = false;
            Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(700f, 90f));
            var text = MakeText(panel.transform, "Text", 30f, FontStyles.Normal);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            view.Bind(text);
            return root;
        }

        private static GameObject Root(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            Stretch((RectTransform)go.transform);
            return go;
        }

        private static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            return image;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = name;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, Color color, out TMP_Text label)
        {
            var image = MakeImage(parent, name, color);
            var button = image.gameObject.AddComponent<Button>();
            var text = MakeText(image.transform, "Label", 32f, FontStyles.Bold);
            Stretch(text.rectTransform);
            label = text;
            return button;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static string SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        private static void Register(params (string path, string address)[] entries)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            foreach (var (path, address) in entries)
            {
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
                entry.address = address;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        }
    }
}
