using BK.Kit;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    public sealed class KitWorkbench : EditorWindow
    {
        private int level=1,gold=1000,boosters=10;
        private string feedback;
        [MenuItem("BK Kit/RND Workbench")]
        public static void Open() => GetWindow<KitWorkbench>("BK Kit RND");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Local RND controls",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Open Migrated Bootstrap and enter Play mode. These controls change only this kit's local profile.",MessageType.Info);
            level=EditorGUILayout.IntField("Stage (1 - 1000000)",level);
            gold=EditorGUILayout.IntField("Gold",gold);
            boosters=EditorGUILayout.IntField("Each booster",boosters);
            var app=KitApp.Instance;
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying || app==null || app.IsLoading))
            {
                if(GUILayout.Button("Apply and play stage"))
                {
                    bool saved=app.TrySetSandboxProgress(level,gold,boosters);
                    feedback=saved?"Profile saved; loading stage.":"Could not save. Check values and save access.";
                    if(saved)app.Play(level);
                }
                if(GUILayout.Button("Return to lobby"))app.GoToLobby();
                if(GUILayout.Button("Retry pending save"))feedback=app.RetryPendingSave()?"Saved.":"Still waiting for save access.";
            }
            if(!string.IsNullOrEmpty(feedback))EditorGUILayout.HelpBox(feedback,MessageType.None);
            if(app!=null)EditorGUILayout.LabelField("State",app.Session.State+" / stage "+app.Session.Level);
            EditorGUILayout.Space();
            if(GUILayout.Button("Open Migrated Bootstrap"))IntegrationSetup.Open();
        }
        private void OnInspectorUpdate()=>Repaint();
    }
}
