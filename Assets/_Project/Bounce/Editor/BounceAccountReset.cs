using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    [InitializeOnLoad]
    public static class BounceAccountReset
    {
        private const string Pending = "Bounce.AccountReset.Pending";
        static BounceAccountReset()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += CompletePending;
            };
            EditorApplication.delayCall += CompletePending;
        }

        [MenuItem("BK/Integration/Reset All Bounce Game Data (New Account)")]
        public static void RequestReset()
        {
            if (!EditorUtility.DisplayDialog("Bounce 신규 계정 초기화",
                "Play를 종료하고 진행도, 재화, 부스터, 구매·광고 기록, 프로필, 가이드, 연승, 오퍼, 설정을 초기화합니다.\n기존 로컬 데이터는 별도 백업합니다. 다음 Play는 Bootstrap에서 시작하세요.",
                "백업 후 초기화", "취소")) return;
            UnityEditor.SessionState.SetBool(Pending, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else EditorApplication.delayCall += CompletePending;
        }

        [MenuItem("BK/Integration/Reset All Bounce Game Data (New Account)", true)]
        private static bool CanReset() => !EditorApplication.isCompiling && !UnityEditor.SessionState.GetBool(Pending, false)
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(IntegratedProjectScope.TestSaveDirectoryVariable));

        private static void CompletePending()
        {
            if (!UnityEditor.SessionState.GetBool(Pending, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            UnityEditor.SessionState.SetBool(Pending, false);
            try
            {
                string backup = ArchiveLocalAccount(Application.persistentDataPath);
                Debug.Log("Bounce 신규 계정 초기화 완료. 백업: " + backup + "\n다음 Play를 Bootstrap에서 시작하세요.");
                // Also clear static Fake Store/session caches when Enter Play Mode disables domain reload.
                EditorUtility.RequestScriptReload();
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>Archives all slots (including recovery files) and this app's IAP transaction cache.
        /// Caller must stop the app first so disposal cannot write old state back.</summary>
        public static string ArchiveLocalAccount(string persistentRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before resetting a profile.");
            string root = Path.GetFullPath(persistentRoot);
            string backup = Path.Combine(root, "Bounce", "EditorBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            var paths = new[] { Path.Combine(root, "Bounce", "Profiles", "local"), Path.Combine(root, "Unity", "UnityPurchasing") };
            // Validate every source and destination before moving anything. Do not follow junctions.
            ValidatePath(root, backup);
            foreach (string source in paths) ValidatePath(root, source);
            var moved = new List<(string source, string destination)>();
            Directory.CreateDirectory(backup);
            try
            {
                foreach (string source in paths)
                {
                    if (!Directory.Exists(source)) continue;
                    string destination = Path.Combine(backup, Path.GetFileName(source));
                    Directory.Move(source, destination);
                    moved.Add((source, destination));
                }
                File.WriteAllText(Path.Combine(backup, "restore.txt"),
                    "Stop Unity Play before restoring. Move local to Bounce/Profiles/local and UnityPurchasing to Unity/UnityPurchasing under:\n" + root);
            }
            catch
            {
                for (int i = moved.Count - 1; i >= 0; i--)
                    Directory.Move(moved[i].destination, moved[i].source);
                throw;
            }
            return backup;
        }

        private static void ValidatePath(string root, string path)
        {
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Reset path escaped the application folder.");
            for (var dir = new DirectoryInfo(full); dir != null; dir = dir.Parent)
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reset does not follow linked folders: " + dir.FullName);
        }
    }
}
