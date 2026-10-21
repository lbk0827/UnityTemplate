using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BK.Kit.Editor
{
    public static class Grp1PresentationSetup
    {
        private const string Root="Assets/_Project/Bounce/Content/Imported/KitResources/";
        [MenuItem("BK/Integration/Apply GRP1 Presentation")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var icons=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Bounce/Content/UI/Toy/HudIcons.png").OfType<Sprite>().ToDictionary(s=>s.name);
            var goods=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Bounce/Content/Shop/Original/ShopIcons.png").OfType<Sprite>().ToDictionary(s=>s.name);
            var all=AssetDatabase.FindAssets("t:Sprite",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(path=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()).GroupBy(s=>s.name).ToDictionary(g=>g.Key,g=>g.First());
            Sprite Find(params string[] names)=>names.Select(n=>all.TryGetValue(n,out var s)?s:null).FirstOrDefault(s=>s!=null);
            // Runtime shop data continues to use its semantic names; artwork comes from the source kit.
            p.shopSprites=p.shopSprites.Select(s=>s==null?s:icons.TryGetValue(s.name,out var own)?own:
                goods.TryGetValue(s.name,out var good) && s.name!="IMG_GoodsLight"?good:all.TryGetValue(s.name,out var original)?original:s).ToArray();
            var aliases=new Dictionary<string,Sprite> {
                {"Panel",Find("IMG_PopBoxNew","IMG_PopBox")},{"Header",Find("IMG_PopTitle","IMG_TitleBox")},
                {"Primary",Find("BTN_CommonGreen","BTN_Continue")},{"Secondary",Find("BTN_CommonPurple","BTN_CommonBlue")},
                {"Close",Find("BTN_Close_Red","BTN_Close","BTN_Close_1")},{"Well",Find("IMG_ContentsBox","IMG_ProfileNameBox")},
                {"Hero",Find("IMG_UnlockLight","IMG_GimmickLight")},
                {"ButtonFrame",Find("IMG_ButtonFrame_Basic","IMG_ButtonFrame_Black")},
                {"AvatarBack",Find("IMG_AvatarBack")},{"AvatarCheck",Find("IMG_CheckFrame")}
            };
            var refFolder="Assets/_Project/Bounce/Content/UI/Grp1/";Directory.CreateDirectory(refFolder);
            var palette=aliases.Where(kv=>kv.Value!=null).Select(kv=>Alias(kv.Value,kv.Key,refFolder)).ToList();
            foreach(var type in new[]{"tube","cupboard","panel","mine"})palette.Add(RenderGuide(p.ingameConfig.gimmickPrefabs.Find(type),type,refFolder));
            p.refinedSprites=palette.ToArray();
            var shopAliases=new Dictionary<string,Sprite> {
                {"Shop_Footer",Find("IMG_ShopBar_Package")},{"Shop_Sign",Find("IMG_ShopBox")},
                {"Shop_RewardWell",Find("IMG_ContentsBox")},{"Tile",Find("IMG_ContentsBox")},{"Level",Find("IMG_LevelBGNormar")}
            };
            p.shopSprites=p.shopSprites.Select(s=>s!=null && shopAliases.TryGetValue(s.name,out var source) && source!=null?Alias(source,s.name,refFolder):s).ToArray();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"StaticTemplate/Font/01_KenneyFuture/LilitaOne SDF.asset");
            if(p.bodyFont!=null && !font.fallbackFontAssetTable.Contains(p.bodyFont))
            {font.fallbackFontAssetTable.Add(p.bodyFont);EditorUtility.SetDirty(font);}
            foreach(var prefab in new[]{p.lobby,p.home,p.hud,p.ingame,p.win,p.lose,p.settings,p.ingameSettings,p.quit,p.retry,p.heart,p.unlockGimmick,p.unlockBooster,p.useBooster,p.celebration,p.reward,p.profile,p.welcomeDeal,p.endlessOffer,p.endlessGifts})
            {
                var path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var animator in root.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                    foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if(text.font==null)text.font=font;
                        if(text.fontSharedMaterial!=null)text.fontSharedMaterial=TextMaterial(text.fontSharedMaterial,refFolder);
                        text.raycastTarget=false;
                    }
                    foreach(var image in root.GetComponentsInChildren<Image>(true))
                    {
                        var ancestors=image.GetComponentsInParent<Transform>(true);
                        if(image.sprite!=null && AssetDatabase.GetAssetPath(image.sprite).Contains("UI/Refined/") && image.sprite.name=="Primary")image.sprite=Find("BTN_CommonGreen");
                        if(image.name=="IMG_Heart" && prefab==p.heart){image.sprite=icons["IMG_Heart"];image.preserveAspect=true;}
                        if(image.name=="sp_icon" && image.GetComponentsInParent<Transform>(true).Any(t=>t.name.ToLowerInvariant().Contains("gold")))
                        {image.sprite=icons["IMG_Coin"];image.preserveAspect=true;}
                        if(image.name=="sp_icon" && ancestors.Any(t=>t.name=="heart")){image.sprite=icons["IMG_Heart"];image.preserveAspect=true;}
                        if(image.sprite!=null)
                        {
                            var name=image.sprite.name;
                            string hud=name=="IMG_Store_On" || name=="IMG_Store_Off"?"HUD_Store":name=="IMG_Home_On" || name=="IMG_Home_Off"?"HUD_Home":name=="IMG_Lock_On" || name=="IMG_Lock_Off"?"HUD_Lock":null;
                            if(hud!=null){image.sprite=icons[hud];image.preserveAspect=true;}
                        }
                        if(image.name=="IMG_Item")
                            foreach(var kind in System.Enum.GetNames(typeof(BoosterKind)))if(ancestors.Any(t=>t.name=="BTN_"+kind) && goods.TryGetValue("Shop_"+kind,out var icon)){image.sprite=icon;image.preserveAspect=true;}
                        if(ancestors.Any(t=>t.name=="UI_Right"))
                        {
                            if(image.name=="IMG_Ads" && image.sprite!=null && (image.sprite.name=="IMG_NoAds_Icon" || image.sprite.name=="IMG_NoAds_Icon2"))
                            {image.sprite=goods["IMG_NoAds_Icon2"];image.enabled=true;image.color=Color.white;image.preserveAspect=true;foreach(Transform child in image.transform)child.gameObject.SetActive(false);}
                            if(image.name=="IMG_Char" && ancestors.Any(t=>t.name=="Lobby_WelcomeDeal"))
                            {image.sprite=goods["IMG_Bundle_Item_00"];image.preserveAspect=true;image.rectTransform.anchoredPosition=Vector2.zero;image.rectTransform.sizeDelta=new Vector2(174,174);}
                            if(image.name=="IMG_Coin" && ancestors.Any(t=>t.name=="Lobby_WelcomeDeal"))image.gameObject.SetActive(false);
                        }
                        if(image.name=="IMG_Box" && image.GetComponentsInParent<Transform>(true).Any(t=>t.name=="UI_Right"))
                        {image.type=Image.Type.Simple;image.preserveAspect=true;}
                    }
                    foreach(var button in root.GetComponentsInChildren<Button>(true))button.onClick=new Button.ButtonClickedEvent();
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();
            LagoonPresentationSetup.ApplyStudioLobby();
            var inventory=new List<string>();
            foreach(var go in new[]{p.lobby,p.hud,p.home,p.heart,p.quit,p.retry,p.unlockGimmick,p.unlockBooster,p.useBooster,p.reward,p.profile,p.celebration})
            {
                inventory.Add("PREFAB "+go.name);
                foreach(var t in go.GetComponentsInChildren<RectTransform>(true))
                {
                    var text=t.GetComponent<TMP_Text>();var image=t.GetComponent<Image>();
                    if(text==null && image==null && t.GetComponent<Button>()==null)continue;
                    inventory.Add(PathOf(t)+" | "+t.anchoredPosition+" | "+t.sizeDelta+" | "+t.gameObject.activeSelf+" | "+(text!=null?text.text:image!=null && image.sprite!=null?image.sprite.name:"none"));
                }
                foreach(var b in go.GetComponentsInChildren<VisualBindings>(true))
                    if(b.role.StartsWith("UI") || b.role.Contains("Reward"))foreach(var e in b.entries)inventory.Add("BIND "+b.role+" "+e.key+" => "+(e.value!=null?e.value.name:"none"));
            }
            Directory.CreateDirectory("TestResults");File.WriteAllLines("TestResults/grp1-inventory.txt",inventory);
            File.WriteAllLines("TestResults/grp1-sprites.txt",all.Select(kv=>kv.Key+" | "+AssetDatabase.GetAssetPath(kv.Value)).OrderBy(s=>s));
            Debug.Log("BK_GRP1_PRESENTATION_OK");
        }
        private static Material TextMaterial(Material source,string folder)
        {
            Material material;
            if(AssetDatabase.GetAssetPath(source).StartsWith(folder))material=source;
            else
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id);
                string path=folder+"Text_"+guid+"_"+id+".mat";
                material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
            }
            material.SetFloat("_FaceDilate",0);material.SetFloat("_OutlineWidth",.055f);
            material.SetColor("_OutlineColor",new Color(.16f,.10f,.29f,1));
            material.SetFloat("_UnderlayDilate",0);material.SetFloat("_UnderlayOffsetX",0);material.SetFloat("_UnderlayOffsetY",-.35f);material.SetFloat("_UnderlaySoftness",.12f);
            material.SetColor("_UnderlayColor",new Color(.10f,.05f,.23f,.4f));
            EditorUtility.SetDirty(material);return material;
        }
        internal static Sprite RenderGuide(GimmickView prefab,string type,string folder)
        {
            var path=folder+"Guide_"+type+".png";
            if(File.Exists(path))return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var stage=new GameObject("Guide preview");var instance=Object.Instantiate(prefab,stage.transform);
            stage.transform.position=new Vector3(500,500,500);
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var camera=new GameObject("Guide camera").AddComponent<Camera>();camera.enabled=false;
            var rotation=Quaternion.Euler(58,-16,0);camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*30,rotation);
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.z)*.62f;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;
            var light=new GameObject("Guide key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(48,-35,0);
            var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
            var old=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target;var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);
                texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally
            {RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(stage);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);}
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        private static Sprite Alias(Sprite source,string name,string folder)
        {
            var path=folder+name+".asset";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite!=null)return sprite;
            sprite=Sprite.Create(source.texture,source.rect,source.pivot/source.rect.size,source.pixelsPerUnit,0,SpriteMeshType.FullRect,source.border);
            sprite.name=name;AssetDatabase.CreateAsset(sprite,path);return sprite;
        }
    }
}
