using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    public static class BounceLevelCatalogMenu
    {
        [MenuItem("BK Kit/Balance/Open Level Catalog")]
        public static void Open()
        {
            var path=Path.GetFullPath("docs/bounce-level-catalog/index.html");
            if(!File.Exists(path))
            { Debug.LogError("Level catalog is missing. Run python tools/balance/build_level_catalog.py."); return; }
            Application.OpenURL(new Uri(path).AbsoluteUri);
        }
    }
}
