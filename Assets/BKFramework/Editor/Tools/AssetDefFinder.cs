// Ported from the sf RootBox editor tools (namespace RootBoxEditor), 2026-10-08.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BK.Editor.Tools
{
    public class AssetDefFinder : EditorWindow
    {
        private ObjectField _sourceFolderField;
        private ObjectField _targetFolderField;
        private Button _searchButton;
        private ScrollView _dependencyList;
        private Label _statusLabel;

        private DropdownField _presetDropdown;
        private Button _savePresetButton;
        private Button _deletePresetButton;

        private string _sourceFolderPath = "";
        private string _targetFolderPath = "";
        private List<DependencyInfo> _foundDependencies = new List<DependencyInfo>();

        private const string PREFS_KEY_SOURCE_FOLDER = "AssetDefFinder_SourceFolder";
        private const string PREFS_KEY_TARGET_FOLDER = "AssetDefFinder_TargetFolder";
        private const string PREFS_KEY_PRESETS = "AssetDefFinder_Presets";
        private const string PREFS_KEY_LAST_PRESET = "AssetDefFinder_LastPreset";
        private const string NEW_PRESET_OPTION = "+ New Preset...";

        private Dictionary<string, PresetData> _presets = new Dictionary<string, PresetData>();
        private string _currentPresetName = "";

        [MenuItem("BK/Tools/Asset Dependency Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetDefFinder>();
            window.titleContent = new GUIContent("Asset Dependency Finder");
            window.minSize = new Vector2(420, 550);
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/BKFramework/Editor/Tools/AssetDefFinder.uxml");
            visualTree.CloneTree(rootVisualElement);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/BKFramework/Editor/Tools/AssetDefFinder.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            _sourceFolderField = rootVisualElement.Q<ObjectField>("source-folder-field");
            _targetFolderField = rootVisualElement.Q<ObjectField>("target-folder-field");
            _searchButton = rootVisualElement.Q<Button>("search-btn");
            _dependencyList = rootVisualElement.Q<ScrollView>("dependency-list");
            _statusLabel = rootVisualElement.Q<Label>("status-label");

            _presetDropdown = rootVisualElement.Q<DropdownField>("preset-dropdown");
            _savePresetButton = rootVisualElement.Q<Button>("save-preset-btn");
            _deletePresetButton = rootVisualElement.Q<Button>("delete-preset-btn");

            _sourceFolderField.objectType = typeof(DefaultAsset);
            _sourceFolderField.allowSceneObjects = false;
            _targetFolderField.objectType = typeof(DefaultAsset);
            _targetFolderField.allowSceneObjects = false;

            _sourceFolderField.RegisterValueChangedCallback(OnSourceFolderChanged);
            _targetFolderField.RegisterValueChangedCallback(OnTargetFolderChanged);
            _searchButton.clicked += OnSearchClicked;

            _presetDropdown.RegisterValueChangedCallback(OnPresetChanged);
            _savePresetButton.clicked += OnSavePresetClicked;
            _deletePresetButton.clicked += OnDeletePresetClicked;

            LoadPresets();
            UpdatePresetDropdown();
            LoadLastSettings();

            UpdateStatusLabel();
        }

        private void OnSourceFolderChanged(ChangeEvent<Object> evt)
        {
            if (evt.newValue != null)
            {
                var path = AssetDatabase.GetAssetPath(evt.newValue);
                if (AssetDatabase.IsValidFolder(path))
                {
                    _sourceFolderPath = path;
                    SaveLastSettings();
                }
                else
                {
                    _sourceFolderField.SetValueWithoutNotify(null);
                    _sourceFolderPath = "";
                }
            }
            else
            {
                _sourceFolderPath = "";
            }
            UpdateStatusLabel();
        }

        private void OnTargetFolderChanged(ChangeEvent<Object> evt)
        {
            if (evt.newValue != null)
            {
                var path = AssetDatabase.GetAssetPath(evt.newValue);
                if (AssetDatabase.IsValidFolder(path))
                {
                    _targetFolderPath = path;
                    SaveLastSettings();
                }
                else
                {
                    _targetFolderField.SetValueWithoutNotify(null);
                    _targetFolderPath = "";
                }
            }
            else
            {
                _targetFolderPath = "";
            }
            UpdateStatusLabel();
        }

        private void OnSearchClicked()
        {
            if (string.IsNullOrEmpty(_sourceFolderPath) || string.IsNullOrEmpty(_targetFolderPath))
            {
                EditorUtility.DisplayDialog("Search", "Please set both Source and Target folders.", "OK");
                return;
            }

            SearchDependencies();
            UpdateDependencyList();
            UpdateStatusLabel();
        }

        private void SearchDependencies()
        {
            _foundDependencies.Clear();

            var sourceGuids = AssetDatabase.FindAssets("", new[] { _sourceFolderPath });

            var targetGuids = AssetDatabase.FindAssets("", new[] { _targetFolderPath });
            var targetPaths = new HashSet<string>(
                targetGuids.Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Where(p => !AssetDatabase.IsValidFolder(p)));

            int totalAssets = sourceGuids.Length;
            int processed = 0;

            foreach (var guid in sourceGuids)
            {
                processed++;
                var sourcePath = AssetDatabase.GUIDToAssetPath(guid);

                if (AssetDatabase.IsValidFolder(sourcePath)) continue;

                if (processed % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("Searching Dependencies",
                        $"Processing {processed}/{totalAssets}: {System.IO.Path.GetFileName(sourcePath)}",
                        (float)processed / totalAssets);
                }

                var deps = AssetDatabase.GetDependencies(sourcePath, false);

                var targetDeps = deps.Where(d => IsInTargetFolder(d, targetPaths)).ToList();

                if (targetDeps.Count > 0)
                {
                    var info = AnalyzeDependencyDetails(sourcePath, targetDeps);
                    _foundDependencies.Add(info);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private bool IsInTargetFolder(string assetPath, HashSet<string> targetPaths)
        {
            return targetPaths.Contains(assetPath);
        }

        private DependencyInfo AnalyzeDependencyDetails(string sourcePath, List<string> targetDeps)
        {
            var sourceAsset = AssetDatabase.LoadAssetAtPath<Object>(sourcePath);
            var info = new DependencyInfo
            {
                SourceAsset = sourceAsset,
                SourcePath = sourcePath,
                SourceName = sourceAsset != null ? sourceAsset.name : System.IO.Path.GetFileNameWithoutExtension(sourcePath),
                SourceType = GetAssetTypeName(sourcePath),
                References = new List<DependencyReference>()
            };

            var targetAssetMap = new Dictionary<string, Object>();
            foreach (var targetPath in targetDeps)
            {
                var targetAsset = AssetDatabase.LoadAssetAtPath<Object>(targetPath);
                if (targetAsset != null)
                {
                    targetAssetMap[targetPath] = targetAsset;
                }
            }

            if (sourceAsset is GameObject prefab)
            {
                AnalyzePrefabReferences(prefab, info, targetAssetMap);
            }
            else if (sourceAsset is ScriptableObject so)
            {
                AnalyzeScriptableObjectReferences(so, info, targetAssetMap);
            }
            else
            {
                AnalyzeGenericAssetReferences(sourceAsset, sourcePath, info, targetAssetMap);
            }

            return info;
        }

        private void AnalyzePrefabReferences(GameObject prefab, DependencyInfo info,
            Dictionary<string, Object> targetAssetMap)
        {
            var allTransforms = prefab.GetComponentsInChildren<Transform>(true);

            foreach (var transform in allTransforms)
            {
                var go = transform.gameObject;
                var hierarchyPath = GetHierarchyPath(prefab.transform, transform);
                var components = go.GetComponents<Component>();

                foreach (var component in components)
                {
                    if (component == null) continue;

                    var serializedObject = new SerializedObject(component);
                    var iterator = serializedObject.GetIterator();

                    while (iterator.NextVisible(true))
                    {
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            var objRef = iterator.objectReferenceValue;
                            if (objRef != null)
                            {
                                var refPath = AssetDatabase.GetAssetPath(objRef);
                                if (targetAssetMap.ContainsKey(refPath))
                                {
                                    info.References.Add(new DependencyReference
                                    {
                                        TargetAsset = objRef,
                                        TargetPath = refPath,
                                        TargetName = objRef.name,
                                        TargetType = objRef.GetType().Name,
                                        GameObjectPath = hierarchyPath,
                                        ComponentType = component.GetType().Name,
                                        PropertyPath = iterator.propertyPath
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AnalyzeScriptableObjectReferences(ScriptableObject so, DependencyInfo info,
            Dictionary<string, Object> targetAssetMap)
        {
            var serializedObject = new SerializedObject(so);
            var iterator = serializedObject.GetIterator();

            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var objRef = iterator.objectReferenceValue;
                    if (objRef != null)
                    {
                        var refPath = AssetDatabase.GetAssetPath(objRef);
                        if (targetAssetMap.ContainsKey(refPath))
                        {
                            info.References.Add(new DependencyReference
                            {
                                TargetAsset = objRef,
                                TargetPath = refPath,
                                TargetName = objRef.name,
                                TargetType = objRef.GetType().Name,
                                GameObjectPath = "",
                                ComponentType = so.GetType().Name,
                                PropertyPath = iterator.propertyPath
                            });
                        }
                    }
                }
            }
        }

        private void AnalyzeGenericAssetReferences(Object asset, string sourcePath, DependencyInfo info,
            Dictionary<string, Object> targetAssetMap)
        {
            foreach (var kvp in targetAssetMap)
            {
                info.References.Add(new DependencyReference
                {
                    TargetAsset = kvp.Value,
                    TargetPath = kvp.Key,
                    TargetName = kvp.Value.name,
                    TargetType = kvp.Value.GetType().Name,
                    GameObjectPath = "",
                    ComponentType = "",
                    PropertyPath = "(dependency)"
                });
            }
        }

        private string GetHierarchyPath(Transform root, Transform target)
        {
            if (target == root) return root.name;

            var path = new List<string>();
            var current = target;

            while (current != null && current != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            if (current == root)
            {
                path.Insert(0, root.name);
            }

            return string.Join("/", path);
        }

        private string GetAssetTypeName(string assetPath)
        {
            var ext = System.IO.Path.GetExtension(assetPath).ToLower();
            switch (ext)
            {
                case ".prefab": return "Prefab";
                case ".asset": return "ScriptableObject";
                case ".mat": return "Material";
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd": return "Texture";
                case ".fbx":
                case ".obj": return "Model";
                case ".anim": return "Animation";
                case ".controller": return "AnimatorController";
                case ".cs": return "Script";
                case ".shader": return "Shader";
                case ".unity": return "Scene";
                default: return "Asset";
            }
        }

        private void UpdateDependencyList()
        {
            _dependencyList.Clear();

            if (_foundDependencies.Count == 0)
            {
                var emptyLabel = new Label("No dependencies found from Source(A) to Target(B) folder");
                emptyLabel.AddToClassList("empty-label");
                _dependencyList.Add(emptyLabel);
                return;
            }

            foreach (var depInfo in _foundDependencies)
            {
                var item = CreateDependencyItem(depInfo);
                _dependencyList.Add(item);
            }
        }

        private VisualElement CreateDependencyItem(DependencyInfo depInfo)
        {
            var item = new VisualElement();
            item.AddToClassList("dependency-item");

            var header = new VisualElement();
            header.AddToClassList("dependency-header");

            var nameLabel = new Label(depInfo.SourceName);
            nameLabel.AddToClassList("dependency-name");

            var typeLabel = new Label($"({depInfo.SourceType})");
            typeLabel.AddToClassList("dependency-type");

            header.Add(nameLabel);
            header.Add(typeLabel);

            var pathLabel = new Label(depInfo.SourcePath);
            pathLabel.AddToClassList("dependency-path");

            item.Add(header);
            item.Add(pathLabel);

            var refContainer = new VisualElement();
            refContainer.AddToClassList("ref-container");

            var groupedRefs = depInfo.References
                .GroupBy(r => r.TargetPath)
                .ToList();

            foreach (var group in groupedRefs)
            {
                var firstRef = group.First();

                var refItem = new VisualElement();
                refItem.AddToClassList("ref-item");

                var refHeader = new VisualElement();
                refHeader.AddToClassList("ref-header");

                var arrow = new Label("\u2192");
                arrow.AddToClassList("ref-arrow");

                var targetName = new Label(firstRef.TargetName);
                targetName.AddToClassList("ref-target-name");

                var targetType = new Label($"({firstRef.TargetType})");
                targetType.AddToClassList("ref-target-type");

                refHeader.Add(arrow);
                refHeader.Add(targetName);
                refHeader.Add(targetType);

                refItem.Add(refHeader);

                foreach (var reference in group)
                {
                    var detailText = BuildReferenceDetailText(reference);
                    if (!string.IsNullOrEmpty(detailText))
                    {
                        var detail = new Label(detailText);
                        detail.AddToClassList("ref-detail");
                        refItem.Add(detail);
                    }
                }

                var targetAsset = firstRef.TargetAsset;
                refItem.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (targetAsset != null)
                    {
                        Selection.activeObject = targetAsset;
                        EditorGUIUtility.PingObject(targetAsset);
                    }
                });

                refContainer.Add(refItem);
            }

            item.Add(refContainer);

            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (depInfo.SourceAsset != null)
                {
                    Selection.activeObject = depInfo.SourceAsset;
                    EditorGUIUtility.PingObject(depInfo.SourceAsset);
                }
            });

            return item;
        }

        private string BuildReferenceDetailText(DependencyReference reference)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(reference.GameObjectPath))
            {
                parts.Add($"@ {reference.GameObjectPath}");
            }

            if (!string.IsNullOrEmpty(reference.ComponentType))
            {
                parts.Add($"> {reference.ComponentType}");
            }

            if (!string.IsNullOrEmpty(reference.PropertyPath) && reference.PropertyPath != "(dependency)")
            {
                parts.Add($".{reference.PropertyPath}");
            }

            return string.Join(" ", parts);
        }

        private void UpdateStatusLabel()
        {
            if (string.IsNullOrEmpty(_sourceFolderPath) || string.IsNullOrEmpty(_targetFolderPath))
            {
                _statusLabel.text = "Set folders and click Search";
                return;
            }

            _statusLabel.text = $"Found {_foundDependencies.Count} asset(s) with dependencies to Target folder";
        }

        #region Preset System

        private void SaveLastSettings()
        {
            EditorPrefs.SetString(PREFS_KEY_SOURCE_FOLDER, _sourceFolderPath);
            EditorPrefs.SetString(PREFS_KEY_TARGET_FOLDER, _targetFolderPath);
        }

        private void LoadLastSettings()
        {
            var lastPreset = EditorPrefs.GetString(PREFS_KEY_LAST_PRESET, "");

            if (!string.IsNullOrEmpty(lastPreset) && _presets.ContainsKey(lastPreset))
            {
                _currentPresetName = lastPreset;
                _presetDropdown.SetValueWithoutNotify(lastPreset);
                LoadPresetData(_presets[lastPreset]);
                return;
            }

            var sourcePath = EditorPrefs.GetString(PREFS_KEY_SOURCE_FOLDER, "");
            var targetPath = EditorPrefs.GetString(PREFS_KEY_TARGET_FOLDER, "");

            if (!string.IsNullOrEmpty(sourcePath) && AssetDatabase.IsValidFolder(sourcePath))
            {
                _sourceFolderPath = sourcePath;
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourcePath);
                _sourceFolderField.SetValueWithoutNotify(asset);
            }

            if (!string.IsNullOrEmpty(targetPath) && AssetDatabase.IsValidFolder(targetPath))
            {
                _targetFolderPath = targetPath;
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(targetPath);
                _targetFolderField.SetValueWithoutNotify(asset);
            }
        }

        private void LoadPresetData(PresetData data)
        {
            if (!string.IsNullOrEmpty(data.SourceFolder) && AssetDatabase.IsValidFolder(data.SourceFolder))
            {
                _sourceFolderPath = data.SourceFolder;
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(data.SourceFolder);
                _sourceFolderField.SetValueWithoutNotify(asset);
            }
            else
            {
                _sourceFolderPath = "";
                _sourceFolderField.SetValueWithoutNotify(null);
            }

            if (!string.IsNullOrEmpty(data.TargetFolder) && AssetDatabase.IsValidFolder(data.TargetFolder))
            {
                _targetFolderPath = data.TargetFolder;
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(data.TargetFolder);
                _targetFolderField.SetValueWithoutNotify(asset);
            }
            else
            {
                _targetFolderPath = "";
                _targetFolderField.SetValueWithoutNotify(null);
            }

            UpdateStatusLabel();
        }

        private void SavePresets()
        {
            var presetData = new List<string>();
            foreach (var preset in _presets)
            {
                presetData.Add($"{preset.Key}:{preset.Value.SourceFolder},{preset.Value.TargetFolder}");
            }
            var json = string.Join("|", presetData);
            EditorPrefs.SetString(PREFS_KEY_PRESETS, json);
        }

        private void LoadPresets()
        {
            _presets.Clear();
            var json = EditorPrefs.GetString(PREFS_KEY_PRESETS, "");
            if (string.IsNullOrEmpty(json)) return;

            var presetStrings = json.Split('|');
            foreach (var presetStr in presetStrings)
            {
                if (string.IsNullOrEmpty(presetStr)) continue;

                var colonIndex = presetStr.IndexOf(':');
                if (colonIndex <= 0) continue;

                var name = presetStr.Substring(0, colonIndex);
                var foldersStr = presetStr.Substring(colonIndex + 1);
                var folders = foldersStr.Split(',');

                if (folders.Length >= 2)
                {
                    _presets[name] = new PresetData
                    {
                        SourceFolder = folders[0],
                        TargetFolder = folders[1]
                    };
                }
            }
        }

        private void UpdatePresetDropdown()
        {
            var choices = new List<string> { NEW_PRESET_OPTION };
            choices.AddRange(_presets.Keys.OrderBy(k => k));
            _presetDropdown.choices = choices;

            if (!string.IsNullOrEmpty(_currentPresetName) && _presets.ContainsKey(_currentPresetName))
            {
                _presetDropdown.SetValueWithoutNotify(_currentPresetName);
            }
            else
            {
                _presetDropdown.SetValueWithoutNotify(choices.Count > 1 ? choices[1] : NEW_PRESET_OPTION);
            }
        }

        private void OnPresetChanged(ChangeEvent<string> evt)
        {
            var selectedPreset = evt.newValue;

            if (selectedPreset == NEW_PRESET_OPTION)
            {
                _sourceFolderPath = "";
                _targetFolderPath = "";
                _sourceFolderField.SetValueWithoutNotify(null);
                _targetFolderField.SetValueWithoutNotify(null);
                _currentPresetName = "";
                EditorPrefs.SetString(PREFS_KEY_LAST_PRESET, "");
                _foundDependencies.Clear();
                UpdateDependencyList();
                UpdateStatusLabel();
                return;
            }

            if (_presets.TryGetValue(selectedPreset, out var data))
            {
                _currentPresetName = selectedPreset;
                EditorPrefs.SetString(PREFS_KEY_LAST_PRESET, selectedPreset);
                LoadPresetData(data);
            }
        }

        private void OnSavePresetClicked()
        {
            if (string.IsNullOrEmpty(_sourceFolderPath) && string.IsNullOrEmpty(_targetFolderPath))
            {
                EditorUtility.DisplayDialog("Save Preset", "No folders to save. Set folders first.", "OK");
                return;
            }

            var defaultName = string.IsNullOrEmpty(_currentPresetName) ? "New Preset" : _currentPresetName;
            var presetName = EditorInputDialog.Show("Save Preset", "Enter preset name:", defaultName);

            if (string.IsNullOrEmpty(presetName)) return;

            _presets[presetName] = new PresetData
            {
                SourceFolder = _sourceFolderPath,
                TargetFolder = _targetFolderPath
            };
            _currentPresetName = presetName;
            SavePresets();
            UpdatePresetDropdown();
            _presetDropdown.SetValueWithoutNotify(presetName);
            EditorPrefs.SetString(PREFS_KEY_LAST_PRESET, presetName);
        }

        private void OnDeletePresetClicked()
        {
            var selectedPreset = _presetDropdown.value;

            if (selectedPreset == NEW_PRESET_OPTION || !_presets.ContainsKey(selectedPreset))
            {
                EditorUtility.DisplayDialog("Delete Preset", "No preset selected to delete.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Preset", $"Delete preset '{selectedPreset}'?", "Delete", "Cancel"))
            {
                return;
            }

            _presets.Remove(selectedPreset);
            _currentPresetName = "";
            SavePresets();
            UpdatePresetDropdown();
            EditorPrefs.SetString(PREFS_KEY_LAST_PRESET, "");
        }

        #endregion

        #region Data Classes

        private class DependencyInfo
        {
            public Object SourceAsset { get; set; }
            public string SourcePath { get; set; }
            public string SourceName { get; set; }
            public string SourceType { get; set; }
            public List<DependencyReference> References { get; set; }
        }

        private class DependencyReference
        {
            public Object TargetAsset { get; set; }
            public string TargetPath { get; set; }
            public string TargetName { get; set; }
            public string TargetType { get; set; }
            public string GameObjectPath { get; set; }
            public string ComponentType { get; set; }
            public string PropertyPath { get; set; }
        }

        private class PresetData
        {
            public string SourceFolder { get; set; }
            public string TargetFolder { get; set; }
        }

        #endregion
    }
}
