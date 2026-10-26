using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    public static class BounceReleaseGenerator
    {
        public const string DataPath="Assets/_Project/Bounce/Content/bounce-release-levels.json";
        [MenuItem("BK Kit/Balance/Apply Release Campaign")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            var library=JsonUtility.FromJson<BounceModule.Levels>(asset.text);
            for(int i=0;i<library.levels.Length;i++)
            {
                var errors=BounceLevelLayout.Validate(library.levels[i],true);
                foreach(var g in library.levels[i].gimmicks)
                    if(!BounceReleasePolicy.IsAllowed(g.type,i+1))errors.Add("Gimmick used before unlock: "+g.type);
                if(errors.Count>0)throw new InvalidOperationException("Stage "+(i+1)+": "+string.Join(" / ",errors));
            }
            var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>(BounceLearningGenerator.PresentationPath);
            presentation.levels=asset;EditorUtility.SetDirty(presentation);AssetDatabase.SaveAssets();
            Debug.Log("Release campaign applied: "+library.levels.Length+" stages, no looping.");
        }
    }
}
