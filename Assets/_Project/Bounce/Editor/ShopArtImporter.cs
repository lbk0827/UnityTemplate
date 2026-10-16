using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    // Explicit, repeatable import; never changes shared HUD or in-game art.
    public static class ShopArtImporter
    {
        private const string Root = "Assets/_Project/Bounce/Content/Shop/";
        private static readonly string[] Names = {
            "Goods_Coin_01", "Goods_Coin_02", "Goods_Coin_03", "Goods_Coin_04",
            "Goods_Coin_05", "Goods_Coin_06", "IMG_Bundle_Item_00", "IMG_Bundle_Item_01",
            "IMG_Bundle_Item_02", "IMG_Bundle_Item_03", "IMG_Bundle_Item_04", "IMG_Coin",
            "IMG_Heart", "IMG_HeartInfinite", "IMG_NoAds_Icon2", "Shop_Missile",
            "Shop_ExtraBall", "Shop_Bomb", "Shop_Laser", "IMG_GoodsLight"
        };

        [MenuItem("BK/Integration/Import Original Shop Art")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            string path = Root + "Original/ShopIcons.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            // Reviewed top-left pixel bounds of the approved 1254px source atlas.
            // Explicit Sprite Editor rectangles avoid adjacent-row art in an AI-generated grid.
            var bounds = new[] {
                new Rect(70,95,195,155), new Rect(375,85,190,165), new Rect(663,94,231,159), new Rect(990,62,221,197),
                new Rect(70,315,194,194), new Rect(344,304,245,213), new Rect(690,337,196,174), new Rect(988,313,234,198),
                new Rect(52,566,239,200), new Rect(358,598,244,173), new Rect(663,549,243,225), new Rect(1005,580,194,186),
                new Rect(62,826,207,166), new Rect(360,851,225,126), new Rect(681,800,207,195), new Rect(1001,811,207,193),
                new Rect(66,1046,212,171), new Rect(388,1014,196,198), new Rect(700,1007,178,210), new Rect(1007,1034,196,185)
            };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture.width != 1254 || texture.height != 1254)
                throw new InvalidOperationException("Re-review sprite rectangles when replacing the source atlas");
            var entries = new SpriteMetaData[Names.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var rect = bounds[i]; rect.y = texture.height - rect.yMax;
                entries[i] = new SpriteMetaData { name = Names[i], rect = rect, pivot = new Vector2(.5f,.5f) };
            }
#pragma warning disable CS0618
            importer.spritesheet = entries;
#pragma warning restore CS0618
            importer.isReadable = false;
            importer.SaveAndReimport();
            var icons = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            if (icons.Count != Names.Length) throw new InvalidOperationException("Shop atlas import incomplete");
            var presentation = AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var sprites = presentation.shopSprites.Select(s => icons.TryGetValue(s.name, out var icon) ? icon : s).ToList();
            foreach (var name in Names.Where(n => n.StartsWith("Shop_")))
                if (!sprites.Any(s => s.name == name)) sprites.Add(icons[name]);
            presentation.shopSprites = sprites.ToArray();
            EditorUtility.SetDirty(presentation);
            AssetDatabase.SaveAssets();
            Debug.Log("BK_SHOP_ART_OK: " + icons.Count + " original icons; " + sprites.Count + " shop bindings");
        }
    }
}
