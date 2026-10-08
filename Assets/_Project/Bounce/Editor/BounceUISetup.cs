using System;
using System.IO;
using BK.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace BK.Kit.Editor
{
    /// <summary>
    /// Generates Bounce's BK.UI popup prefabs (daily rewards, win-streak pre-play) and registers them as Addressables.
    /// The prefabs carry only the view component and the presentation reference; the views skin themselves at runtime.
    /// </summary>
    public static class BounceUISetup
    {
        public const string Folder = IntegrationSetup.Root + "/Views";

        [MenuItem("BK/Integration/Generate Bounce UI Prefabs")]
        public static void Generate()
        {
            var presentation = AssetDatabase.LoadAssetAtPath<GamePresentation>(IntegrationSetup.Root + "/Content/Presentation.asset");
            if (presentation == null) throw new InvalidOperationException("Missing Bounce presentation.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Missing Addressables settings.");
            var group = IntegrationSetup.EnsureGroup(settings);
            Build<DailyRewardPopup>("DailyRewardPopup", DailyRewardPopup.Address, presentation, settings, group);
            Build<WinStreakPopup>("WinStreakPopup", WinStreakPopup.Address, presentation, settings, group);
            AssetDatabase.SaveAssets();
            Debug.Log("BK_BOUNCE_UI_OK");
        }

        private static void Build<T>(string name, string address, GamePresentation presentation, AddressableAssetSettings settings, AddressableAssetGroup group)
            where T : BouncePopupView
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var view = go.AddComponent<T>();
            view.presentation = presentation;
            view.SetLayer(UILayer.Popup);
            view.SetDimLevel(DimLevel.Soft);
            string path = Folder + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            IntegrationSetup.Register(path, address, settings, group);
        }
    }
}
