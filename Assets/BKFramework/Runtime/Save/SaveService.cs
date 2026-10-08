using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BK.Core.Diagnostics;

namespace BK.Save
{
    /// <inheritdoc cref="ISaveService"/>
    public sealed class SaveService : ISaveService, IDisposable
    {
        private readonly string _directory;
        private readonly Dictionary<Type, SaveData> _slots = new();
        private readonly object _gate = new();
        private SaveFlushDriver _driver;
        private bool _disposed;

        /// <param name="directory">Folder for slot files. Tests pass a temp folder.</param>
        /// <param name="flushIntervalSeconds">Periodic flush cadence while playing. 0 disables the driver.</param>
        public SaveService(string directory, float flushIntervalSeconds = 5f)
        {
            _directory = directory;
            FlushIntervalSeconds = flushIntervalSeconds;
        }

        /// <summary>Default production folder.</summary>
        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "Save");

        public float FlushIntervalSeconds { get; }

        public bool HasUnsavedChanges
        {
            get
            {
                lock (_gate)
                {
                    foreach (var slot in _slots.Values)
                        if (slot.IsDirty) return true;
                    return false;
                }
            }
        }

        public T Get<T>() where T : SaveData, new()
        {
            lock (_gate)
            {
                if (_slots.TryGetValue(typeof(T), out var existing))
                    return (T)existing;

                var loaded = Load<T>();
                _slots.Add(typeof(T), loaded);
                EnsureDriver();
                return loaded;
            }
        }

        public bool Flush()
        {
            var allOk = true;
            lock (_gate)
            {
                foreach (var pair in _slots)
                {
                    var slot = pair.Value;
                    if (!slot.IsDirty) continue;
                    try
                    {
                        SaveFile.WriteAtomic(PathFor(pair.Key), JsonUtility.ToJson(slot, Application.isEditor));
                        slot.ClearDirty();
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        // Stays dirty: the next flush retries. Loss is delayed, not silent.
                        BKLog.Error(SaveFile.Category, $"save failed {pair.Key.Name}: {exception.Message}");
                        allOk = false;
                    }
                }
            }
            return allOk;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Flush();
            if (_driver != null)
                UnityEngine.Object.Destroy(_driver.gameObject);
        }

        private string PathFor(Type type) => Path.Combine(_directory, type.Name + ".json");

        private T Load<T>() where T : SaveData, new()
        {
            var path = PathFor(typeof(T));
            var json = SaveFile.ReadUsable(path, out var primaryCorrupt);
            if (primaryCorrupt)
                BKLog.Error(SaveFile.Category, $"corrupt save {typeof(T).Name}, preserved at {SaveFile.PreserveCorrupt(path) ?? "(failed)"}");

            var data = new T();
            if (json != null && TryApply(data, json, out var migrated))
            {
                if (migrated) data.MarkDirty();
                return data;
            }

            if (json != null)
            {
                // Parsed but failed validation or population: same policy as corrupt.
                BKLog.Error(SaveFile.Category, $"invalid save {typeof(T).Name}, preserved at {SaveFile.PreserveCorrupt(path) ?? "(failed)"}");
                data = new T();
            }

            data.version = data.CurrentVersion;
            data.OnCreate();
            data.MarkDirty();
            return data;
        }

        private static bool TryApply<T>(T data, string json, out bool migrated) where T : SaveData
        {
            migrated = false;
            if (!SaveFile.TryReadVersion(json, out var fileVersion))
                return false;
            try { JsonUtility.FromJsonOverwrite(json, data); }
            catch (ArgumentException) { return false; }

            if (fileVersion < data.CurrentVersion)
            {
                data.OnMigrate(fileVersion);
                data.version = data.CurrentVersion;
                migrated = true;
            }
            return data.Validate();
        }

        private void EnsureDriver()
        {
            if (_driver != null || FlushIntervalSeconds <= 0f || !Application.isPlaying)
                return;
            _driver = SaveFlushDriver.Create(this);
        }
    }
}
