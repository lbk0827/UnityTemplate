using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit.Editor
{
    public static class PopupImportSetup
    {
        private const string Base="Assets/_Project/Bounce/Content/Imported/KitResources/Template/Prefabs/UI/Popup/";
        [MenuItem("BK/Integration/Prepare Common Popups")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            p.quit=Load("StageFail/popup_quit");p.retry=Load("Ingame/popup_retry");p.heart=Load("Heart/popup_heart");
            p.unlockGimmick=Load("Ingame/popup_unlockgimmick");p.unlockBooster=Load("Ingame/popup_unlockbooster");p.useBooster=Load("Ingame/popup_usebooster");
            p.celebration=Load("StageClear/popup_stagecelebration");p.reward=Load("Reward/popup_reward");p.profile=Load("Profile/popup_profile");
            var font=p.settings.GetComponentInChildren<TMP_Text>(true);
            var art=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Bounce/Content/UI/Toy/Interface.png").OfType<Sprite>().ToDictionary(s=>s.name);
            var icons=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Bounce/Content/UI/Toy/HudIcons.png").OfType<Sprite>().ToDictionary(s=>s.name);
            foreach(var prefab in new[]{p.quit,p.retry,p.heart,p.unlockGimmick,p.unlockBooster,p.useBooster,p.celebration,p.reward,p.profile})
            {
                var path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {text.font=font.font;text.fontSharedMaterial=font.fontSharedMaterial;text.raycastTarget=false;}
                    foreach(var button in root.GetComponentsInChildren<Button>(true))button.onClick=new Button.ButtonClickedEvent();
                    foreach(var animator in root.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                    foreach(var image in root.GetComponentsInChildren<Image>(true))
                    {
                        Sprite replacement=null;
                        if(image.name=="IMG_Popup" || image.name=="IMG_PopBox")art.TryGetValue("Card",out replacement);
                        else if(image.name=="IMG_PopRibbon")art.TryGetValue("Header",out replacement);
                        else if(image.name=="IMG_Heart" || image.name=="IMG_BrokenHeart")icons.TryGetValue("IMG_Heart",out replacement);
                        if(replacement!=null){image.sprite=replacement;image.color=Color.white;image.type=replacement.border==Vector4.zero?Image.Type.Simple:Image.Type.Sliced;image.preserveAspect=image.type==Image.Type.Simple;}
                    }
                    foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true))group.alpha=1;
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();Debug.Log("BK_COMMON_POPUPS_READY");
        }
        private static GameObject Load(string path)=>AssetDatabase.LoadAssetAtPath<GameObject>(Base+path+".prefab")??throw new InvalidOperationException(path);
    }
}
