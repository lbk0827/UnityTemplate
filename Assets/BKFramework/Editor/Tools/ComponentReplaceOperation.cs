// Ported from the sf RootBox editor tools (namespace RootBoxEditor), 2026-10-08.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BK.Editor.Tools
{
    public class PrefabScanResult
    {
        public string PrefabPath { get; set; }
        public string PrefabName { get; set; }
        public int ComponentCount { get; set; }
        public List<string> GameObjectPaths { get; set; } = new List<string>();
        public bool IsSelected { get; set; } = true;
    }

    public class ComponentReplaceResult
    {
        public int TotalPrefabs { get; set; }
        public int SuccessfulPrefabs { get; set; }
        public int TotalComponentsReplaced { get; set; }
        public int TotalFieldsCopied { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class PrefabReplaceResult
    {
        public string PrefabPath { get; set; }
        public bool Success { get; set; }
        public int ComponentsReplaced { get; set; }
        public int FieldsCopied { get; set; }
        public string Error { get; set; }
    }

    public class ReplaceProgressInfo
    {
        public float Progress { get; set; }
        public string CurrentPrefab { get; set; }
        public string Status { get; set; }
    }

    public class ComponentReplaceOperation
    {
        private readonly Type _sourceType;
        private readonly Type _targetType;
        private readonly List<FieldCompatibilityInfo> _compatibleFields;

        public ComponentReplaceOperation(
            Type sourceType,
            Type targetType,
            List<FieldCompatibilityInfo> compatibleFields)
        {
            _sourceType = sourceType;
            _targetType = targetType;
            _compatibleFields = compatibleFields.Where(f => f.IsCompatible).ToList();
        }

        public ComponentReplaceResult Execute(
            List<PrefabScanResult> prefabs,
            IProgress<ReplaceProgressInfo> progress = null)
        {
            var result = new ComponentReplaceResult
            {
                TotalPrefabs = prefabs.Count(p => p.IsSelected)
            };

            var selectedPrefabs = prefabs.Where(p => p.IsSelected).ToList();

            for (int i = 0; i < selectedPrefabs.Count; i++)
            {
                var prefab = selectedPrefabs[i];

                try
                {
                    // Check if prefab is in read-only package
                    if (IsInReadOnlyPackage(prefab.PrefabPath))
                    {
                        result.Warnings.Add($"Skipped read-only prefab: {prefab.PrefabPath}");
                        continue;
                    }

                    var prefabResult = ReplaceSinglePrefab(prefab.PrefabPath);

                    if (prefabResult.Success)
                    {
                        result.SuccessfulPrefabs++;
                        result.TotalComponentsReplaced += prefabResult.ComponentsReplaced;
                        result.TotalFieldsCopied += prefabResult.FieldsCopied;
                    }
                    else
                    {
                        result.Errors.Add($"{prefab.PrefabPath}: {prefabResult.Error}");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{prefab.PrefabPath}: {ex.Message}");
                    Debug.LogException(ex);
                }

                progress?.Report(new ReplaceProgressInfo
                {
                    Progress = (float)(i + 1) / selectedPrefabs.Count,
                    CurrentPrefab = prefab.PrefabName,
                    Status = $"Processing {i + 1}/{selectedPrefabs.Count}"
                });
            }

            return result;
        }

        private PrefabReplaceResult ReplaceSinglePrefab(string prefabPath)
        {
            var result = new PrefabReplaceResult { PrefabPath = prefabPath };

            // Load prefab contents for editing
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                // Find all source components recursively
                var sourceComponents = prefabContents.GetComponentsInChildren(_sourceType, true);

                foreach (var sourceComponent in sourceComponents)
                {
                    int fieldsCopied = ReplaceComponent(sourceComponent.gameObject, sourceComponent);
                    result.ComponentsReplaced++;
                    result.FieldsCopied += fieldsCopied;
                }

                // Save changes
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            finally
            {
                // Always unload prefab contents
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            return result;
        }

        private int ReplaceComponent(GameObject go, Component sourceComponent)
        {
            int fieldsCopied = 0;

            // 1. Cache source component field values before destroying
            var sourceSerializedObj = new SerializedObject(sourceComponent);
            var cachedValues = CacheFieldValues(sourceSerializedObj);

            // 2. Get component index (for ordering)
            var components = go.GetComponents<Component>();
            int componentIndex = Array.IndexOf(components, sourceComponent);

            // 3. Remove source component FIRST (to avoid DisallowMultipleComponent conflict)
            UnityEngine.Object.DestroyImmediate(sourceComponent, true);

            // 4. Add new component
            var newComponent = go.AddComponent(_targetType);
            var targetSerializedObj = new SerializedObject(newComponent);

            // 5. Apply cached field values to new component
            fieldsCopied = ApplyCachedValues(targetSerializedObj, cachedValues);

            // 6. Reorder component to original position (if possible)
            ReorderComponent(go, newComponent, componentIndex);

            return fieldsCopied;
        }

        private Dictionary<string, object> CacheFieldValues(SerializedObject source)
        {
            var cache = new Dictionary<string, object>();
            source.Update();

            foreach (var fieldInfo in _compatibleFields)
            {
                var sourceProp = source.FindProperty(fieldInfo.FieldName);
                if (sourceProp != null)
                {
                    var value = GetPropertyValue(sourceProp);
                    if (value != null)
                    {
                        cache[fieldInfo.FieldName] = value;
                    }
                }
            }

            return cache;
        }

        private object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    return prop.colorValue;
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue;
                case SerializedPropertyType.LayerMask:
                    return prop.intValue;
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value;
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value;
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value;
                case SerializedPropertyType.Rect:
                    return prop.rectValue;
                case SerializedPropertyType.AnimationCurve:
                    return new AnimationCurve(prop.animationCurveValue.keys);
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue;
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue;
                case SerializedPropertyType.Vector2Int:
                    return prop.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return prop.vector3IntValue;
                case SerializedPropertyType.RectInt:
                    return prop.rectIntValue;
                case SerializedPropertyType.BoundsInt:
                    return prop.boundsIntValue;
                case SerializedPropertyType.Generic:
                    return CacheGenericProperty(prop);
                default:
                    return null;
            }
        }

        private object CacheGenericProperty(SerializedProperty prop)
        {
            if (prop.isArray)
            {
                var list = new List<object>();
                for (int i = 0; i < prop.arraySize; i++)
                {
                    var element = prop.GetArrayElementAtIndex(i);
                    list.Add(GetPropertyValue(element));
                }
                return list;
            }
            else
            {
                // For complex nested types, cache as dictionary
                var dict = new Dictionary<string, object>();
                var iter = prop.Copy();
                if (iter.NextVisible(true))
                {
                    int depth = iter.depth;
                    do
                    {
                        if (iter.depth < depth) break;
                        dict[iter.name] = GetPropertyValue(iter);
                    } while (iter.NextVisible(false) && iter.depth >= depth);
                }
                return dict;
            }
        }

        private int ApplyCachedValues(SerializedObject target, Dictionary<string, object> cachedValues)
        {
            int copiedCount = 0;
            target.Update();

            foreach (var kvp in cachedValues)
            {
                var targetProp = target.FindProperty(kvp.Key);
                if (targetProp != null && kvp.Value != null)
                {
                    if (SetPropertyValue(targetProp, kvp.Value))
                    {
                        copiedCount++;
                    }
                }
            }

            target.ApplyModifiedPropertiesWithoutUndo();
            return copiedCount;
        }

        private bool SetPropertyValue(SerializedProperty prop, object value)
        {
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = (int)value;
                        break;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = (bool)value;
                        break;
                    case SerializedPropertyType.Float:
                        prop.floatValue = (float)value;
                        break;
                    case SerializedPropertyType.String:
                        prop.stringValue = (string)value;
                        break;
                    case SerializedPropertyType.Color:
                        prop.colorValue = (Color)value;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        prop.objectReferenceValue = value as UnityEngine.Object;
                        break;
                    case SerializedPropertyType.LayerMask:
                        prop.intValue = (int)value;
                        break;
                    case SerializedPropertyType.Enum:
                        prop.enumValueIndex = (int)value;
                        break;
                    case SerializedPropertyType.Vector2:
                        prop.vector2Value = (Vector2)value;
                        break;
                    case SerializedPropertyType.Vector3:
                        prop.vector3Value = (Vector3)value;
                        break;
                    case SerializedPropertyType.Vector4:
                        prop.vector4Value = (Vector4)value;
                        break;
                    case SerializedPropertyType.Rect:
                        prop.rectValue = (Rect)value;
                        break;
                    case SerializedPropertyType.AnimationCurve:
                        prop.animationCurveValue = (AnimationCurve)value;
                        break;
                    case SerializedPropertyType.Bounds:
                        prop.boundsValue = (Bounds)value;
                        break;
                    case SerializedPropertyType.Quaternion:
                        prop.quaternionValue = (Quaternion)value;
                        break;
                    case SerializedPropertyType.Vector2Int:
                        prop.vector2IntValue = (Vector2Int)value;
                        break;
                    case SerializedPropertyType.Vector3Int:
                        prop.vector3IntValue = (Vector3Int)value;
                        break;
                    case SerializedPropertyType.RectInt:
                        prop.rectIntValue = (RectInt)value;
                        break;
                    case SerializedPropertyType.BoundsInt:
                        prop.boundsIntValue = (BoundsInt)value;
                        break;
                    case SerializedPropertyType.Generic:
                        ApplyGenericProperty(prop, value);
                        break;
                    default:
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to set property {prop.name}: {ex.Message}");
                return false;
            }
        }

        private void ApplyGenericProperty(SerializedProperty prop, object value)
        {
            if (prop.isArray && value is List<object> list)
            {
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    var element = prop.GetArrayElementAtIndex(i);
                    SetPropertyValue(element, list[i]);
                }
            }
            else if (value is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    var childProp = prop.FindPropertyRelative(kvp.Key);
                    if (childProp != null)
                    {
                        SetPropertyValue(childProp, kvp.Value);
                    }
                }
            }
        }

        private void ReorderComponent(GameObject go, Component component, int targetIndex)
        {
            if (targetIndex < 0) return;

            var components = go.GetComponents<Component>();
            int currentIndex = Array.IndexOf(components, component);

            if (currentIndex < 0) return;

            // Move component up to reach target position
            while (currentIndex > targetIndex)
            {
                ComponentUtility.MoveComponentUp(component);
                currentIndex--;
            }
        }

        private bool IsInReadOnlyPackage(string assetPath)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            return packageInfo != null &&
                   (packageInfo.source == UnityEditor.PackageManager.PackageSource.BuiltIn ||
                    packageInfo.source == UnityEditor.PackageManager.PackageSource.Registry);
        }

        #region Static Utility Methods

        public static List<PrefabScanResult> ScanPrefabs(
            string folderPath,
            Type sourceType,
            bool includeNested,
            IProgress<float> progress = null)
        {
            var results = new List<PrefabScanResult>();

            if (string.IsNullOrEmpty(folderPath) || sourceType == null)
                return results;

            // Find all prefabs in folder
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                // Load prefab asset (not contents - faster for scanning)
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                // Check for source component
                Component[] components;
                if (includeNested)
                {
                    components = prefab.GetComponentsInChildren(sourceType, true);
                }
                else
                {
                    // Only direct children, not nested prefabs
                    components = GetComponentsExcludingNestedPrefabs(prefab, sourceType);
                }

                if (components.Length > 0)
                {
                    var scanResult = new PrefabScanResult
                    {
                        PrefabPath = prefabPath,
                        PrefabName = prefab.name,
                        ComponentCount = components.Length,
                        GameObjectPaths = components.Select(c => GetGameObjectPath(c.gameObject, prefab)).ToList(),
                        IsSelected = true
                    };
                    results.Add(scanResult);
                }

                progress?.Report((float)(i + 1) / prefabGuids.Length);
            }

            return results;
        }

        private static Component[] GetComponentsExcludingNestedPrefabs(GameObject root, Type componentType)
        {
            var results = new List<Component>();
            GetComponentsExcludingNestedPrefabsRecursive(root, componentType, results, isRoot: true);
            return results.ToArray();
        }

        private static void GetComponentsExcludingNestedPrefabsRecursive(
            GameObject go,
            Type componentType,
            List<Component> results,
            bool isRoot)
        {
            // Skip if this is a nested prefab instance (but not the root)
            if (!isRoot && PrefabUtility.IsAnyPrefabInstanceRoot(go))
            {
                return;
            }

            // Add components on this object
            results.AddRange(go.GetComponents(componentType));

            // Recurse to children
            foreach (Transform child in go.transform)
            {
                GetComponentsExcludingNestedPrefabsRecursive(child.gameObject, componentType, results, isRoot: false);
            }
        }

        private static string GetGameObjectPath(GameObject go, GameObject root)
        {
            if (go == root) return root.name;

            var path = new List<string>();
            Transform current = go.transform;

            while (current != null && current.gameObject != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return root.name + "/" + string.Join("/", path);
        }

        #endregion
    }
}
