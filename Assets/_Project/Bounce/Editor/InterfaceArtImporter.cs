using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BK.Kit.Editor
{
    public static class InterfaceArtImporter
    {
        private const string Root="Assets/_Project/Bounce/Content/UI/Toy/";
        private static SpriteMetaData Slice(string name,int x,int y,int w,int h,int border=0)
            =>new SpriteMetaData{name=name,rect=new Rect(x,1254-y-h,w,h),pivot=new Vector2(.5f,.5f),border=Vector4.one*border};
        private static Dictionary<string,Sprite> Sheet(string path,SpriteMetaData[] entries)
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;
            t.spriteImportMode=SpriteImportMode.Multiple;t.alphaIsTransparency=true;t.mipmapEnabled=false;
            t.npotScale=TextureImporterNPOTScale.None;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;
#pragma warning disable CS0618
            t.spritesheet=entries;
#pragma warning restore CS0618
            t.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
        }
        [MenuItem("BK/Integration/Import Casual Interface Art")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            var entries=new[]{Slice("Stage",32,117,319,311),Slice("Play",393,194,435,180),
                Slice("Level",832,194,410,177),Slice("Card",28,499,390,282,55),Slice("Header",422,557,459,180,45),
                Slice("Tile",893,490,307,304,48),Slice("Navigation",832,194,410,177,48),Slice("Close",74,887,280,280),Slice("Plus",480,884,285,286),Slice("Switch",843,932,384,216)};
            var icons=AssetDatabase.LoadAllAssetsAtPath(Root+"HudIcons.png").OfType<Sprite>().ToDictionary(x=>x.name);
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");

            // Keep the shop's semantic sprite names so existing renderers and reward flows remain stable.
            var aliases=new List<SpriteMetaData>();
            foreach(var n in new[]{"IMG_Bundle_Bg","IMG_NoADBundle_Bg","IMG_Coin_Goods_Bg"})aliases.Add(Slice(n,28,499,390,282,55));
            foreach(var n in new[]{"IMG_Bundle_Goods_Btn","BTN_CoinBuy"})aliases.Add(Slice(n,393,194,435,180));
            foreach(var n in new[]{"IMG_ShopTitle_Coin","IMG_ShopTitle_NoAd","IMG_ShopTitle_Package","IMG_ShopBar_Coin","IMG_ShopBar_NoAds","IMG_ShopBar_Package","IMG_ShopBox"})aliases.Add(Slice(n,422,557,459,180,45));
            var s=Sheet(Root+"Interface.png",entries.Concat(aliases).ToArray());
            var details=Sheet(Root+"InterfaceDetails.png",new[]{Slice("Canopy",6,182,1242,383),Slice("Sound",12,774,304,309),
                Slice("Music",319,774,303,309),Slice("Haptic",627,774,303,309),Slice("Restart",934,774,309,309)});
            var shop=p.shopSprites.Where(x=>x!=null).ToDictionary(x=>x.name);
            p.shopSprites=p.shopSprites.Select(x=>s.TryGetValue(x.name,out var value)?value:x).Concat(new[]{s["Tile"],s["Level"],details["Canopy"]}).GroupBy(x=>x.name).Select(g=>g.Last()).ToArray();
            EditorUtility.SetDirty(p);
            var paths=new[]{p.home,p.lobby,p.hud,p.ingame,p.settings,p.ingameSettings}.Select(AssetDatabase.GetAssetPath).Concat(new[]{"Assets/_Project/Bounce/Content/Imported/KitResources/Template/Prefabs/UI/Panel/Lobby/Sub/HomeSub/stage_slot.prefab"});
            foreach(var path in paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var b in root.GetComponentsInChildren<VisualBindings>(true))
                        foreach(var e in b.entries)
                            if(e.key=="normalSprite" || e.key=="hardSprite" || e.key=="veryHardSprite")e.value=s["Stage"];
                    foreach(var i in root.GetComponentsInChildren<Image>(true))
                    {
                        var n=i.sprite==null?"":i.sprite.name;var hierarchy=Path(i.transform);Sprite replacement=null;
                        if(hierarchy.Contains("btn_start_"))
                        {if(i.name=="img_frame")i.enabled=false;else if(i.name=="img_bg")replacement=s["Play"];}
                        else if(n.StartsWith("IMG_LevelBG"))replacement=s["Level"];
                        else if(i.name=="sp_setting")replacement=icons["HUD_Settings"];
                        else if(i.name=="sp_restart")replacement=details["Restart"];
                        else if(n=="IMG_SettingSound")replacement=details["Sound"];
                        else if(n=="IMG_SettingBGM")replacement=details["Music"];
                        else if(n=="IMG_SettingHaptic")replacement=details["Haptic"];
                        else if(n=="BTN_SettingFrame")replacement=s["Tile"];
                        else if(n.StartsWith("BTN_Close"))replacement=s["Close"];
                        else if(n=="BTN_Plus")replacement=s["Plus"];
                        else if(n=="IMG_PopBox" || n.StartsWith("IMG_SettingBox") || n=="IMG_Itembg_Normal" || n=="IMG_BallBG")replacement=s["Card"];
                        else if(n=="IMG_PopTitle" || n=="IMG_TitleBox")replacement=s["Header"];
                        else if(n=="IMG_TitlePanel")replacement=s["Level"];
                        else if(n=="IMG_ItemFrame")i.enabled=false;
                        else if(n=="IMG_Lock")replacement=icons["HUD_Lock"];
                        else if(hierarchy.Contains("popup_setting") && i.name=="img_frame")i.enabled=false;
                        else if(hierarchy.Contains("popup_setting") && (i.name=="img_on" || i.name=="img_off"))replacement=s["Header"];
                        else if(hierarchy.Contains("popup_setting") && (i.name=="img_bg" || n=="BTN_Privacy"))replacement=s["Play"];
                        else if(i.name=="IMG_LobbyBg" && path==AssetDatabase.GetAssetPath(p.hud))replacement=s["Navigation"];
                        else if(i.name=="Checkmark" && hierarchy.Contains("tgg_lobby"))replacement=s["Tile"];
                        else if(i.name=="IMG_Box" && hierarchy.Contains("/UI_Right/"))replacement=s["Tile"];
                        if(hierarchy.Contains("/btn_ad/") && i.name=="IMG_Ads" && i.sprite!=null)
                        {replacement=shop["IMG_NoAds_Icon2"];foreach(Transform child in i.transform)child.gameObject.SetActive(false);}
                        if(hierarchy.Contains("/Lobby_WelcomeDeal/"))
                        {if(i.name=="IMG_Char")replacement=shop["IMG_Bundle_Item_00"];else i.enabled=false;}
                        if(replacement==null)continue;
                        i.sprite=replacement;i.type=replacement.border==Vector4.zero?Image.Type.Simple:Image.Type.Sliced;
                        i.enabled=true;i.color=Color.white;i.preserveAspect=replacement.border==Vector4.zero;
                    }
                    foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        var hierarchy=Path(label.transform);
                        if(!(hierarchy.Contains("popup_setting") || hierarchy.Contains("btn_start_") || hierarchy.Contains("stage_slot") || hierarchy.Contains("/UI_Level/")))continue;
                        bool dark=label.name=="TXT_Sound" || label.name=="TXT_Music" || label.name=="TXT_Haptic";
                        string materialPath=Root+"InterfaceText_"+(dark?"Dark_":"Light_")+label.font.name.Replace(" ","_")+".mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(material==null){material=new Material(label.font.material);AssetDatabase.CreateAsset(material,materialPath);}
                        material.SetFloat("_OutlineWidth",dark?0:.09f);material.SetColor("_OutlineColor",new Color(.19f,.09f,.36f));
                        material.SetColor("_FaceColor",Color.white);material.DisableKeyword("UNDERLAY_ON");material.DisableKeyword("UNDERLAY_INNER");
                        label.fontSharedMaterial=material;label.color=dark?new Color(.19f,.09f,.36f):new Color(1,.98f,.89f);
                        EditorUtility.SetDirty(material);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("BK_INTERFACE_ART_OK");
        }
        public static void Inspect()
        {
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var lines=new List<string>();
            var roots=new[]{p.lobby,p.home,p.ingame,p.settings,p.ingameSettings}.Where(x=>x!=null).ToList();
            foreach(var root in roots.ToArray())
                foreach(var b in root.GetComponentsInChildren<VisualBindings>(true))
                    foreach(var e in b.entries)
                        if(e.key=="levelSlotPrefab" && e.value is GameObject go)roots.Add(go);
            foreach(var root in roots.Distinct())
            {
                lines.Add("PREFAB "+AssetDatabase.GetAssetPath(root));
                foreach(var i in root.GetComponentsInChildren<Image>(true))
                    lines.Add(Path(i.transform)+" | "+(i.sprite==null?"NULL":i.sprite.name)+" | "+i.rectTransform.sizeDelta+" | "+i.type);
                foreach(var b in root.GetComponentsInChildren<VisualBindings>(true))
                    foreach(var e in b.entries)lines.Add("BIND "+Path(b.transform)+" | "+e.key+" | "+(e.value==null?"NULL":e.value.name));
            }
            Directory.CreateDirectory("TestResults");File.WriteAllLines("TestResults/interface-art-inventory.txt",lines);
        }
        private static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
    }
}



