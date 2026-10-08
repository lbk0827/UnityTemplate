// Ported from the sf RootBox editor tools (namespace RootBoxEditor), 2026-10-08.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BK.Editor.Tools
{
    public class PrefabCleanupTool : EditorWindow
    {
        private ObjectField _prefabField;
        private Label _prefabInfo;
        private Button _removeMissingBtn;
        private Label _missingScriptsInfo;
        private TextField _scriptSearchField;
        private ScrollView _scriptList;
        private Button _removeSelectedScriptsBtn;
        private Toggle _includeChildrenToggle;
        private Label _statusLabel;
        private ProgressBar _progressBar;

        private GameObject _selectedPrefab;
        private List<ScriptInfo> _availableScripts = new List<ScriptInfo>();
        private HashSet<string> _selectedScriptsToRemove = new HashSet<string>();

        [MenuItem("BK/Tools/Prefab Cleanup Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabCleanupTool>();
            window.titleContent = new GUIContent("Prefab Cleanup Tool");
            window.minSize = new Vector2(450, 650);
        }

        public void CreateGUI()
        {
            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/BKFramework/Editor/Tools/PrefabCleanupTool.uxml");
            visualTree.CloneTree(rootVisualElement);

            // Get references to UI elements
            _prefabField = rootVisualElement.Q<ObjectField>("prefab-field");
            _prefabInfo = rootVisualElement.Q<Label>("prefab-info");
            _removeMissingBtn = rootVisualElement.Q<Button>("remove-missing-btn");
            _missingScriptsInfo = rootVisualElement.Q<Label>("missing-scripts-info");
            _scriptSearchField = rootVisualElement.Q<TextField>("script-search-field");
            _scriptList = rootVisualElement.Q<ScrollView>("script-list");
            _removeSelectedScriptsBtn = rootVisualElement.Q<Button>("remove-selected-scripts-btn");
            _includeChildrenToggle = rootVisualElement.Q<Toggle>("include-children-toggle");
            _statusLabel = rootVisualElement.Q<Label>("status-label");
            _progressBar = rootVisualElement.Q<ProgressBar>("progress-bar");

            // Set up event handlers
            _prefabField.RegisterValueChangedCallback(OnPrefabChanged);
            _removeMissingBtn.clicked += OnRemoveMissingScripts;
            _removeSelectedScriptsBtn.clicked += OnRemoveSelectedScripts;
            _scriptSearchField.RegisterValueChangedCallback(OnSearchTextChanged);

            // Initial state
            _progressBar.style.display = DisplayStyle.None;
            UpdateUI();
        }

        private void OnPrefabChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            _selectedPrefab = evt.newValue as GameObject;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_selectedPrefab == null)
            {
                _prefabInfo.text = "No prefab selected";
                _removeMissingBtn.SetEnabled(false);
                _removeSelectedScriptsBtn.SetEnabled(false);
                _scriptList.Clear();
                _availableScripts.Clear();
                _selectedScriptsToRemove.Clear();
                _missingScriptsInfo.text = "";
                return;
            }

            // Check if it's a prefab
            if (PrefabUtility.GetPrefabAssetType(_selectedPrefab) == PrefabAssetType.NotAPrefab)
            {
                _prefabInfo.text = "Selected object is not a prefab!";
                _removeMissingBtn.SetEnabled(false);
                _removeSelectedScriptsBtn.SetEnabled(false);
                _scriptList.Clear();
                _availableScripts.Clear();
                _selectedScriptsToRemove.Clear();
                _missingScriptsInfo.text = "";
                return;
            }

            _prefabInfo.text = $"Prefab: {_selectedPrefab.name}";
            _removeMissingBtn.SetEnabled(true);
            _removeSelectedScriptsBtn.SetEnabled(true);

            // Analyze prefab
            AnalyzePrefab();
        }

        private void AnalyzePrefab()
        {
            if (_selectedPrefab == null) return;

            _availableScripts.Clear();
            _selectedScriptsToRemove.Clear();

            bool includeChildren = _includeChildrenToggle.value;

            // Count missing scripts
            int missingScriptsCount = CountMissingScripts(_selectedPrefab, includeChildren);
            _missingScriptsInfo.text = missingScriptsCount > 0
                ? $"Found {missingScriptsCount} missing script(s)"
                : "No missing scripts found";

            // Collect all scripts
            CollectScripts(_selectedPrefab, includeChildren);

            // Update script list UI
            UpdateScriptList();
        }

        private int CountMissingScripts(GameObject go, bool includeChildren)
        {
            int count = 0;
            var components = go.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null)
                {
                    count++;
                }
            }

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                {
                    count += CountMissingScripts(child.gameObject, true);
                }
            }

            return count;
        }

        private void CollectScripts(GameObject go, bool includeChildren)
        {
            var components = go.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null) continue;

                var type = component.GetType();
                var fullName = type.FullName;

                if (!_availableScripts.Any(s => s.FullName == fullName))
                {
                    _availableScripts.Add(new ScriptInfo
                    {
                        FullName = fullName,
                        ShortName = type.Name,
                        Count = 1
                    });
                }
                else
                {
                    var existing = _availableScripts.First(s => s.FullName == fullName);
                    existing.Count++;
                }
            }

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                {
                    CollectScripts(child.gameObject, true);
                }
            }
        }

        private void UpdateScriptList(string searchFilter = "")
        {
            _scriptList.Clear();

            var filteredScripts = string.IsNullOrEmpty(searchFilter)
                ? _availableScripts
                : _availableScripts.Where(s =>
                    s.ShortName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.FullName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            foreach (var scriptInfo in filteredScripts.OrderBy(s => s.ShortName))
            {
                var toggle = new Toggle
                {
                    text = $"{scriptInfo.ShortName} ({scriptInfo.Count})",
                    tooltip = scriptInfo.FullName,
                    value = _selectedScriptsToRemove.Contains(scriptInfo.FullName)
                };
                toggle.AddToClassList("script-item");

                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        _selectedScriptsToRemove.Add(scriptInfo.FullName);
                    }
                    else
                    {
                        _selectedScriptsToRemove.Remove(scriptInfo.FullName);
                    }
                });

                _scriptList.Add(toggle);
            }

            if (filteredScripts.Count == 0)
            {
                var label = new Label(string.IsNullOrEmpty(searchFilter)
                    ? "No scripts found in prefab"
                    : "No scripts match search filter");
                label.AddToClassList("info-label");
                _scriptList.Add(label);
            }
        }

        private void OnSearchTextChanged(ChangeEvent<string> evt)
        {
            UpdateScriptList(evt.newValue);
        }

        private void OnRemoveMissingScripts()
        {
            if (_selectedPrefab == null) return;

            bool includeChildren = _includeChildrenToggle.value;

            if (!EditorUtility.DisplayDialog(
                "Remove Missing Scripts",
                $"Are you sure you want to remove all missing scripts from '{_selectedPrefab.name}'" +
                (includeChildren ? " and all its children?" : "?"),
                "Yes", "Cancel"))
            {
                return;
            }

            try
            {
                _statusLabel.text = "Removing missing scripts...";
                _progressBar.style.display = DisplayStyle.Flex;
                _progressBar.value = 0;

                string prefabPath = AssetDatabase.GetAssetPath(_selectedPrefab);
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

                int removedCount = RemoveMissingScriptsRecursive(prefabContents, includeChildren);

                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabContents);

                _progressBar.value = 100;
                _statusLabel.text = $"Removed {removedCount} missing script(s)";

                AssetDatabase.Refresh();
                UpdateUI();

                EditorUtility.DisplayDialog("Success",
                    $"Removed {removedCount} missing script(s) from '{_selectedPrefab.name}'", "OK");
            }
            catch (Exception e)
            {
                _statusLabel.text = "Error occurred!";
                Debug.LogError($"Error removing missing scripts: {e.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"An error occurred: {e.Message}", "OK");
            }
            finally
            {
                _progressBar.style.display = DisplayStyle.None;
            }
        }

        private int RemoveMissingScriptsRecursive(GameObject go, bool includeChildren)
        {
            int removedCount = 0;

            // Use GameObjectUtility to remove missing scripts
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removedCount += removed;

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                {
                    removedCount += RemoveMissingScriptsRecursive(child.gameObject, true);
                }
            }

            return removedCount;
        }

        private void OnRemoveSelectedScripts()
        {
            if (_selectedPrefab == null || _selectedScriptsToRemove.Count == 0)
            {
                EditorUtility.DisplayDialog("No Scripts Selected",
                    "Please select at least one script to remove.", "OK");
                return;
            }

            bool includeChildren = _includeChildrenToggle.value;
            string scriptsList = string.Join("\n- ", _selectedScriptsToRemove.Select(s =>
                _availableScripts.First(a => a.FullName == s).ShortName));

            if (!EditorUtility.DisplayDialog(
                "Remove Selected Scripts",
                $"Are you sure you want to remove the following script(s) from '{_selectedPrefab.name}'" +
                (includeChildren ? " and all its children?" : "?") +
                $"\n\n- {scriptsList}",
                "Yes", "Cancel"))
            {
                return;
            }

            try
            {
                _statusLabel.text = "Removing selected scripts...";
                _progressBar.style.display = DisplayStyle.Flex;
                _progressBar.value = 0;

                string prefabPath = AssetDatabase.GetAssetPath(_selectedPrefab);
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

                int removedCount = RemoveSpecificScriptsRecursive(prefabContents,
                    _selectedScriptsToRemove, includeChildren);

                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabContents);

                _progressBar.value = 100;
                _statusLabel.text = $"Removed {removedCount} script instance(s)";

                AssetDatabase.Refresh();
                _selectedScriptsToRemove.Clear();
                UpdateUI();

                EditorUtility.DisplayDialog("Success",
                    $"Removed {removedCount} script instance(s) from '{_selectedPrefab.name}'", "OK");
            }
            catch (Exception e)
            {
                _statusLabel.text = "Error occurred!";
                Debug.LogError($"Error removing scripts: {e.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"An error occurred: {e.Message}", "OK");
            }
            finally
            {
                _progressBar.style.display = DisplayStyle.None;
            }
        }

        private int RemoveSpecificScriptsRecursive(GameObject go, HashSet<string> scriptsToRemove, bool includeChildren)
        {
            int removedCount = 0;
            var components = go.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null) continue;

                var type = component.GetType();
                if (scriptsToRemove.Contains(type.FullName))
                {
                    DestroyImmediate(component, true);
                    removedCount++;
                }
            }

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                {
                    removedCount += RemoveSpecificScriptsRecursive(child.gameObject, scriptsToRemove, true);
                }
            }

            return removedCount;
        }

        private class ScriptInfo
        {
            public string FullName { get; set; }
            public string ShortName { get; set; }
            public int Count { get; set; }
        }
    }
}
