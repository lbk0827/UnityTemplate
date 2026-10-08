using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BK.Kit.Editor
{
    public static class FontReplacement
    {
        [MenuItem("BK/Integration/Rebuild CC0 Fonts")]
        public static void Run()
        {
            // TTF payloads are the verified CC0 Kenney Future Narrow download.
            // Rebuild the atlas too: renaming the font without replacing glyphs is not a font replacement.
            AssetDatabase.Refresh();
            var fontPaths = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { IntegrationSetup.Root })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains("DUGKate")).ToArray();
            foreach (var path in fontPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                string sourcePath = AssetDatabase.GUIDToAssetPath(new SerializedObject(font).FindProperty("m_SourceFontFileGUID").stringValue);
                var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                if (source == null) throw new InvalidOperationException("Missing CC0 source font for " + path);
                var generated = TMP_FontAsset.CreateFontAsset(source, 90, 12, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                string characters = new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray());
                if (!generated.TryAddCharacters(characters, out string missing))
                    throw new InvalidOperationException("CC0 font is missing UI characters: " + missing);
                var atlas = font.atlasTextures[0];
                var material = font.material;
                var fallbacks = font.fallbackFontAssetTable;
                var generatedAtlas = generated.atlasTextures[0];
                var generatedMaterial = generated.material;
                // Keep subasset IDs so every prefab and material preset retains its reference.
                EditorUtility.CopySerialized(generatedAtlas, atlas);
                material.CopyPropertiesFromMaterial(generatedMaterial);
                material.mainTexture = atlas;
                EditorUtility.CopySerialized(generated, font);
                font.atlasTextures = new[] { atlas };
                font.material = material;
                font.fallbackFontAssetTable = fallbacks;
                font.name = Path.GetFileNameWithoutExtension(path).Replace("DUGKate-Regular", "KenneyFutureNarrow");
                atlas.name = font.name + " Atlas";
                material.name = font.name + " Material";
                foreach (string matPath in AssetDatabase.FindAssets("t:Material", new[] { IntegrationSetup.Root }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    if (!matPath.Contains("DUGKate")) continue;
                    var preset = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (preset.mainTexture != atlas) continue;
                    foreach (var property in new[] { "_GradientScale", "_TextureWidth", "_TextureHeight", "_WeightNormal", "_WeightBold" })
                        if (material.HasProperty(property)) preset.SetFloat(property, material.GetFloat(property));
                    if (preset.HasProperty("_OutlineWidth")) preset.SetFloat("_OutlineWidth", Mathf.Min(.15f, preset.GetFloat("_OutlineWidth")));
                    preset.name = preset.name.Replace("DUGKate-Regular", "KenneyFutureNarrow");
                    EditorUtility.SetDirty(preset);
                }
                EditorUtility.SetDirty(atlas); EditorUtility.SetDirty(material); EditorUtility.SetDirty(font);
                UnityEngine.Object.DestroyImmediate(generatedMaterial);
                UnityEngine.Object.DestroyImmediate(generatedAtlas);
                UnityEngine.Object.DestroyImmediate(generated);
            }
            AssetDatabase.SaveAssets();
            // Rename actual files via AssetDatabase, retaining GUIDs and references.
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && Path.GetFileName(p).Contains("DUG")).OrderByDescending(p => p.Length).ToArray())
            {
                var name = Path.GetFileName(path).Replace("DUGKate-Regular", "KenneyFutureNarrow").Replace("01_DUGKate", "01_KenneyFuture");
                string destination = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + name;
                string error = AssetDatabase.MoveAsset(path, destination);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BK_CC0_FONT_REPLACEMENT_OK");
        }
    }
}
