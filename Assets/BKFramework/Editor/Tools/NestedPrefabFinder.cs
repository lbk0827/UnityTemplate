// Ported from the sf RootBox editor tools (namespace RootBoxEditor), 2026-10-08.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace BK.Editor.Tools
{
    public class NestedPrefabFinder : EditorWindow
    {
        private VisualElement _folderListContainer;
        private Button _addFolderButton;
        private ScrollView _nestedPrefabList;
        private ScrollView _resourceList;
        private Label _statusLabel;
        private Button _refreshButton;
        private Label _currentPrefabLabel;

        // 프리셋 UI 요소
        private DropdownField _presetDropdown;
        private Button _savePresetButton;
        private Button _deletePresetButton;

        private List<string> _targetFolders = new List<string>();
        private List<NestedPrefabInfo> _foundPrefabs = new List<NestedPrefabInfo>();
        private List<ResourceAssetInfo> _foundResources = new List<ResourceAssetInfo>();
        private List<VisualElement> _folderRows = new List<VisualElement>();
        private PrefabStage _lastPrefabStage;

        // Replace 기능용 필드
        private NestedPrefabInfo _pendingReplacePrefabInfo;
        private int _objectPickerControlID;

        // EditorPrefs 키
        private const string PREFS_KEY_LAST_FOLDERS = "NestedPrefabFinder_LastFolders";
        private const string PREFS_KEY_PRESETS = "NestedPrefabFinder_Presets";
        private const string PREFS_KEY_LAST_PRESET = "NestedPrefabFinder_LastPreset";
        private const string NEW_PRESET_OPTION = "+ New Preset...";

        // 프리셋 데이터
        private Dictionary<string, List<string>> _presets = new Dictionary<string, List<string>>();
        private string _currentPresetName = "";

        [MenuItem("BK/Tools/Nested Prefab Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<NestedPrefabFinder>();
            window.titleContent = new GUIContent("Nested Prefab Finder");
            window.minSize = new Vector2(400, 500);
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/BKFramework/Editor/Tools/NestedPrefabFinder.uxml");
            visualTree.CloneTree(rootVisualElement);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/BKFramework/Editor/Tools/NestedPrefabFinder.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            _currentPrefabLabel = rootVisualElement.Q<Label>("current-prefab-label");
            _folderListContainer = rootVisualElement.Q<VisualElement>("folder-list");
            _addFolderButton = rootVisualElement.Q<Button>("add-folder-btn");
            _nestedPrefabList = rootVisualElement.Q<ScrollView>("prefab-list");
            _resourceList = rootVisualElement.Q<ScrollView>("resource-list");
            _statusLabel = rootVisualElement.Q<Label>("status-label");
            _refreshButton = rootVisualElement.Q<Button>("refresh-btn");

            // 프리셋 UI 연결
            _presetDropdown = rootVisualElement.Q<DropdownField>("preset-dropdown");
            _savePresetButton = rootVisualElement.Q<Button>("save-preset-btn");
            _deletePresetButton = rootVisualElement.Q<Button>("delete-preset-btn");

            _addFolderButton.clicked += AddFolderField;
            _refreshButton.clicked += RefreshSearch;
            _savePresetButton.clicked += OnSavePresetClicked;
            _deletePresetButton.clicked += OnDeletePresetClicked;

            _presetDropdown.RegisterValueChangedCallback(OnPresetChanged);

            // 프리셋 및 마지막 폴더 설정 불러오기
            LoadPresets();
            UpdatePresetDropdown();
            LoadLastFolders();

            UpdateCurrentPrefabLabel();
            UpdateStatusLabel();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            _lastPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentStage != _lastPrefabStage)
            {
                _lastPrefabStage = currentStage;
                OnPrefabStageChanged(currentStage);
            }
        }

        private void OnPrefabStageChanged(PrefabStage stage)
        {
            if (stage != null)
            {
                UpdateCurrentPrefabLabel();
                RefreshSearch();
            }
            else
            {
                _foundPrefabs.Clear();
                _foundResources.Clear();
                _nestedPrefabList?.Clear();
                _resourceList?.Clear();
                UpdateCurrentPrefabLabel();
                UpdateStatusLabel();
            }
        }

        private void UpdateCurrentPrefabLabel()
        {
            if (_currentPrefabLabel == null) return;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                _currentPrefabLabel.text = $"Current Prefab: {stage.prefabContentsRoot.name}";
            }
            else
            {
                _currentPrefabLabel.text = "No prefab opened";
            }
        }

        private void AddFolderField()
        {
            var row = new VisualElement();
            row.AddToClassList("folder-row");

            var objectField = new ObjectField();
            objectField.objectType = typeof(DefaultAsset);
            objectField.allowSceneObjects = false;
            objectField.AddToClassList("folder-field");

            objectField.RegisterValueChangedCallback(evt =>
            {
                UpdateTargetFolders();
            });

            var removeButton = new Button(() =>
            {
                RemoveFolderField(row);
            });
            removeButton.text = "X";
            removeButton.AddToClassList("remove-folder-btn");

            row.Add(objectField);
            row.Add(removeButton);

            _folderListContainer.Add(row);
            _folderRows.Add(row);
        }

        private void RemoveFolderField(VisualElement row)
        {
            _folderListContainer.Remove(row);
            _folderRows.Remove(row);
            UpdateTargetFolders();
        }

        private void UpdateTargetFolders()
        {
            _targetFolders.Clear();

            foreach (var row in _folderRows)
            {
                var objectField = row.Q<ObjectField>();
                if (objectField?.value != null)
                {
                    var path = AssetDatabase.GetAssetPath(objectField.value);
                    if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                    {
                        _targetFolders.Add(path);
                    }
                }
            }

            // 마지막 폴더 설정 자동 저장
            SaveLastFolders();
        }

        private void RefreshSearch()
        {
            UpdateTargetFolders();
            SearchNestedPrefabs();
            SearchResourceAssets();
            UpdateNestedPrefabList();
            UpdateResourceList();
            UpdateStatusLabel();
        }

        private void SearchNestedPrefabs()
        {
            _foundPrefabs.Clear();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return;
            }

            if (_targetFolders.Count == 0)
            {
                return;
            }

            var root = stage.prefabContentsRoot;
            SearchNestedPrefabsRecursive(root.transform, "");
        }

        private void SearchNestedPrefabsRecursive(Transform current, string hierarchyPath)
        {
            foreach (Transform child in current)
            {
                var childPath = string.IsNullOrEmpty(hierarchyPath)
                    ? child.name
                    : $"{hierarchyPath}/{child.name}";

                if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    if (sourcePrefab != null)
                    {
                        var sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);

                        if (IsInTargetFolders(sourcePath))
                        {
                            _foundPrefabs.Add(new NestedPrefabInfo
                            {
                                Instance = child.gameObject,
                                SourcePath = sourcePath,
                                HierarchyPath = childPath,
                                PrefabName = sourcePrefab.name
                            });
                        }
                    }
                    // Nested Prefab 내부는 탐색하지 않음 (한 뎁스까지만)
                }
                else
                {
                    // Prefab이 아닌 일반 GameObject만 재귀 탐색
                    SearchNestedPrefabsRecursive(child, childPath);
                }
            }
        }

        private bool IsInTargetFolders(string assetPath)
        {
            foreach (var folder in _targetFolders)
            {
                if (assetPath.StartsWith(folder + "/") || assetPath == folder)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateNestedPrefabList()
        {
            _nestedPrefabList.Clear();

            if (_foundPrefabs.Count == 0)
            {
                var emptyLabel = new Label("No nested prefabs found in target folders");
                emptyLabel.AddToClassList("empty-label");
                _nestedPrefabList.Add(emptyLabel);
                return;
            }

            foreach (var prefabInfo in _foundPrefabs)
            {
                var item = CreatePrefabListItem(prefabInfo);
                _nestedPrefabList.Add(item);
            }
        }

        private VisualElement CreatePrefabListItem(NestedPrefabInfo prefabInfo)
        {
            var item = new VisualElement();
            item.AddToClassList("prefab-item");

            // 헤더 행 (이름)
            var headerRow = new VisualElement();
            headerRow.AddToClassList("prefab-header-row");

            var nameLabel = new Label(prefabInfo.PrefabName);
            nameLabel.AddToClassList("prefab-name");

            headerRow.Add(nameLabel);

            var pathLabel = new Label(prefabInfo.HierarchyPath);
            pathLabel.AddToClassList("prefab-path");

            var sourceLabel = new Label(prefabInfo.SourcePath);
            sourceLabel.AddToClassList("prefab-source");

            // Replace용 행 (ObjectField + Replace 버튼)
            var replaceRow = new VisualElement();
            replaceRow.AddToClassList("replace-row");

            var replaceField = new ObjectField();
            replaceField.objectType = typeof(GameObject);
            replaceField.allowSceneObjects = false;
            replaceField.AddToClassList("replace-field");

            var replaceBtn = new Button(() =>
            {
                var newPrefab = replaceField.value as GameObject;
                if (newPrefab == null)
                {
                    EditorUtility.DisplayDialog("Replace", "Drop a prefab to the field first.", "OK");
                    return;
                }

                if (PrefabUtility.GetPrefabAssetType(newPrefab) == PrefabAssetType.NotAPrefab)
                {
                    EditorUtility.DisplayDialog("Error", "Selected object is not a prefab.", "OK");
                    replaceField.SetValueWithoutNotify(null);
                    return;
                }

                ReplacePrefabInstance(prefabInfo, newPrefab);
            });
            replaceBtn.text = "Replace";
            replaceBtn.AddToClassList("replace-btn");

            replaceRow.Add(replaceField);
            replaceRow.Add(replaceBtn);

            item.Add(headerRow);
            item.Add(pathLabel);
            item.Add(sourceLabel);
            item.Add(replaceRow);

            item.RegisterCallback<ClickEvent>(evt =>
            {
                // Replace 버튼이나 ObjectField 클릭이 아닌 경우에만 선택
                if (evt.target != replaceBtn && !(evt.target is ObjectField))
                {
                    OnPrefabItemClicked(prefabInfo);
                }
            });

            return item;
        }

        private void OnPrefabItemClicked(NestedPrefabInfo prefabInfo)
        {
            if (prefabInfo.Instance != null)
            {
                Selection.activeGameObject = prefabInfo.Instance;
                EditorGUIUtility.PingObject(prefabInfo.Instance);
            }
        }

        private void OnReplacePrefabClicked(NestedPrefabInfo prefabInfo)
        {
            _pendingReplacePrefabInfo = prefabInfo;
            _objectPickerControlID = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<GameObject>(null, false, "t:Prefab", _objectPickerControlID);
        }

        private void OnGUI()
        {
            // Object Picker 결과 처리
            if (Event.current.commandName == "ObjectSelectorClosed")
            {
                if (EditorGUIUtility.GetObjectPickerControlID() == _objectPickerControlID)
                {
                    var selectedPrefab = EditorGUIUtility.GetObjectPickerObject() as GameObject;
                    if (selectedPrefab != null && _pendingReplacePrefabInfo != null)
                    {
                        ReplacePrefabInstance(_pendingReplacePrefabInfo, selectedPrefab);
                    }
                    _pendingReplacePrefabInfo = null;
                }
            }
        }

        private void ReplacePrefabInstance(NestedPrefabInfo oldPrefabInfo, GameObject newPrefab)
        {
            // 1. 유효성 체크 - 프리팹 에셋인지 확인
            if (PrefabUtility.GetPrefabAssetType(newPrefab) == PrefabAssetType.NotAPrefab)
            {
                EditorUtility.DisplayDialog("Error", "Selected object is not a prefab.", "OK");
                return;
            }

            // 인스턴스가 유효한지 확인
            if (oldPrefabInfo.Instance == null)
            {
                EditorUtility.DisplayDialog("Error", "Original prefab instance is no longer valid.", "OK");
                RefreshSearch();
                return;
            }

            var result = new OverrideCopyResult();

            // 2. 기존 Transform 정보 저장
            var oldTransform = oldPrefabInfo.Instance.transform;
            var localPosition = oldTransform.localPosition;
            var localRotation = oldTransform.localRotation;
            var localScale = oldTransform.localScale;
            var parent = oldTransform.parent;
            var siblingIndex = oldTransform.GetSiblingIndex();
            var instanceName = oldPrefabInfo.Instance.name;

            // 3. 오버라이드 정보 수집 (인스턴스 삭제 전)
            var oldInstance = oldPrefabInfo.Instance;
            var oldPrefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(oldInstance);
            var propertyMods = PrefabUtility.GetPropertyModifications(oldInstance);
            var addedComponents = PrefabUtility.GetAddedComponents(oldInstance);
            var addedGameObjects = PrefabUtility.GetAddedGameObjects(oldInstance);

            // 4. AddedComponents 정보 미리 저장 (인스턴스 삭제 전) - 임시 GameObject에 복사
            var componentCopyInfos = new List<(GameObject tempHolder, System.Type compType, string relativePath)>();
            if (addedComponents != null)
            {
                foreach (var addedComp in addedComponents)
                {
                    if (addedComp.instanceComponent != null)
                    {
                        var relativePath = GetRelativeHierarchyPath(oldInstance.transform, addedComp.instanceComponent.transform);
                        var compType = addedComp.instanceComponent.GetType();

                        // 임시 GameObject 생성 및 컴포넌트 복사 (삭제 전에 수행)
                        var tempHolder = new GameObject("__TempComponentHolder__");
                        tempHolder.SetActive(false);
                        ComponentUtility.CopyComponent(addedComp.instanceComponent);
                        ComponentUtility.PasteComponentAsNew(tempHolder);

                        componentCopyInfos.Add((tempHolder, compType, relativePath));
                    }
                }
            }

            // 5. AddedGameObjects 정보 미리 저장 (인스턴스 삭제 전) - 복제본 생성
            var gameObjectCopyInfos = new List<(GameObject copy, string parentPath, int siblingIdx, string name, Vector3 srcLocalPos, Quaternion srcLocalRot, Vector3 srcLocalScale)>();
            if (addedGameObjects != null)
            {
                foreach (var addedGO in addedGameObjects)
                {
                    if (addedGO.instanceGameObject != null)
                    {
                        var srcObject = addedGO.instanceGameObject;
                        var srcTransform = srcObject.transform;
                        var parentPath = GetRelativeHierarchyPath(oldInstance.transform, srcTransform.parent);

                        // 임시로 복제본 생성 (부모 없이)
                        var tempCopy = Object.Instantiate(srcObject);
                        tempCopy.SetActive(false);
                        gameObjectCopyInfos.Add((tempCopy, parentPath, srcTransform.GetSiblingIndex(), srcObject.name,
                            srcTransform.localPosition, srcTransform.localRotation, srcTransform.localScale));
                    }
                }
            }

            // 6. 기존 인스턴스 제거
            DestroyImmediate(oldInstance);

            // 7. 새 프리팹 인스턴스 생성
            var newInstance = PrefabUtility.InstantiatePrefab(newPrefab, parent) as GameObject;

            // 8. Transform 복원
            newInstance.transform.localPosition = localPosition;
            newInstance.transform.localRotation = localRotation;
            newInstance.transform.localScale = localScale;
            newInstance.transform.SetSiblingIndex(siblingIndex);
            newInstance.name = instanceName;  // 기존 이름 유지

            // 9. PropertyModifications 복사
            if (propertyMods != null && propertyMods.Length > 0)
            {
                CopyPropertyModifications(oldPrefabAsset, newPrefab, newInstance, propertyMods, result);
            }

            // 이름이 PropertyModifications에 의해 변경될 수 있으므로 다시 설정
            newInstance.name = instanceName;

            // 10. AddedComponents 복사 (임시 holder에서 복사)
            foreach (var (tempHolder, compType, relativePath) in componentCopyInfos)
            {
                var targetTransform = FindTransformByHierarchyPath(newInstance.transform, relativePath);
                if (targetTransform != null)
                {
                    // tempHolder에서 해당 타입의 컴포넌트 찾기
                    var tempComp = tempHolder.GetComponent(compType);
                    if (tempComp != null)
                    {
                        ComponentUtility.CopyComponent(tempComp);
                        ComponentUtility.PasteComponentAsNew(targetTransform.gameObject);
                        result.CopiedComponents++;
                    }
                    else
                    {
                        result.FailedComponents++;
                        result.FailedComponentDetails.Add($"{compType.Name} on {relativePath} (temp copy failed)");
                    }
                }
                else
                {
                    result.FailedComponents++;
                    result.FailedComponentDetails.Add($"{compType.Name} on {relativePath}");
                }

                // 임시 holder 정리
                DestroyImmediate(tempHolder);
            }

            // 11. AddedGameObjects 복사
            foreach (var (tempCopy, parentPath, siblingIdx, objName, srcLocalPos, srcLocalRot, srcLocalScale) in gameObjectCopyInfos)
            {
                var newParent = FindTransformByHierarchyPath(newInstance.transform, parentPath);
                if (newParent != null)
                {
                    tempCopy.transform.SetParent(newParent);
                    // 로컬 Transform 복원
                    tempCopy.transform.localPosition = srcLocalPos;
                    tempCopy.transform.localRotation = srcLocalRot;
                    tempCopy.transform.localScale = srcLocalScale;
                    tempCopy.transform.SetSiblingIndex(siblingIdx);
                    tempCopy.name = objName;
                    tempCopy.SetActive(true);
                    result.CopiedGameObjects++;
                }
                else
                {
                    result.FailedGameObjects++;
                    result.FailedGameObjectDetails.Add($"{objName} (parent: {parentPath})");
                    DestroyImmediate(tempCopy); // 실패 시 임시 복제본 제거
                }
            }

            // 10. 프리팹 스테이지 dirty 처리 (저장 필요 표시)
            EditorUtility.SetDirty(newInstance);

            // 11. 새 인스턴스 선택
            Selection.activeGameObject = newInstance;

            // 12. 결과 표시
            ShowOverrideCopyResult(result);

            // 13. 목록 새로고침
            RefreshSearch();
        }

        private void CopyPropertyModifications(GameObject oldPrefabAsset, GameObject newPrefabAsset,
            GameObject newInstance, PropertyModification[] propertyMods, OverrideCopyResult result)
        {
            var newMods = new List<PropertyModification>();

            foreach (var mod in propertyMods)
            {
                // Transform 관련은 제외 (이미 별도 처리)
                if (mod.propertyPath.StartsWith("m_LocalPosition") ||
                    mod.propertyPath.StartsWith("m_LocalRotation") ||
                    mod.propertyPath.StartsWith("m_LocalScale") ||
                    mod.propertyPath.StartsWith("m_RootOrder") ||
                    mod.propertyPath == "m_Name")
                {
                    continue;
                }

                // 새 프리팹에서 대응 오브젝트 찾기
                var newTarget = FindCorrespondingObjectInPrefab(oldPrefabAsset, newPrefabAsset, mod.target);

                if (newTarget != null && IsPropertyPathValid(newTarget, mod.propertyPath))
                {
                    // objectReference 처리: 프리팹 에셋 내부 객체면 매핑 필요
                    Object newObjectReference = null;
                    if (mod.objectReference != null)
                    {
                        var objRefPath = AssetDatabase.GetAssetPath(mod.objectReference);
                        var oldPrefabPath = AssetDatabase.GetAssetPath(oldPrefabAsset);

                        if (!string.IsNullOrEmpty(objRefPath) && objRefPath == oldPrefabPath)
                        {
                            // objectReference가 구 프리팹 에셋 내부 객체인 경우 → 새 프리팹에서 대응 객체 찾기
                            newObjectReference = FindCorrespondingObjectInPrefab(oldPrefabAsset, newPrefabAsset, mod.objectReference);
                        }
                        else
                        {
                            // 외부 에셋(Sprite, Material 등)이거나 씬 객체인 경우 그대로 사용
                            newObjectReference = mod.objectReference;
                        }
                    }

                    // objectReference가 필요한데 매핑 실패한 경우 스킵
                    if (mod.objectReference != null && newObjectReference == null)
                    {
                        result.FailedPropertyMods++;
                        result.FailedPropertyModDetails.Add($"{mod.target?.name ?? "null"}.{mod.propertyPath} (objectReference mapping failed)");
                        continue;
                    }

                    newMods.Add(new PropertyModification
                    {
                        target = newTarget,
                        propertyPath = mod.propertyPath,
                        value = mod.value,
                        objectReference = newObjectReference
                    });
                    result.CopiedPropertyMods++;
                }
                else
                {
                    result.FailedPropertyMods++;
                    result.FailedPropertyModDetails.Add($"{mod.target?.name ?? "null"}.{mod.propertyPath}");
                }
            }

            // 적용
            if (newMods.Count > 0)
            {
                var existingMods = PrefabUtility.GetPropertyModifications(newInstance);
                var allMods = existingMods?.ToList() ?? new List<PropertyModification>();
                allMods.AddRange(newMods);
                PrefabUtility.SetPropertyModifications(newInstance, allMods.ToArray());
            }
        }

        private void SearchResourceAssets()
        {
            _foundResources.Clear();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || _targetFolders.Count == 0)
            {
                return;
            }

            var root = stage.prefabContentsRoot;
            var foundAssets = new Dictionary<Object, ResourceAssetInfo>();

            // 루트 오브젝트의 컴포넌트도 검색
            SearchComponentsForResources(root, root.name, foundAssets);

            // 자식 오브젝트들 재귀 탐색
            SearchResourceAssetsRecursive(root.transform, "", foundAssets);

            _foundResources = foundAssets.Values.ToList();
        }

        private void SearchComponentsForResources(GameObject obj, string hierarchyPath, Dictionary<Object, ResourceAssetInfo> foundAssets)
        {
            var components = obj.GetComponents<Component>();
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
                            var assetPath = AssetDatabase.GetAssetPath(objRef);
                            if (!string.IsNullOrEmpty(assetPath) && IsInTargetFolders(assetPath))
                            {
                                // GameObject나 Component는 제외 (리소스 에셋만)
                                if (!(objRef is GameObject) && !(objRef is Component))
                                {
                                    var reference = new ResourceReference
                                    {
                                        GameObject = obj,
                                        HierarchyPath = hierarchyPath,
                                        Component = component,
                                        PropertyPath = iterator.propertyPath
                                    };

                                    if (foundAssets.ContainsKey(objRef))
                                    {
                                        foundAssets[objRef].References.Add(reference);
                                    }
                                    else
                                    {
                                        foundAssets[objRef] = new ResourceAssetInfo
                                        {
                                            Asset = objRef,
                                            AssetPath = assetPath,
                                            AssetName = objRef.name,
                                            AssetType = objRef.GetType().Name,
                                            AssetSystemType = objRef.GetType(),
                                            References = new List<ResourceReference> { reference }
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SearchResourceAssetsRecursive(Transform current, string hierarchyPath, Dictionary<Object, ResourceAssetInfo> foundAssets)
        {
            foreach (Transform child in current)
            {
                var childPath = string.IsNullOrEmpty(hierarchyPath)
                    ? child.name
                    : $"{hierarchyPath}/{child.name}";

                // NestedPrefab 내부는 탐색하지 않음
                if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    continue;
                }

                // 현재 오브젝트의 모든 컴포넌트에서 리소스 에셋 찾기
                SearchComponentsForResources(child.gameObject, childPath, foundAssets);

                // 재귀 탐색
                SearchResourceAssetsRecursive(child, childPath, foundAssets);
            }
        }

        private void UpdateResourceList()
        {
            _resourceList.Clear();

            if (_foundResources.Count == 0)
            {
                var emptyLabel = new Label("No resource assets found in target folders");
                emptyLabel.AddToClassList("empty-label");
                _resourceList.Add(emptyLabel);
                return;
            }

            foreach (var resourceInfo in _foundResources)
            {
                var item = CreateResourceListItem(resourceInfo);
                _resourceList.Add(item);
            }
        }

        private VisualElement CreateResourceListItem(ResourceAssetInfo resourceInfo)
        {
            var item = new VisualElement();
            item.AddToClassList("resource-item");

            var nameLabel = new Label(resourceInfo.AssetName);
            nameLabel.AddToClassList("resource-name");

            var typeLabel = new Label($"[{resourceInfo.AssetType}]");
            typeLabel.AddToClassList("resource-type");

            var pathLabel = new Label(resourceInfo.AssetPath);
            pathLabel.AddToClassList("resource-path");

            var refCount = resourceInfo.References.Count;
            var refCountLabel = new Label($"Used in {refCount} place{(refCount > 1 ? "s" : "")}");
            refCountLabel.AddToClassList("resource-ref-count");

            // Replace용 행 (ObjectField + Replace 버튼)
            var replaceRow = new VisualElement();
            replaceRow.AddToClassList("resource-replace-row");

            var replaceField = new ObjectField();
            replaceField.objectType = resourceInfo.AssetSystemType;
            replaceField.allowSceneObjects = false;
            replaceField.AddToClassList("resource-replace-field");

            var replaceBtn = new Button(() =>
            {
                var newAsset = replaceField.value;
                if (newAsset == null)
                {
                    EditorUtility.DisplayDialog("Replace", "Drop an asset to the field first.", "OK");
                    return;
                }

                ReplaceResourceAsset(resourceInfo, newAsset);
            });
            replaceBtn.text = "Replace";
            replaceBtn.AddToClassList("resource-replace-btn");

            replaceRow.Add(replaceField);
            replaceRow.Add(replaceBtn);

            item.Add(nameLabel);
            item.Add(typeLabel);
            item.Add(pathLabel);
            item.Add(refCountLabel);
            item.Add(replaceRow);

            item.RegisterCallback<ClickEvent>(evt =>
            {
                // Replace 버튼이나 ObjectField 클릭이 아닌 경우에만 선택
                if (evt.target != replaceBtn && !(evt.target is ObjectField))
                {
                    OnResourceItemClicked(resourceInfo);
                }
            });

            return item;
        }

        private void OnResourceItemClicked(ResourceAssetInfo resourceInfo)
        {
            if (resourceInfo.Asset != null)
            {
                Selection.activeObject = resourceInfo.Asset;
                EditorGUIUtility.PingObject(resourceInfo.Asset);
            }
        }

        private void ReplaceResourceAsset(ResourceAssetInfo resourceInfo, Object newAsset)
        {
            if (newAsset == null)
            {
                EditorUtility.DisplayDialog("Replace", "Drop an asset to the field first.", "OK");
                return;
            }

            // 타입 검증
            if (newAsset.GetType() != resourceInfo.AssetSystemType)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Type mismatch. Expected {resourceInfo.AssetType}, got {newAsset.GetType().Name}", "OK");
                return;
            }

            // 같은 에셋으로 교체 시도 체크
            if (newAsset == resourceInfo.Asset)
            {
                EditorUtility.DisplayDialog("Replace", "Cannot replace with the same asset.", "OK");
                return;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                EditorUtility.DisplayDialog("Error", "No prefab opened.", "OK");
                return;
            }

            int replacedCount = 0;
            int failedCount = 0;
            var failedDetails = new List<string>();

            foreach (var reference in resourceInfo.References)
            {
                if (reference.Component == null)
                {
                    failedCount++;
                    failedDetails.Add($"{reference.HierarchyPath} (component destroyed)");
                    continue;
                }

                var serializedObject = new SerializedObject(reference.Component);
                var property = serializedObject.FindProperty(reference.PropertyPath);

                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    property.objectReferenceValue = newAsset;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(reference.Component);
                    replacedCount++;
                }
                else
                {
                    failedCount++;
                    failedDetails.Add($"{reference.HierarchyPath}.{reference.PropertyPath}");
                }
            }

            // 결과 메시지
            var message = $"Replaced {replacedCount} reference(s)";
            if (failedCount > 0)
            {
                message += $"\n{failedCount} failed";
                Debug.LogWarning($"[NestedPrefabFinder] Resource replace failures:\n  " + string.Join("\n  ", failedDetails));
            }

            EditorUtility.DisplayDialog("Replace Complete", message, "OK");

            // 목록 새로고침
            RefreshSearch();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null) return;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                _statusLabel.text = "Open a prefab to search";
                return;
            }

            if (_targetFolders.Count == 0)
            {
                _statusLabel.text = "Add target folders to filter";
                return;
            }

            _statusLabel.text = $"Found {_foundPrefabs.Count} prefab(s), {_foundResources.Count} resource(s)";
        }

        #region Preset & Save/Load

        private void SaveLastFolders()
        {
            var json = string.Join("|", _targetFolders);
            EditorPrefs.SetString(PREFS_KEY_LAST_FOLDERS, json);
        }

        private void LoadLastFolders()
        {
            var lastPreset = EditorPrefs.GetString(PREFS_KEY_LAST_PRESET, "");

            // 마지막으로 선택한 프리셋이 있으면 해당 프리셋 불러오기
            if (!string.IsNullOrEmpty(lastPreset) && _presets.ContainsKey(lastPreset))
            {
                _currentPresetName = lastPreset;
                _presetDropdown.SetValueWithoutNotify(lastPreset);
                LoadFoldersFromList(_presets[lastPreset]);
                return;
            }

            // 프리셋이 없으면 마지막 폴더 설정 불러오기
            var json = EditorPrefs.GetString(PREFS_KEY_LAST_FOLDERS, "");
            if (string.IsNullOrEmpty(json)) return;

            var folders = json.Split('|').Where(f => !string.IsNullOrEmpty(f)).ToList();
            LoadFoldersFromList(folders);
        }

        private void LoadFoldersFromList(List<string> folders)
        {
            // 기존 폴더 필드 모두 제거
            ClearAllFolderFields();

            // 저장된 폴더들 로드
            foreach (var folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    AddFolderFieldWithPath(folder);
                }
            }

            UpdateTargetFolders();
        }

        private void ClearAllFolderFields()
        {
            foreach (var row in _folderRows.ToList())
            {
                _folderListContainer.Remove(row);
            }
            _folderRows.Clear();
            _targetFolders.Clear();
        }

        private void AddFolderFieldWithPath(string folderPath)
        {
            var row = new VisualElement();
            row.AddToClassList("folder-row");

            var objectField = new ObjectField();
            objectField.objectType = typeof(DefaultAsset);
            objectField.allowSceneObjects = false;
            objectField.AddToClassList("folder-field");

            // 폴더 에셋 로드 및 설정
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            objectField.value = folderAsset;

            objectField.RegisterValueChangedCallback(evt =>
            {
                UpdateTargetFolders();
            });

            var removeButton = new Button(() =>
            {
                RemoveFolderField(row);
            });
            removeButton.text = "X";
            removeButton.AddToClassList("remove-folder-btn");

            row.Add(objectField);
            row.Add(removeButton);

            _folderListContainer.Add(row);
            _folderRows.Add(row);
        }

        private void SavePresets()
        {
            var presetData = new List<string>();
            foreach (var preset in _presets)
            {
                var folders = string.Join(",", preset.Value);
                presetData.Add($"{preset.Key}:{folders}");
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
                var folders = foldersStr.Split(',').Where(f => !string.IsNullOrEmpty(f)).ToList();

                _presets[name] = folders;
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
                // 새 프리셋 선택 시 폴더 비우기
                ClearAllFolderFields();
                _currentPresetName = "";
                EditorPrefs.SetString(PREFS_KEY_LAST_PRESET, "");
                return;
            }

            if (_presets.TryGetValue(selectedPreset, out var folders))
            {
                _currentPresetName = selectedPreset;
                EditorPrefs.SetString(PREFS_KEY_LAST_PRESET, selectedPreset);
                LoadFoldersFromList(folders);
                RefreshSearch();
            }
        }

        private void OnSavePresetClicked()
        {
            UpdateTargetFolders();

            if (_targetFolders.Count == 0)
            {
                EditorUtility.DisplayDialog("Save Preset", "No folders to save. Add folders first.", "OK");
                return;
            }

            var defaultName = string.IsNullOrEmpty(_currentPresetName) ? "New Preset" : _currentPresetName;
            var presetName = EditorInputDialog.Show("Save Preset", "Enter preset name:", defaultName);

            if (string.IsNullOrEmpty(presetName)) return;

            _presets[presetName] = new List<string>(_targetFolders);
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

        private class NestedPrefabInfo
        {
            public GameObject Instance { get; set; }
            public string SourcePath { get; set; }
            public string HierarchyPath { get; set; }
            public string PrefabName { get; set; }
        }

        private class ResourceAssetInfo
        {
            public Object Asset { get; set; }
            public string AssetPath { get; set; }
            public string AssetName { get; set; }
            public string AssetType { get; set; }
            public System.Type AssetSystemType { get; set; }
            public List<ResourceReference> References { get; set; } = new List<ResourceReference>();
        }

        private class ResourceReference
        {
            public GameObject GameObject { get; set; }
            public string HierarchyPath { get; set; }
            public Component Component { get; set; }
            public string PropertyPath { get; set; }
        }

        private class OverrideCopyResult
        {
            public int CopiedPropertyMods;
            public int FailedPropertyMods;
            public List<string> FailedPropertyModDetails = new List<string>();

            public int CopiedComponents;
            public int FailedComponents;
            public List<string> FailedComponentDetails = new List<string>();

            public int CopiedGameObjects;
            public int FailedGameObjects;
            public List<string> FailedGameObjectDetails = new List<string>();

            public bool HasFailures => FailedPropertyMods > 0 || FailedComponents > 0 || FailedGameObjects > 0;
            public bool HasCopies => CopiedPropertyMods > 0 || CopiedComponents > 0 || CopiedGameObjects > 0;
        }

        #region Override Copy Helpers

        private string GetRelativeHierarchyPath(Transform root, Transform target)
        {
            if (target == root) return "";
            if (target == null) return "";

            var path = new List<string>();
            var current = target;

            while (current != null && current != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }

        private Transform FindTransformByHierarchyPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;

            var parts = path.Split('/');
            var current = root;

            foreach (var part in parts)
            {
                var child = current.Find(part);
                if (child == null) return null;
                current = child;
            }

            return current;
        }

        private bool IsPropertyPathValid(Object target, string propertyPath)
        {
            if (target == null) return false;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyPath);
            return property != null;
        }

        private Object FindCorrespondingObjectInPrefab(GameObject oldPrefabAsset, GameObject newPrefabAsset, Object oldTarget)
        {
            if (oldTarget == null || oldPrefabAsset == null || newPrefabAsset == null)
                return null;

            // GameObject인 경우
            if (oldTarget is GameObject oldGO)
            {
                var relativePath = GetGameObjectRelativePath(oldPrefabAsset, oldGO);
                if (relativePath == null)
                    return null; // target이 oldPrefabAsset의 자식이 아님

                if (string.IsNullOrEmpty(relativePath))
                    return newPrefabAsset; // 루트인 경우

                var newTransform = newPrefabAsset.transform.Find(relativePath);
                return newTransform?.gameObject;
            }

            // Component인 경우
            if (oldTarget is Component oldComp)
            {
                var oldGOPath = GetGameObjectRelativePath(oldPrefabAsset, oldComp.gameObject);
                if (oldGOPath == null)
                    return null; // target이 oldPrefabAsset의 자식이 아님

                Transform newTransform;

                if (string.IsNullOrEmpty(oldGOPath))
                    newTransform = newPrefabAsset.transform;
                else
                    newTransform = newPrefabAsset.transform.Find(oldGOPath);

                if (newTransform == null) return null;

                // 같은 타입의 컴포넌트 찾기
                var compType = oldComp.GetType();
                var oldComps = oldComp.gameObject.GetComponents(compType);
                var compIndex = System.Array.IndexOf(oldComps, oldComp);

                var newComps = newTransform.GetComponents(compType);

                // 정확히 같은 인덱스에 컴포넌트가 있어야만 반환
                // 인덱스가 맞지 않으면 null 반환 (잘못된 컴포넌트에 값을 적용하지 않음)
                if (compIndex >= 0 && compIndex < newComps.Length)
                    return newComps[compIndex];

                return null;
            }

            return null;
        }

        private string GetGameObjectRelativePath(GameObject root, GameObject target)
        {
            if (target == root) return "";
            if (target == null || root == null) return null;

            var path = new List<string>();
            var current = target.transform;

            while (current != null && current.gameObject != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            if (current == null) return null; // target이 root의 자식이 아님
            return string.Join("/", path);
        }

        private void ShowOverrideCopyResult(OverrideCopyResult result)
        {
            if (!result.HasCopies && !result.HasFailures)
            {
                return; // 복사할 오버라이드가 없었음
            }

            var message = "Override Copy Result\n\n" +
                $"Property Overrides: {result.CopiedPropertyMods} copied";
            if (result.FailedPropertyMods > 0)
                message += $", {result.FailedPropertyMods} failed";

            message += $"\nAdded Components: {result.CopiedComponents} copied";
            if (result.FailedComponents > 0)
                message += $", {result.FailedComponents} failed";

            message += $"\nAdded GameObjects: {result.CopiedGameObjects} copied";
            if (result.FailedGameObjects > 0)
                message += $", {result.FailedGameObjects} failed";

            if (result.HasFailures)
            {
                message += "\n\nSome overrides could not be copied due to structure differences. Check Console for details.";

                // Console에 상세 로그
                var details = new List<string>();
                if (result.FailedPropertyModDetails.Count > 0)
                    details.Add("Failed Property Mods:\n  " + string.Join("\n  ", result.FailedPropertyModDetails.Take(10)));
                if (result.FailedComponentDetails.Count > 0)
                    details.Add("Failed Components:\n  " + string.Join("\n  ", result.FailedComponentDetails));
                if (result.FailedGameObjectDetails.Count > 0)
                    details.Add("Failed GameObjects:\n  " + string.Join("\n  ", result.FailedGameObjectDetails));

                Debug.LogWarning("[NestedPrefabFinder] Override copy failures:\n" + string.Join("\n", details));
            }

            EditorUtility.DisplayDialog("Replace Complete", message, "OK");
        }

        #endregion
    }

    /// <summary>
    /// 간단한 텍스트 입력 다이얼로그
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string _inputText = "";
        private string _message = "";
        private bool _confirmed = false;
        private bool _initialized = false;

        public static string Show(string title, string message, string defaultValue = "")
        {
            var dialog = CreateInstance<EditorInputDialog>();
            dialog.titleContent = new GUIContent(title);
            dialog._message = message;
            dialog._inputText = defaultValue;
            dialog.minSize = new Vector2(300, 100);
            dialog.maxSize = new Vector2(300, 100);

            dialog.ShowModalUtility();

            return dialog._confirmed ? dialog._inputText : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(_message);
            EditorGUILayout.Space(5);

            GUI.SetNextControlName("InputField");
            _inputText = EditorGUILayout.TextField(_inputText);

            if (!_initialized)
            {
                EditorGUI.FocusTextInControl("InputField");
                _initialized = true;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _confirmed = false;
                Close();
            }

            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                _confirmed = true;
                Close();
            }

            EditorGUILayout.EndHorizontal();

            // Enter 키로 확인
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                _confirmed = true;
                Close();
            }

            // Escape 키로 취소
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _confirmed = false;
                Close();
            }
        }
    }
}
