// Ported from the sf RootBox editor tools (namespace RootBoxEditor), 2026-10-08.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BK.Editor.Tools
{
    public enum MemberType
    {
        Field,
        Property
    }

    public class FieldCompatibilityInfo
    {
        public string FieldName { get; set; }
        public Type SourceType { get; set; }
        public Type TargetType { get; set; }
        public bool IsCompatible { get; set; }
        public MemberType MemberKind { get; set; }
        public string IncompatibilityReason { get; set; }

        public string SourceTypeName => SourceType?.Name ?? "N/A";
        public string TargetTypeName => TargetType?.Name ?? "N/A";
    }

    public class FieldCompatibilityAnalyzer
    {
        public List<FieldCompatibilityInfo> Analyze(Type sourceType, Type targetType)
        {
            var results = new List<FieldCompatibilityInfo>();

            if (sourceType == null || targetType == null)
                return results;

            // Get all serializable fields from source
            var sourceFields = GetSerializableFields(sourceType);
            var targetFields = GetSerializableFields(targetType);

            // Build target field lookup dictionary
            var targetFieldDict = targetFields.ToDictionary(f => f.Name, f => f);

            foreach (var sourceField in sourceFields)
            {
                var info = new FieldCompatibilityInfo
                {
                    FieldName = sourceField.Name,
                    SourceType = sourceField.FieldType,
                    MemberKind = MemberType.Field
                };

                // Check if target has field with same name
                if (targetFieldDict.TryGetValue(sourceField.Name, out var targetField))
                {
                    info.TargetType = targetField.FieldType;

                    // Check type compatibility
                    if (AreTypesCompatible(sourceField.FieldType, targetField.FieldType))
                    {
                        info.IsCompatible = true;
                    }
                    else
                    {
                        info.IsCompatible = false;
                        info.IncompatibilityReason =
                            $"Type mismatch: {sourceField.FieldType.Name} -> {targetField.FieldType.Name}";
                    }
                }
                else
                {
                    info.IsCompatible = false;
                    info.TargetType = null;
                    info.IncompatibilityReason = "Field not found in target type";
                }

                results.Add(info);
            }

            return results;
        }

        public List<FieldCompatibilityInfo> GetCompatibleFields(Type sourceType, Type targetType)
        {
            return Analyze(sourceType, targetType).Where(f => f.IsCompatible).ToList();
        }

        public List<FieldCompatibilityInfo> GetIncompatibleFields(Type sourceType, Type targetType)
        {
            return Analyze(sourceType, targetType).Where(f => !f.IsCompatible).ToList();
        }

        private List<FieldInfo> GetSerializableFields(Type type)
        {
            var fields = new List<FieldInfo>();
            var currentType = type;

            while (currentType != null &&
                   currentType != typeof(MonoBehaviour) &&
                   currentType != typeof(Component) &&
                   currentType != typeof(Behaviour) &&
                   currentType != typeof(UnityEngine.Object))
            {
                var typeFields = currentType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                foreach (var field in typeFields)
                {
                    // Skip fields that are already added (from derived classes)
                    if (fields.Any(f => f.Name == field.Name))
                        continue;

                    // Public fields are serialized by default (unless marked with NonSerialized)
                    if (field.IsPublic && !field.IsDefined(typeof(NonSerializedAttribute), false))
                    {
                        if (IsSerializableFieldType(field.FieldType))
                        {
                            fields.Add(field);
                        }
                    }
                    // Private/protected fields with [SerializeField]
                    else if (!field.IsPublic && field.IsDefined(typeof(SerializeField), false))
                    {
                        if (IsSerializableFieldType(field.FieldType))
                        {
                            fields.Add(field);
                        }
                    }
                }

                currentType = currentType.BaseType;
            }

            return fields;
        }

        private bool IsSerializableFieldType(Type type)
        {
            // Primitive types
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return true;

            // Unity built-in types
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                type == typeof(Vector2Int) || type == typeof(Vector3Int) ||
                type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Color32) ||
                type == typeof(Rect) || type == typeof(RectInt) ||
                type == typeof(Bounds) || type == typeof(BoundsInt) ||
                type == typeof(AnimationCurve) || type == typeof(Gradient) ||
                type == typeof(LayerMask))
                return true;

            // Unity Object references
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;

            // Enums
            if (type.IsEnum)
                return true;

            // Arrays of serializable types
            if (type.IsArray)
                return IsSerializableFieldType(type.GetElementType());

            // Generic Lists
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return IsSerializableFieldType(type.GetGenericArguments()[0]);

            // Serializable classes/structs
            if (type.IsClass || type.IsValueType)
            {
                if (type.IsDefined(typeof(SerializableAttribute), false))
                    return true;
            }

            return false;
        }

        private bool AreTypesCompatible(Type sourceType, Type targetType)
        {
            // Null check
            if (sourceType == null || targetType == null)
                return false;

            // Exact match
            if (sourceType == targetType)
                return true;

            // Inheritance (target can accept source)
            if (targetType.IsAssignableFrom(sourceType))
                return true;

            // Handle nullable types
            var sourceUnderlying = Nullable.GetUnderlyingType(sourceType);
            var targetUnderlying = Nullable.GetUnderlyingType(targetType);

            if (sourceUnderlying != null && targetUnderlying != null)
            {
                return sourceUnderlying == targetUnderlying;
            }

            // Handle generic lists
            if (sourceType.IsGenericType && targetType.IsGenericType)
            {
                if (sourceType.GetGenericTypeDefinition() == typeof(List<>) &&
                    targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var sourceArg = sourceType.GetGenericArguments()[0];
                    var targetArg = targetType.GetGenericArguments()[0];
                    return AreTypesCompatible(sourceArg, targetArg);
                }
            }

            // Handle arrays
            if (sourceType.IsArray && targetType.IsArray)
            {
                return AreTypesCompatible(
                    sourceType.GetElementType(),
                    targetType.GetElementType());
            }

            return false;
        }
    }
}
