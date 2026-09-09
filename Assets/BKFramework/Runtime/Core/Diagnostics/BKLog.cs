using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BK.Core.Diagnostics
{
    /// <summary>
    /// Category-tagged logging facade. Verbose/info calls compile out of release
    /// builds entirely, so call sites cost nothing when logging is disabled.
    /// </summary>
    public static class BKLog
    {
        public const string Boot = "Boot";
        public const string Assets = "Assets";
        public const string Scene = "Scene";
        public const string UI = "UI";
        public const string Data = "Data";
        public const string Loc = "Loc";

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Verbose(string category, string message)
            => Debug.Log(Format(category, message));

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string category, string message)
            => Debug.Log(Format(category, message));

        public static void Warn(string category, string message)
            => Debug.LogWarning(Format(category, message));

        public static void Error(string category, string message)
            => Debug.LogError(Format(category, message));

        public static void Exception(string category, System.Exception exception)
        {
            Debug.LogError(Format(category, "unhandled exception"));
            Debug.LogException(exception);
        }

        private static string Format(string category, string message)
            => $"[{category}] {message}";
    }
}
