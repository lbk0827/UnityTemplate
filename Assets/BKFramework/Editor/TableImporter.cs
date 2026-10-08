using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BK.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace BK.Editor
{
    /// <summary>
    /// Fills <see cref="TableAsset{TKey,TRow}"/> assets from JSON files written by
    /// tools/tables/export_tables.py ({"_rows":[...]}), creating the asset when missing,
    /// registering it as an Addressable and appending it to a TableCatalog.
    /// </summary>
    public static class TableImporter
    {
        public const string GroupName = "BK Tables";
        public const string AddressPrefix = "Data/Tables/";
        public const string ProjectJsonFolder = "Assets/_Project/Data/Tables/Generated";
        public const string ProjectAssetFolder = "Assets/_Project/Data/Tables";
        public const string ProjectCatalog = "Assets/_Project/Data/TableCatalog.asset";

        [MenuItem("BK/Data/Import Tables")]
        public static void ImportProjectTables()
        {
            var imported = ImportFolder(ProjectJsonFolder, ProjectAssetFolder, registerAddressables: true, catalogPath: ProjectCatalog);
            Debug.Log($"BK_TABLES_OK: {imported.Count} tables imported");
        }

        public static IReadOnlyList<string> ImportFolder(string jsonFolder, string assetFolder, bool registerAddressables = true, string catalogPath = null)
        {
            if (!Directory.Exists(jsonFolder))
                throw new DirectoryNotFoundException(jsonFolder);
            Directory.CreateDirectory(assetFolder);

            var tableTypes = new Dictionary<string, Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<TableAsset>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;
                tableTypes[type.Name] = type;
            }

            var imported = new List<string>();
            foreach (var jsonPath in Directory.GetFiles(jsonFolder, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(jsonPath);
                if (!tableTypes.TryGetValue(name, out var type) && !tableTypes.TryGetValue(name + "Table", out type))
                {
                    Debug.LogWarning($"[Data] no TableAsset type named '{name}' or '{name}Table' for {jsonPath}; skipped");
                    continue;
                }

                var assetPath = Path.Combine(assetFolder, type.Name + ".asset").Replace('\\', '/');
                var fresh = (TableAsset)ScriptableObject.CreateInstance(type);
                try
                {
                    FillRows(fresh, File.ReadAllText(jsonPath));
                    var existing = AssetDatabase.LoadAssetAtPath<TableAsset>(assetPath);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(fresh, assetPath);
                        fresh = null; // now owned by the asset database
                    }
                    else
                    {
                        EditorUtility.CopySerialized(fresh, existing);
                        existing.name = type.Name;
                        existing.BuildIndex(); // a loaded instance may hold an index over the old rows
                        EditorUtility.SetDirty(existing);
                    }
                }
                finally
                {
                    if (fresh != null)
                        UnityEngine.Object.DestroyImmediate(fresh);
                }

                imported.Add(assetPath);
            }

            AssetDatabase.SaveAssets();

            if (registerAddressables && imported.Count > 0)
                Register(imported);
            if (!string.IsNullOrEmpty(catalogPath) && imported.Count > 0)
                AppendToCatalog(catalogPath, imported);

            AssetDatabase.SaveAssets();
            return imported;
        }

        private static void FillRows(TableAsset table, string json)
        {
            var genericBase = table.GetType();
            while (genericBase != null && !(genericBase.IsGenericType && genericBase.GetGenericTypeDefinition() == typeof(TableAsset<,>)))
                genericBase = genericBase.BaseType;
            if (genericBase == null)
                throw new InvalidOperationException($"{table.GetType().Name} does not derive from TableAsset<,>");

            var rowType = genericBase.GetGenericArguments()[1];
            var wrapperType = typeof(RowsWrapper<>).MakeGenericType(rowType);
            var wrapper = JsonUtility.FromJson(json, wrapperType);
            var rows = wrapperType.GetField("_rows").GetValue(wrapper);

            var field = genericBase.GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(table, rows);
        }

        [Serializable]
        private sealed class RowsWrapper<TRow>
        {
            public List<TRow> _rows = new();
        }

        private static void Register(IReadOnlyList<string> assetPaths)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                Debug.LogWarning("[Data] Addressables settings missing; tables were not registered");
                return;
            }

            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            foreach (var path in assetPaths)
            {
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
                entry.address = AddressPrefix + Path.GetFileNameWithoutExtension(path);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        }

        private static void AppendToCatalog(string catalogPath, IReadOnlyList<string> assetPaths)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TableCatalog>(catalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[Data] TableCatalog not found at {catalogPath}; addresses were not appended");
                return;
            }

            var serialized = new SerializedObject(catalog);
            var addresses = serialized.FindProperty("_addresses");
            var known = new HashSet<string>();
            for (var i = 0; i < addresses.arraySize; i++)
                known.Add(addresses.GetArrayElementAtIndex(i).stringValue);

            foreach (var path in assetPaths)
            {
                var address = AddressPrefix + Path.GetFileNameWithoutExtension(path);
                if (!known.Add(address))
                    continue;
                addresses.InsertArrayElementAtIndex(addresses.arraySize);
                addresses.GetArrayElementAtIndex(addresses.arraySize - 1).stringValue = address;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
