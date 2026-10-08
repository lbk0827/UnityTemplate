using System;
using System.IO;
using System.Linq;
using BK.Composition;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BK.Kit.Editor
{
    public static class IntegrationSetup
    {
        public const string Root = "Assets/_Project/Bounce";
        public const string Boot = Root + "/Scenes/VisualBootstrap.unity";

        [MenuItem("BK/Integration/Open Bounce Bootstrap")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Boot);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Boot);
        }

        // Explicit, repeatable generation restricted to the imported Bounce integration folder.
        [MenuItem("BK/Integration/Rebuild Integrated Scenes")]
        public static void Setup()
        {
            var presentation = AssetDatabase.LoadAssetAtPath<GamePresentation>(Root + "/Content/Presentation.asset");
            if (presentation == null) throw new InvalidOperationException("Missing Bounce presentation.");
            Directory.CreateDirectory(Root + "/Views");
            AssetDatabase.Refresh();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Missing Addressables settings.");
            var group = settings.FindGroup("Bounce Integration") ?? settings.CreateGroup("Bounce Integration", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);

            foreach (var name in new[] { "VisualLobby", "VisualIngame" })
            {
                bool lobby = name == "VisualLobby";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = Color.black;
                camera.AddComponent<UniversalAdditionalCameraData>();
                if (!lobby)
                {
                    var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
                    light.type = LightType.Directional; light.intensity = 1.5f;
                    light.transform.rotation = Quaternion.Euler(55, -35, 0);
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
                }
                string scenePath = Root + "/Scenes/" + name + ".unity";
                EditorSceneManager.SaveScene(scene, scenePath);
                Register(scenePath, "Bounce/Scenes/" + name, settings, group);

                var go = new GameObject(name + "View", typeof(RectTransform), typeof(ImportedSceneView));
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var view = go.GetComponent<ImportedSceneView>(); view.presentation = presentation; view.lobby = lobby;
                if (!lobby) go.AddComponent<BounceModule>().presentation = presentation;
                string viewPath = Root + "/Views/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(go, viewPath);
                UnityEngine.Object.DestroyImmediate(go);
                Register(viewPath, "Bounce/UI/" + name, settings, group);
            }
            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scope = new GameObject("IntegratedProjectScope").AddComponent<IntegratedProjectScope>();
            var serialized = new SerializedObject(scope);
            serialized.FindProperty("_settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FrameworkSettings>("Assets/_Project/Settings/FrameworkSettings.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(boot, Boot);
            // World scenes are owned by Addressables; only the boot scene belongs in the player list.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Boot, true) };
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Boot);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("BK_INTEGRATION_SETUP_OK");
        }

        private static void Register(string path, string address, AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            entry.address = address;
        }

        // Lobby offer popups were imported after the presentation asset; fill missing references in place.
        [MenuItem("BK/Integration/Ensure Offer Popups")]
        public static void EnsureOfferPopups()
        {
            var p = AssetDatabase.LoadAssetAtPath<GamePresentation>(Root + "/Content/Presentation.asset");
            if (p == null) throw new InvalidOperationException("Missing presentation");
            const string popup = Root + "/Content/Imported/KitResources/Template/Prefabs/UI/Popup/";
            bool changed = false;
            if (p.welcomeDeal == null) { p.welcomeDeal = AssetDatabase.LoadAssetAtPath<GameObject>(popup + "WelcomeDeal/popup_welcomedeal.prefab"); changed = true; }
            if (p.endlessOffer == null) { p.endlessOffer = AssetDatabase.LoadAssetAtPath<GameObject>(popup + "EndlessOffer/popup_endlessoffer.prefab"); changed = true; }
            if (p.endlessGifts == null) { p.endlessGifts = AssetDatabase.LoadAssetAtPath<GameObject>(popup + "EndlessOffer/popup_endlessgifts.prefab"); changed = true; }
            if (changed) { EditorUtility.SetDirty(p); AssetDatabase.SaveAssets(); }
            Debug.Log("BK_OFFER_POPUPS_" + (p.welcomeDeal != null && p.endlessOffer != null && p.endlessGifts != null ? "OK" : "MISSING"));
        }

        [MenuItem("BK/Integration/Validate")]
        public static void Validate()
        {
            if (AssetDatabase.GetAllAssetPaths().Any(x => x.StartsWith("Assets/") && Path.GetFileName(x).Contains("DUG")))
                throw new InvalidOperationException("A replaced font path still contains DUG.");
            // Active Bounce fonts are the OFL pair LilitaOne (Latin) and Jua (Korean fallback); see FontReplacement.
            foreach (var fontPath in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Where(x => x.Contains("LilitaOne SDF") || x.Contains("Jua SDF")))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
                string family = fontPath.Contains("Jua") ? "Jua" : "Lilita";
                if (!font.faceInfo.familyName.Contains(family)) throw new InvalidOperationException("Font glyphs were not replaced: " + fontPath);
                if (font.sourceFontFile == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
                    throw new InvalidOperationException("Incomplete replacement font: " + fontPath);
            }
            var p = AssetDatabase.LoadAssetAtPath<GamePresentation>(Root + "/Content/Presentation.asset");
            if (p == null) throw new InvalidOperationException("Missing presentation");
            foreach (var field in typeof(GamePresentation).GetFields())
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && field.GetValue(p) as UnityEngine.Object == null)
                    throw new InvalidOperationException("Missing presentation field: " + field.Name);
            foreach (string path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(p), true).Where(x => x.EndsWith(".prefab")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new InvalidOperationException("Missing script: " + path);
            }
            if (p.blocks.Any(x => x == null)) throw new InvalidOperationException("Missing block prefab");
            Debug.Log("BK_INTEGRATION_VALIDATION_OK");
        }

        [MenuItem("BK/Integration/Build Windows")]
        public static void Build()
        {
            Validate();
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) throw new InvalidOperationException(content.Error);
            Directory.CreateDirectory("Builds/Bounce");
            var result = BuildPipeline.BuildPlayer(new[] { Boot }, "Builds/Bounce/UnityTemplate_Bounce.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Build failed");
            Debug.Log("BK_INTEGRATION_BUILD_OK");
        }
    }
}
