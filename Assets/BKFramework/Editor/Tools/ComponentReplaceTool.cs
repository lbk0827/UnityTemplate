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
    public class ComponentReplaceTool : EditorWindow
    {
        // UI Elements
        private ObjectField _folderField;
        private ObjectField _sourceScriptField;
        private ObjectField _targetScriptField;
        private ScrollView _fieldList;
        private Label _fieldsSummary;
        private Button _scanButton;
        private Toggle _includeNestedToggle;
        private ScrollView _prefabList;
        private Label _prefabCount;
        private Button _selectAllButton;
        private Button _deselectAllButton;
        private Button _executeButton;
        private Label _statusLabel;
        private ProgressBar _progressBar;
        private Label _folderInfo;

        // Data
        private string _targetFolderPath;
        private MonoScript _sourceScript;
        private MonoScript _targetScript;
        private List<PrefabScanResult> _scanResults = new List<PrefabScanResult>();
        private List<FieldCompatibilityInfo> _compatibleFields = new List<FieldCompatibilityInfo>();
        private FieldCompatibilityAnalyzer _analyzer = new FieldCompatibilityAnalyzer();

        [MenuItem("BK/Tools/Component Replace Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<ComponentReplaceTool>();
            window.titleContent = new GUIContent("Component Replace Tool");
            window.minSize = new Vector2(350, 400);
        }

        public void CreateGUI()
        {
            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/BKFramework/Editor/Tools/ComponentReplaceTool.uxml");

            if (visualTree == null)
            {
                Debug.LogError("ComponentReplaceTool.uxml not found!");
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            // Get references to UI elements
            _folderField = rootVisualElement.Q<ObjectField>("folder-field");
            _folderInfo = rootVisualElement.Q<Label>("folder-info");
            _sourceScriptField = rootVisualElement.Q<ObjectField>("source-script-field");
            _targetScriptField = rootVisualElement.Q<ObjectField>("target-script-field");
            _fieldList = rootVisualElement.Q<ScrollView>("field-list");
            _fieldsSummary = rootVisualElement.Q<Label>("fields-summary");
            _scanButton = rootVisualElement.Q<Button>("scan-btn");
            _includeNestedToggle = rootVisualElement.Q<Toggle>("include-nested-toggle");
            _prefabList = rootVisualElement.Q<ScrollView>("prefab-list");
            _prefabCount = rootVisualElement.Q<Label>("prefab-count");
            _selectAllButton = rootVisualElement.Q<Button>("select-all-btn");
            _deselectAllButton = rootVisualElement.Q<Button>("deselect-all-btn");
            _executeButton = rootVisualElement.Q<Button>("execute-btn");
            _statusLabel = rootVisualElement.Q<Label>("status-label");
            _progressBar = rootVisualElement.Q<ProgressBar>("progress-bar");

            // Configure ObjectField types
            _folderField.objectType = typeof(DefaultAsset);
            _sourceScriptField.objectType = typeof(MonoScript);
            _targetScriptField.objectType = typeof(MonoScript);

            // Set up event handlers
            _folderField.RegisterValueChangedCallback(OnFolderChanged);
            _sourceScriptField.RegisterValueChangedCallback(OnSourceScriptChanged);
            _targetScriptField.RegisterValueChangedCallback(OnTargetScriptChanged);
            _scanButton.clicked += OnScanPrefabs;
            _selectAllButton.clicked += OnSelectAll;
            _deselectAllButton.clicked += OnDeselectAll;
            _executeButton.clicked += OnExecuteReplacement;

            // Initial state
            _progressBar.style.display = DisplayStyle.None;
            UpdateUI();
        }

        private void OnFolderChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            var folder = evt.newValue as DefaultAsset;
            if (folder != null)
            {
                string path = AssetDatabase.GetAssetPath(folder);
                if (AssetDatabase.IsValidFolder(path))
                {
                    _targetFolderPath = path;
                    _folderInfo.text = $"Selected: {path}";
                }
                else
                {
                    _targetFolderPath = null;
                    _folderInfo.text = "Selected asset is not a folder";
                }
            }
            else
            {
                _targetFolderPath = null;
                _folderInfo.text = "No folder selected";
            }

            ClearScanResults();
            UpdateUI();
        }

        private void OnSourceScriptChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            _sourceScript = evt.newValue as MonoScript;

            if (_sourceScript != null)
            {
                var type = _sourceScript.GetClass();
                if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    _sourceScript = null;
                    EditorUtility.DisplayDialog("Invalid Script",
                        "Selected script is not a valid MonoBehaviour.", "OK");
                    _sourceScriptField.value = null;
                }
            }

            ClearScanResults();
            AnalyzeCompatibility();
            UpdateUI();
        }

        private void OnTargetScriptChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            _targetScript = evt.newValue as MonoScript;

            if (_targetScript != null)
            {
                var type = _targetScript.GetClass();
                if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    _targetScript = null;
                    EditorUtility.DisplayDialog("Invalid Script",
                        "Selected script is not a valid MonoBehaviour.", "OK");
                    _targetScriptField.value = null;
                }
            }

            AnalyzeCompatibility();
            UpdateUI();
        }

        private void AnalyzeCompatibility()
        {
            _compatibleFields.Clear();
            _fieldList.Clear();

            if (_sourceScript == null || _targetScript == null)
            {
                _fieldsSummary.text = "Select both source and target scripts to see compatible fields";
                return;
            }

            var sourceType = _sourceScript.GetClass();
            var targetType = _targetScript.GetClass();

            if (sourceType == null || targetType == null)
            {
                _fieldsSummary.text = "Error: Could not get type from script";
                return;
            }

            var allFields = _analyzer.Analyze(sourceType, targetType);
            _compatibleFields = allFields;

            // Update field list UI
            foreach (var field in allFields.OrderByDescending(f => f.IsCompatible).ThenBy(f => f.FieldName))
            {
                var fieldItem = new VisualElement();
                fieldItem.AddToClassList("field-item");
                fieldItem.AddToClassList(field.IsCompatible ? "field-item-compatible" : "field-item-incompatible");

                var statusIcon = new Label(field.IsCompatible ? "O" : "X");
                statusIcon.AddToClassList("field-status-icon");
                statusIcon.style.color = field.IsCompatible
                    ? new Color(0.3f, 0.8f, 0.3f)
                    : new Color(0.8f, 0.3f, 0.3f);

                var fieldName = new Label(field.FieldName);
                fieldName.AddToClassList("field-name");

                var fieldType = new Label($"{field.SourceTypeName} -> {field.TargetTypeName}");
                fieldType.AddToClassList("field-type");

                if (!field.IsCompatible && !string.IsNullOrEmpty(field.IncompatibilityReason))
                {
                    fieldItem.tooltip = field.IncompatibilityReason;
                }

                fieldItem.Add(statusIcon);
                fieldItem.Add(fieldName);
                fieldItem.Add(fieldType);
                _fieldList.Add(fieldItem);
            }

            int compatibleCount = allFields.Count(f => f.IsCompatible);
            _fieldsSummary.text = $"Compatible: {compatibleCount} / Total: {allFields.Count} fields";
        }

        private void OnScanPrefabs()
        {
            if (string.IsNullOrEmpty(_targetFolderPath))
            {
                EditorUtility.DisplayDialog("No Folder", "Please select a target folder first.", "OK");
                return;
            }

            if (_sourceScript == null)
            {
                EditorUtility.DisplayDialog("No Source Script", "Please select a source component script.", "OK");
                return;
            }

            var sourceType = _sourceScript.GetClass();
            if (sourceType == null)
            {
                EditorUtility.DisplayDialog("Invalid Script", "Source script has no valid class.", "OK");
                return;
            }

            _statusLabel.text = "Scanning prefabs...";
            _progressBar.style.display = DisplayStyle.Flex;
            _progressBar.value = 0;

            try
            {
                var progress = new Progress<float>(p =>
                {
                    _progressBar.value = p * 100;
                });

                _scanResults = ComponentReplaceOperation.ScanPrefabs(
                    _targetFolderPath,
                    sourceType,
                    _includeNestedToggle.value,
                    progress);

                _statusLabel.text = $"Found {_scanResults.Count} prefab(s) with {sourceType.Name}";
            }
            catch (Exception ex)
            {
                _statusLabel.text = "Scan failed!";
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Scan Error", ex.Message, "OK");
            }
            finally
            {
                _progressBar.style.display = DisplayStyle.None;
            }

            UpdatePrefabList();
            UpdateUI();
        }

        private void UpdatePrefabList()
        {
            _prefabList.Clear();

            foreach (var result in _scanResults)
            {
                var prefabItem = new VisualElement();
                prefabItem.AddToClassList("prefab-item");

                var checkbox = new Toggle();
                checkbox.value = result.IsSelected;
                checkbox.AddToClassList("prefab-checkbox");
                checkbox.RegisterValueChangedCallback(evt =>
                {
                    result.IsSelected = evt.newValue;
                    UpdateUI();
                });

                var nameLabel = new Label(result.PrefabName);
                nameLabel.AddToClassList("prefab-name");
                nameLabel.tooltip = result.PrefabPath + "\n\nComponents at:\n" +
                                   string.Join("\n", result.GameObjectPaths);

                var countBadge = new Label(result.ComponentCount.ToString());
                countBadge.AddToClassList("prefab-count-badge");
                countBadge.tooltip = $"{result.ComponentCount} component(s) found";

                prefabItem.Add(checkbox);
                prefabItem.Add(nameLabel);
                prefabItem.Add(countBadge);
                _prefabList.Add(prefabItem);
            }

            int selectedCount = _scanResults.Count(r => r.IsSelected);
            _prefabCount.text = $"({selectedCount}/{_scanResults.Count} selected)";
        }

        private void OnSelectAll()
        {
            foreach (var result in _scanResults)
            {
                result.IsSelected = true;
            }
            UpdatePrefabList();
            UpdateUI();
        }

        private void OnDeselectAll()
        {
            foreach (var result in _scanResults)
            {
                result.IsSelected = false;
            }
            UpdatePrefabList();
            UpdateUI();
        }

        private void ClearScanResults()
        {
            _scanResults.Clear();
            _prefabList.Clear();
            _prefabCount.text = "";
        }

        private void OnExecuteReplacement()
        {
            if (!ValidateBeforeExecution())
                return;

            var sourceType = _sourceScript.GetClass();
            var targetType = _targetScript.GetClass();

            int selectedCount = _scanResults.Count(r => r.IsSelected);
            int compatibleFieldCount = _compatibleFields.Count(f => f.IsCompatible);

            string message = $"Replace {sourceType.Name} with {targetType.Name}?\n\n" +
                            $"Prefabs: {selectedCount}\n" +
                            $"Compatible fields to copy: {compatibleFieldCount}";

            if (!EditorUtility.DisplayDialog("Confirm Replacement", message, "Execute", "Cancel"))
                return;

            _statusLabel.text = "Executing replacement...";
            _progressBar.style.display = DisplayStyle.Flex;
            _progressBar.value = 0;

            try
            {
                var operation = new ComponentReplaceOperation(sourceType, targetType, _compatibleFields);

                var progress = new Progress<ReplaceProgressInfo>(info =>
                {
                    _progressBar.value = info.Progress * 100;
                    _statusLabel.text = info.Status;
                });

                var result = operation.Execute(_scanResults, progress);

                _progressBar.value = 100;

                // Show result dialog
                string resultMessage = $"Replacement completed!\n\n" +
                                       $"Successful prefabs: {result.SuccessfulPrefabs}/{result.TotalPrefabs}\n" +
                                       $"Components replaced: {result.TotalComponentsReplaced}\n" +
                                       $"Fields copied: {result.TotalFieldsCopied}";

                if (result.Errors.Count > 0)
                {
                    resultMessage += $"\n\nErrors: {result.Errors.Count}";
                    foreach (var error in result.Errors.Take(5))
                    {
                        resultMessage += $"\n- {error}";
                    }
                    if (result.Errors.Count > 5)
                    {
                        resultMessage += $"\n... and {result.Errors.Count - 5} more (see Console)";
                    }
                }

                if (result.Warnings.Count > 0)
                {
                    resultMessage += $"\n\nWarnings: {result.Warnings.Count}";
                    foreach (var warning in result.Warnings.Take(3))
                    {
                        resultMessage += $"\n- {warning}";
                    }
                }

                _statusLabel.text = $"Completed: {result.SuccessfulPrefabs}/{result.TotalPrefabs} prefabs";

                EditorUtility.DisplayDialog("Replacement Complete", resultMessage, "OK");

                AssetDatabase.Refresh();
                ClearScanResults();
                UpdateUI();
            }
            catch (Exception ex)
            {
                _statusLabel.text = "Replacement failed!";
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Replacement Error", ex.Message, "OK");
            }
            finally
            {
                _progressBar.style.display = DisplayStyle.None;
            }
        }

        private bool ValidateBeforeExecution()
        {
            var errors = new List<string>();

            // Check folder
            if (string.IsNullOrEmpty(_targetFolderPath) || !AssetDatabase.IsValidFolder(_targetFolderPath))
            {
                errors.Add("Please select a valid target folder.");
            }

            // Check source script
            if (_sourceScript == null)
            {
                errors.Add("Please select a source component script.");
            }
            else
            {
                var sourceType = _sourceScript.GetClass();
                if (sourceType == null)
                {
                    errors.Add("Source script has no valid class (compile errors?).");
                }
                else if (!typeof(MonoBehaviour).IsAssignableFrom(sourceType))
                {
                    errors.Add("Source script must be a MonoBehaviour.");
                }
            }

            // Check target script
            if (_targetScript == null)
            {
                errors.Add("Please select a target component script.");
            }
            else
            {
                var targetType = _targetScript.GetClass();
                if (targetType == null)
                {
                    errors.Add("Target script has no valid class (compile errors?).");
                }
                else if (!typeof(MonoBehaviour).IsAssignableFrom(targetType))
                {
                    errors.Add("Target script must be a MonoBehaviour.");
                }
            }

            // Check scan results
            if (_scanResults == null || !_scanResults.Any(r => r.IsSelected))
            {
                errors.Add("No prefabs selected for replacement. Run scan first.");
            }

            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("Validation Error",
                    string.Join("\n", errors), "OK");
                return false;
            }

            // Check compatible fields (warning, not error)
            if (_compatibleFields == null || !_compatibleFields.Any(f => f.IsCompatible))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "No Compatible Fields",
                    "No compatible fields found between source and target types. " +
                    "Component will be replaced but no values will be copied. Continue?",
                    "Continue", "Cancel");

                if (!proceed) return false;
            }

            return true;
        }

        private void UpdateUI()
        {
            bool hasFolder = !string.IsNullOrEmpty(_targetFolderPath);
            bool hasSource = _sourceScript != null;
            bool hasTarget = _targetScript != null;
            bool hasResults = _scanResults != null && _scanResults.Any(r => r.IsSelected);

            _scanButton.SetEnabled(hasFolder && hasSource);
            _executeButton.SetEnabled(hasFolder && hasSource && hasTarget && hasResults);
            _selectAllButton.SetEnabled(_scanResults.Count > 0);
            _deselectAllButton.SetEnabled(_scanResults.Count > 0);
        }
    }
}
