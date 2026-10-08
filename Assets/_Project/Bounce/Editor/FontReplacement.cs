using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BK.Kit.Editor
{
    public static class FontReplacement
    {
        [MenuItem("BK/Integration/Rebuild OFL Fonts")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var paths = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { IntegrationSetup.Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("KenneyFutureNarrow") || p.Contains("LilitaOne SDF") || p.Contains("Jua SDF")).ToArray();
            if (paths.Length != 2) throw new InvalidOperationException("Expected the two Bounce font assets.");
            TMP_FontAsset primary = null, fallback = null;
            foreach (var path in paths)
            {
                bool korean = path.Contains("FallBack");
                string family = korean ? "Jua" : "LilitaOne";
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/ThirdParty/" + family + "/" + family + "-Regular.ttf");
                if (source == null) throw new InvalidOperationException("Missing OFL font " + family);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                var generated = TMP_FontAsset.CreateFontAsset(source, 90, 12, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                string characters = korean ? "한글 주아체 라이선스 출처 닫기 설정" : new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray());
                if (!generated.TryAddCharacters(characters, out string missing))
                    throw new InvalidOperationException(family + " missing characters: " + missing);
                var atlas = font.atlasTextures[0];
                var material = font.material;
                var generatedAtlas = generated.atlasTextures[0];
                var generatedMaterial = generated.material;
                // Preserve GUIDs and local IDs used by all authored prefabs and outline presets.
                EditorUtility.CopySerialized(generatedAtlas, atlas);
                material.CopyPropertiesFromMaterial(generatedMaterial);
                material.mainTexture = atlas;
                EditorUtility.CopySerialized(generated, font);
                font.atlasTextures = new[] { atlas };
                font.material = material;
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                font.name = family + (korean ? " SDF_FallBack" : " SDF");
                atlas.name = font.name + " Atlas";
                material.name = font.name + " Material";
                foreach (string matPath in AssetDatabase.FindAssets("t:Material", new[] { IntegrationSetup.Root }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var preset = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (preset.mainTexture != atlas) continue;
                    foreach (var property in new[] { "_GradientScale", "_TextureWidth", "_TextureHeight", "_WeightNormal", "_WeightBold" })
                        if (material.HasProperty(property)) preset.SetFloat(property, material.GetFloat(property));
                    preset.name = preset.name.Replace("KenneyFutureNarrow", family);
                    EditorUtility.SetDirty(preset);
                }
                EditorUtility.SetDirty(atlas); EditorUtility.SetDirty(material); EditorUtility.SetDirty(font);
                if (korean) fallback = font; else primary = font;
                UnityEngine.Object.DestroyImmediate(generatedMaterial);
                UnityEngine.Object.DestroyImmediate(generatedAtlas);
                UnityEngine.Object.DestroyImmediate(generated);
            }
            primary.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(primary);
            AssetDatabase.SaveAssets();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(IntegrationSetup.Root + "/") && Path.GetFileName(p).Contains("KenneyFutureNarrow") && !p.EndsWith(".ttf")).ToArray())
            {
                var name = Path.GetFileName(path).Replace("KenneyFutureNarrow", path.Contains("FallBack") ? "Jua" : "LilitaOne");
                string error = AssetDatabase.MoveAsset(path, Path.GetDirectoryName(path).Replace('\\', '/') + "/" + name);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BK_OFL_FONTS_OK");
        }
    }
}
