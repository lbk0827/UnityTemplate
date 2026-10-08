using System;
using System.IO;
using UnityEngine;
using BK.Core.Diagnostics;

namespace BK.Save
{
    /// <summary>
    /// Crash-safe file primitives for save slots. A write goes to a temp file and is
    /// renamed over the target while the previous target becomes the backup, so at any
    /// instant either a valid primary or a valid backup exists. A primary that reads
    /// but does not parse is "corrupt": it is moved aside, never deleted, because it
    /// is the only evidence for diagnosing what went wrong.
    /// </summary>
    public static class SaveFile
    {
        public const string BackupExtension = ".bak";
        public const string TempExtension = ".tmp";
        public const string CorruptExtension = ".corrupt-";
        public const string Category = "Save";

        [Serializable] private sealed class VersionProbe { public int version = -1; }

        /// <summary>True when <paramref name="json"/> is valid JSON that carries a non-negative version.</summary>
        public static bool TryReadVersion(string json, out int version)
        {
            version = -1;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var probe = JsonUtility.FromJson<VersionProbe>(json);
                if (probe == null || probe.version < 0)
                    return false;
                version = probe.version;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the first parseable JSON among primary and backup, or null.
        /// <paramref name="primaryCorrupt"/> is set when the primary existed but did not parse.
        /// </summary>
        public static string ReadUsable(string path, out bool primaryCorrupt)
        {
            primaryCorrupt = false;
            if (File.Exists(path))
            {
                var json = TryRead(path);
                if (json != null && TryReadVersion(json, out _))
                    return json;
                primaryCorrupt = true;
            }

            var backup = path + BackupExtension;
            if (File.Exists(backup))
            {
                var json = TryRead(backup);
                if (json != null && TryReadVersion(json, out _))
                {
                    BKLog.Warn(Category, $"primary unusable, recovered from backup: {Path.GetFileName(path)}");
                    return json;
                }
            }

            return null;
        }

        /// <summary>Writes temp then renames, rotating the old primary into the backup.</summary>
        public static void WriteAtomic(string path, string json)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = path + TempExtension;
            File.WriteAllText(temp, json);
            if (File.Exists(path))
                File.Replace(temp, path, path + BackupExtension);
            else
                File.Move(temp, path);
        }

        /// <summary>Moves a corrupt file to <c>name.corrupt-yyyyMMddHHmmssfff</c>. Returns the new path or null.</summary>
        public static string PreserveCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var target = path + CorruptExtension + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Move(path, target);
                return target;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                BKLog.Warn(Category, $"could not preserve corrupt file {path}: {exception.Message}");
                return null;
            }
        }

        private static string TryRead(string path)
        {
            try { return File.ReadAllText(path); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                BKLog.Warn(Category, $"read failed {path}: {exception.Message}");
                return null;
            }
        }
    }
}
