using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BK.Kit.Editor
{
    public static class IntegrationFirstOpen
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntegrationSetup.Boot);
                if (boot == null) return;
                EditorSceneManager.playModeStartScene = boot;
                var scene = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.path) && !scene.isDirty) EditorSceneManager.OpenScene(IntegrationSetup.Boot);
            };
        }
    }
}
