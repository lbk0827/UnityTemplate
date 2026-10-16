using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit.Editor
{
    // Rebuilds only the lobby scenery and the clear-result prefab. Reward bindings stay semantic.
    public static class LagoonPresentationSetup
    {
        private const string Art = "Assets/_Project/Bounce/Content/UI/Toy/";
        private static Dictionary<string, Sprite> sprites;
        private static TMP_FontAsset font;
        private static Material light, dark;

        [MenuItem("BK/Integration/Apply Lagoon Lobby And Clear Art")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "LobbyLagoon.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var lagoon = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "LobbyLagoon.png");
            var p = AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var path = AssetDatabase.GetAssetPath(p.lobby);
            var lobby = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var background = lobby.GetComponentsInChildren<Image>(true).First(i => i.name == "IMG_LobbyBg");
                background.sprite = lagoon; background.type = Image.Type.Simple; background.color = Color.white;
                foreach (Transform child in background.transform) child.gameObject.SetActive(false);
                var fitter = background.GetComponent<AspectRatioFitter>() ?? background.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = lagoon.rect.width / lagoon.rect.height;
                PrefabUtility.SaveAsPrefabAsset(lobby, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(lobby); }

            sprites = AssetDatabase.LoadAllAssetsAtPath(Art + "Interface.png").OfType<Sprite>().ToDictionary(s => s.name);
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(Art + "HudIcons.png").OfType<Sprite>()) sprites[s.name] = s;
            font = p.settings.GetComponentInChildren<TMP_Text>(true).font;
            light = AssetDatabase.LoadAssetAtPath<Material>(Art + "InterfaceText_Light_" + font.name.Replace(" ", "_") + ".mat");
            dark = AssetDatabase.LoadAssetAtPath<Material>(Art + "InterfaceText_Dark_" + font.name.Replace(" ", "_") + ".mat");
            var root = new GameObject("popup_stageclear", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(VisualBindings));
            try
            {
                Stretch((RectTransform)root.transform);
                var scenery = Picture(root.transform, "Lagoon backdrop", lagoon, Vector2.zero, Vector2.zero);
                Stretch(scenery.rectTransform);
                var cover = scenery.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; cover.aspectRatio = lagoon.rect.width / lagoon.rect.height;
                var shade = Picture(root.transform, "Backdrop shade", null, Vector2.zero, Vector2.zero);
                Stretch(shade.rectTransform); shade.color = new Color(.025f, .16f, .2f, .38f);
                var panel = Picture(root.transform, "Root", sprites["Card"], new Vector2(0, -30), new Vector2(760, 1000));
                var header = Picture(panel.transform, "Clear ribbon", sprites["Header"], new Vector2(0, 440), new Vector2(660, 150));
                Label(header.transform, "Clear title", "Stage Clear!", Vector2.zero, new Vector2(600, 100), 62, false);
                var level = Label(panel.transform, "Level", "Level 1", new Vector2(0, 310), new Vector2(620, 70), 38, true);
                Label(panel.transform, "Praise", "Great shot!", new Vector2(0, 225), new Vector2(620, 90), 56, true);
                var well = Picture(panel.transform, "Reward well", sprites["Tile"], new Vector2(0, 10), new Vector2(520, 300));
                // Use the same star coin as the HUD and the flight, rather than the imported dollar coins.
                var coin = Picture(well.transform, "Reward coin", sprites["IMG_Coin"], new Vector2(-110, 10), new Vector2(210, 210));
                Label(well.transform, "Reward caption", "COINS", new Vector2(110, 65), new Vector2(220, 55), 28, true);
                var amount = Label(well.transform, "Reward amount", "50", new Vector2(110, -10), new Vector2(240, 100), 78, true);
                Label(panel.transform, "Reward hint", "Your reward is ready", new Vector2(0, -205), new Vector2(620, 70), 32, true);
                var claimImage = Picture(panel.transform, "Collect", sprites["Play"], new Vector2(0, -350), new Vector2(450, 175));
                var claim = claimImage.gameObject.AddComponent<Button>(); claim.targetGraphic = claimImage;
                Label(claim.transform, "Collect label", "Collect", Vector2.zero, new Vector2(380, 100), 56, false);
                var closeImage = Picture(panel.transform, "Close", sprites["Close"], new Vector2(342, 460), new Vector2(86, 86));
                var close = closeImage.gameObject.AddComponent<Button>(); close.targetGraphic = closeImage;

                var currencyBinding = p.hud.GetComponentsInChildren<VisualBindings>(true).First(b => b.role == "UIHUDSub_Top").Get<VisualBindings>("gold");
                var currency = Object.Instantiate(currencyBinding.gameObject, root.transform, false);
                currency.name = "Gold HUD";
                var currencyRect = currency.GetComponent<RectTransform>();
                currencyRect.anchorMin = currencyRect.anchorMax = new Vector2(0, 1);
                currencyRect.pivot = new Vector2(0, 1); currencyRect.anchoredPosition = new Vector2(25, -30);
                currencyRect.sizeDelta = new Vector2(240, 80); currencyRect.localScale = Vector3.one;
                var balance = currency.GetComponentInChildren<TMP_Text>(true);
                var target = currency.GetComponentsInChildren<Image>(true).First(i => i.name == "sp_icon").transform;
                var bindings = root.GetComponent<VisualBindings>(); bindings.role = "StageClearPopup";
                bindings.entries = new[] {
                    Entry("levelText", level), Entry("basic.root", claim.gameObject), Entry("basic.claimButton", claim),
                    Entry("closeButton", close), Entry("rewardAmountText", amount), Entry("goldAmountText", balance),
                    Entry("coinFlyStart", coin.transform), Entry("coinFlyTarget", target)
                };
                PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(p.win));
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets(); Debug.Log("BK_LAGOON_PRESENTATION_OK");
        }

        private static VisualBindings.Entry Entry(string key, Object value) => new VisualBindings.Entry { key = key, value = value };
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Image Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
            image.sprite = sprite; image.raycastTarget = false;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = image.type == Image.Type.Simple && sprite != null;
            return image;
        }
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, bool isDark)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false); text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.font = font; text.fontSharedMaterial = isDark ? dark : light; text.fontSize = fontSize;
            text.color = isDark ? new Color(.16f, .24f, .27f) : new Color(1, .98f, .88f);
            text.alignment = TextAlignmentOptions.Center; text.text = value; text.raycastTarget = false;
            return text;
        }
    }
}
