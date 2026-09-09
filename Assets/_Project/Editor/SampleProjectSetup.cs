using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using BK.Composition;
using BK.Data;
using BK.Localization;

namespace Project.Editor
{
    /// <summary>
    /// 메뉴 한 번으로 검증용 샘플 프로젝트를 구성합니다. 멱등적으로 동작하므로
    /// 여러 번 실행해도 기존 에셋을 덮어쓰지 않습니다.
    /// 새 게임을 시작할 때는 이 스크립트를 자기 프로젝트 셋업으로 교체하면 됩니다.
    /// </summary>
    public static class SampleProjectSetup
    {
        private const string Root = "Assets/_Project";
        private const string SettingsPath = Root + "/Settings/FrameworkSettings.asset";
        private const string CatalogPath = Root + "/Data/TableCatalog.asset";
        private const string SampleTablePath = Root + "/Data/Tables/SampleTable.asset";
        private const string LocEnPath = Root + "/Localization/en.asset";
        private const string LobbyViewPath = Root + "/UI/LobbyView.prefab";
        private const string BootScenePath = Root + "/Scenes/Boot.unity";
        private const string LobbyScenePath = Root + "/Scenes/Lobby.unity";
        private const string GroupName = "Sample";

        [MenuItem("BK/Setup/Create Sample Project")]
        public static void Run()
        {
            EnsureFolders();

            var settings = GetOrCreate<FrameworkSettings>(SettingsPath);
            var sampleTable = GetOrCreate<SampleTable>(SampleTablePath, so =>
            {
                var rows = so.FindProperty("_rows");
                rows.arraySize = 1;
                var row = rows.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("_id").intValue = 1;
                row.FindPropertyRelative("_nameKey").stringValue = "sample.item.1";
                row.FindPropertyRelative("_value").intValue = 42;
            });
            var catalog = GetOrCreate<TableCatalog>(CatalogPath, so =>
            {
                var addresses = so.FindProperty("_addresses");
                addresses.arraySize = 1;
                addresses.GetArrayElementAtIndex(0).stringValue = "Data/SampleTable";
            });
            var locEn = GetOrCreate<LocalizationTable>(LocEnPath, so =>
            {
                so.FindProperty("_languageCode").stringValue = "en";
                var entries = so.FindProperty("_entries");
                var pairs = new[]
                {
                    ("lobby.title", "Lobby"),
                    ("lobby.body", "{0}: {1}"),
                    ("sample.item.1", "Sample Item"),
                };
                entries.arraySize = pairs.Length;
                for (var i = 0; i < pairs.Length; i++)
                {
                    var e = entries.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Key").stringValue = pairs[i].Item1;
                    e.FindPropertyRelative("Value").stringValue = pairs[i].Item2;
                }
            });

            GetOrCreateLobbyViewPrefab();
            CreateLobbySceneIfMissing();
            CreateBootSceneIfMissing();

            // 씬 저장이 에셋 재임포트를 일으켜 위에서 만든 객체 참조가 죽을 수 있으므로,
            // 이 시점부터는 객체가 아니라 경로로만 다룹니다.
            RegisterAddressables(
                (SampleTablePath, "Data/SampleTable"),
                (CatalogPath, settings.TableCatalogAddress),
                (LocEnPath, settings.LocalizationAddressFor("en")),
                (LobbyViewPath, GameFlow.LobbyViewAddress),
                (LobbyScenePath, GameFlow.LobbySceneAddress));

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(BootScenePath);
            Debug.Log("[Setup] sample project ready - press Play from the Boot scene.");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { "Settings", "Data", "Data/Tables", "Localization", "UI", "Scenes" })
            {
                var full = Root + "/" + dir;
                if (AssetDatabase.IsValidFolder(full))
                    continue;
                var slash = full.LastIndexOf('/');
                AssetDatabase.CreateFolder(full.Substring(0, slash), full.Substring(slash + 1));
            }
        }

        private static T GetOrCreate<T>(string path, System.Action<SerializedObject> configure = null)
            where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            if (configure != null)
            {
                var so = new SerializedObject(asset);
                configure(so);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject GetOrCreateLobbyViewPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyViewPath);
            if (existing != null)
                return existing;

            var root = new GameObject("LobbyView", typeof(RectTransform), typeof(CanvasGroup), typeof(LobbyView));
            Stretch(root.GetComponent<RectTransform>());

            var title = MakeText(root.transform, "Title", 64, new Vector2(0.5f, 0.7f));
            var body = MakeText(root.transform, "Body", 36, new Vector2(0.5f, 0.5f));

            var so = new SerializedObject(root.GetComponent<LobbyView>());
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LobbyViewPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Text MakeText(Transform parent, string name, int size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(900f, 120f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = name;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void CreateLobbySceneIfMissing()
        {
            if (File.Exists(LobbyScenePath))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("LobbyRoot");
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void CreateBootSceneIfMissing()
        {
            if (File.Exists(BootScenePath))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var settings = AssetDatabase.LoadAssetAtPath<FrameworkSettings>(SettingsPath);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(0.1f, 0.1f, 0.12f);

            var scopeGo = new GameObject("ProjectScope", typeof(ProjectScope));
            var so = new SerializedObject(scopeGo.GetComponent<ProjectScope>());
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BootScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
        }

        private static void RegisterAddressables(params (string path, string address)[] entries)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroupName)
                ?? settings.CreateGroup(GroupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            // 재생성으로 GUID가 바뀐 에셋의 옛 엔트리를 정리해 주소 중복을 막습니다.
            foreach (var stale in new System.Collections.Generic.List<AddressableAssetEntry>(group.entries))
            {
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(stale.guid)))
                    settings.RemoveAssetEntry(stale.guid, false);
            }

            foreach (var (path, address) in entries)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"[Setup] missing asset '{path}' for address '{address}'");
                    continue;
                }
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = address;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        }
    }
}
