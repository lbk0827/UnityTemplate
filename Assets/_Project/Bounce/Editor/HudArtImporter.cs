using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit.Editor
{
    public static class HudArtImporter
    {
        private const string Root="Assets/_Project/Bounce/Content/UI/Toy/";
        private static SpriteMetaData Entry(string name,Rect topLeft,int height,Vector4 border=default)
        {
            topLeft.y=height-topLeft.yMax;
            return new SpriteMetaData {name=name,rect=topLeft,pivot=new Vector2(.5f,.5f),border=border};
        }
        private static Dictionary<string,Sprite> ImportSheet(string path,SpriteMetaData[] entries)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
#pragma warning disable CS0618
            importer.spritesheet=entries;
#pragma warning restore CS0618
            importer.SaveAndReimport();
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
            if(sprites.Count!=entries.Length)throw new InvalidOperationException("Incomplete UI atlas: "+path);
            return sprites;
        }
        [MenuItem("BK/Integration/Import Toy HUD And Shop Art")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            var icons=ImportSheet(Root+"HudIcons.png",new[] {
                Entry("IMG_Coin",new Rect(85,60,390,410),1024),
                Entry("IMG_Heart",new Rect(560,90,410,350),1024),
                Entry("HUD_Store",new Rect(1045,40,440,440),1024),
                Entry("HUD_Home",new Rect(50,535,455,440),1024),
                Entry("HUD_Lock",new Rect(590,535,365,420),1024),
                Entry("HUD_Settings",new Rect(1070,545,410,410),1024)
            });
            var currency=new Rect(8,203,412,180);var card=new Rect(425,196,401,199);
            var buy=new Rect(836,207,410,173);var header=new Rect(825,554,420,172);
            var title=new Rect(720,893,510,180);
            var entries=new List<SpriteMetaData> {
                Entry("HUD_Currency",currency,1254),
                // Transparent space above the plate keeps the original floating icon clear of Play.
                // Preserve the Checkmark RectTransform and all of its child positions.
                Entry("HUD_Tab",new Rect(48,395,293,371),1254,new Vector4(48,48,48,143)),
                Entry("HUD_Navigation",new Rect(380,553,435,172),1254,new Vector4(40,30,40,30)),
                Entry("HUD_Profile",new Rect(60,840,275,280),1254,new Vector4(45,45,45,45)),
                Entry("HUD_SettingsPlate",new Rect(400,840,280,279),1254,new Vector4(45,45,45,45)),
                Entry("IMG_TimeBox",currency,1254),
                Entry("IMG_ShopBox",header,1254,new Vector4(40,30,40,30))
            };
            foreach(var name in new[]{"IMG_Bundle_Bg","IMG_NoADBundle_Bg","IMG_Coin_Goods_Bg"})
                entries.Add(Entry(name,card,1254,new Vector4(45,45,45,45)));
            foreach(var name in new[]{"IMG_Bundle_Goods_Btn","BTN_CoinBuy"})
                entries.Add(Entry(name,buy,1254));
            foreach(var name in new[]{"IMG_ShopTitle_Coin","IMG_ShopTitle_NoAd","IMG_ShopTitle_Package","IMG_ShopBar_Coin","IMG_ShopBar_NoAds","IMG_ShopBar_Package"})
                entries.Add(Entry(name,title,1254,new Vector4(40,30,40,30)));
            var surfaces=ImportSheet(Root+"Surfaces.png",entries.ToArray());
            var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            presentation.shopSprites=presentation.shopSprites.Select(s=>icons.TryGetValue(s.name,out var icon)?icon:surfaces.TryGetValue(s.name,out var surface)?surface:s).ToArray();
            EditorUtility.SetDirty(presentation);
            string prefabPath=AssetDatabase.GetAssetPath(presentation.hud);
            var hud=PrefabUtility.LoadPrefabContents(prefabPath);
            int changed=0;
            try
            {
                foreach(var image in hud.GetComponentsInChildren<Image>(true))
                {
                    string path=PathOf(image.transform),name=image.sprite==null?"":image.sprite.name;
                    Sprite replacement=null;
                    if(image.name=="sp_icon") replacement=icons[path.Contains("/heart/")?"IMG_Heart":"IMG_Coin"];
                    else if(image.name=="sp_box")replacement=surfaces["HUD_Currency"];
                    else if(path.Contains("tgl_store/") && (image.name=="IMG_On" || image.name=="IMG_Off"))replacement=icons["HUD_Store"];
                    else if(path.Contains("tgl_home/") && (image.name=="IMG_On" || image.name=="IMG_Off"))replacement=icons["HUD_Home"];
                    else if(path.Contains("tgl_lock/") && (image.name=="IMG_On" || image.name=="IMG_Off"))replacement=icons["HUD_Lock"];
                    else if(image.name=="sp_setting")replacement=icons["HUD_Settings"];
                    else if(path.Contains("/btn_setting/") && image.name=="img_frame")replacement=surfaces["HUD_SettingsPlate"];
                    else if(image.name=="Checkmark")replacement=surfaces["HUD_Tab"];
                    else if(image.name=="IMG_LobbyBg" || (path.Contains("/tgg_lobby/") && image.name=="Background"))replacement=surfaces["HUD_Navigation"];
                    else if(image.name=="IMG_FrameBack")replacement=surfaces["HUD_Profile"];
                    else if(path.Contains("/profile/Avatar/") || image.name=="IMG_Frame" || image.name=="IMG_Check_Frame" || image.name=="IMG_Profile_Check")image.enabled=false;
                    if(image.name=="IMG_Line1" || image.name=="IMG_Line2")image.color=new Color(.64f,.47f,.86f,.5f);
                    if(replacement==null)continue;
                    image.sprite=replacement;image.type=replacement.border==Vector4.zero?Image.Type.Simple:Image.Type.Sliced;
                    image.preserveAspect=icons.ContainsKey(replacement.name);
                    if(image.name=="Checkmark")image.enabled=true;
                    changed++;
                }
                foreach(var label in hud.GetComponentsInChildren<TMP_Text>(true))
                {
                    // Keep the heart count white on pink; use purple on the cream currency and selected-tab plates.
                    if(label.transform.parent!=null && PathOf(label.transform).Contains("/sp_icon/"))continue;
                    label.color=new Color(.19f,.09f,.36f,label.color.a);
                    if(label.fontSharedMaterial==null)continue;
                    // Stable names let repeated imports reuse the same material assets.
                    string materialPath=Root+"HudText_"+label.font.name.Replace(" ","_")+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null){material=new Material(label.fontSharedMaterial);AssetDatabase.CreateAsset(material,materialPath);}
                    material.SetFloat("_OutlineWidth",0);material.SetColor("_FaceColor",Color.white);
                    material.DisableKeyword("UNDERLAY_ON");material.DisableKeyword("UNDERLAY_INNER");
                    EditorUtility.SetDirty(material);label.fontSharedMaterial=material;
                }
                PrefabUtility.SaveAsPrefabAsset(hud,prefabPath);
            }
            finally {PrefabUtility.UnloadPrefabContents(hud);}
            AssetDatabase.SaveAssets();
            Debug.Log("BK_TOY_UI_OK: "+changed+" HUD images; "+icons.Count+" icons; "+surfaces.Count+" surfaces; shop bindings updated");
            Inspect();
        }
        public static void Inspect()
        {
            var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/hud-art-inventory.txt",presentation.hud.GetComponentsInChildren<Image>(true).Select(i=>
                PathOf(i.transform)+" | "+(i.sprite==null?"NULL":i.sprite.name)+" | "+i.type+" | "+i.rectTransform.sizeDelta+" | "+i.color+" | enabled="+i.enabled+" | "+i.rectTransform.pivot));
        }
        private static string PathOf(Transform t) => t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    }
}
