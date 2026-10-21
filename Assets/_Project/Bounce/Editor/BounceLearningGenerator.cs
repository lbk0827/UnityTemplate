using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    // Explicit, seeded authoring only; never changes a board on import or at runtime.
    public static class BounceLearningGenerator
    {
        public const int Seed = 20261021;
        public const string DataPath = "Assets/_Project/Bounce/Content/bounce-learning-levels.json";
        public const string LegacyPath = "Assets/_Project/Bounce/Content/bounce-levels.json";
        public const string PresentationPath = "Assets/_Project/Bounce/Content/Presentation.asset";

        [MenuItem("BK Kit/Balance/Generate Learning Levels 1-10")]
        public static void GenerateAndApply()
        {
            var library = Create(Seed);
            for (int i = 0; i < library.levels.Length; i++)
            {
                var errors = BounceLevelLayout.Validate(library.levels[i]);
                if (errors.Count > 0) throw new InvalidOperationException("Stage " + (i + 1) + ": " + string.Join(" / ", errors));
            }
            File.WriteAllText(DataPath, JsonUtility.ToJson(library, true) + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceSynchronousImport);
            var presentation = AssetDatabase.LoadAssetAtPath<GamePresentation>(PresentationPath);
            presentation.levels = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            var mineGuide = Grp1PresentationSetup.RenderGuide(presentation.ingameConfig.gimmickPrefabs.Find("mine"), "mine", "Assets/_Project/Bounce/Content/UI/Grp1/");
            presentation.refinedSprites = presentation.refinedSprites.Where(s => s != null && s.name != "Guide_mine").Append(mineGuide).ToArray();
            EditorUtility.SetDirty(presentation); AssetDatabase.SaveAssets();
            Debug.Log("Generated 10 D1-target learning boards; legacy 22-board catalogue preserved. Seed=" + Seed);
        }

        public static BounceModule.Levels Create(int seed)
        {
            var random = new System.Random(seed);
            var levels = new BounceModule.Level[10];
            for (int i = 0; i < levels.Length; i++)
            {
                var cells = Enumerable.Range(0, 13).Select(_ => ".........".ToCharArray()).ToArray();
                var mines = new System.Collections.Generic.List<GimmickSpec>();
                int colour = i < 2 ? i + 1 : random.Next(1, 5);
                void Block(int x, int y) => cells[y][x] = (char)('0' + colour);
                void Rect(int x, int y, int width, int height)
                { for (int row = y; row < y + height; row++) for (int col = x; col < x + width; col++) Block(col, row); }
                void Mine(int x, int y, bool openFront = true)
                {
                    Rect(x - 1, y - 1, 3, 3); cells[y][x] = '.';
                    if (openFront) cells[y + 1][x] = '.';
                    mines.Add(new GimmickSpec { type = "mine", col = x, row = y });
                }
                int balls;
                switch (i)
                {
                    case 0: Rect(3, 3, 3, 2); balls = 8; break; // Wide, unobstructed first targets.
                    case 1: Rect(3, 2, 3, 1); Rect(2, 3, 5, 1); Rect(3, 4, 3, 1); balls = 10; break;
                    case 2: Rect(1, 2, 2, 3); Rect(6, 2, 2, 3); balls = 12; break;
                    case 3: Rect(1, 1, 2, 4); Rect(6, 1, 2, 4); balls = 18; break;
                    case 4: Rect(3, 3, 3, 3); balls = 10; break; // Short consolidation before a new rule.
                    case 5: Mine(4, 4); balls = 6; break;
                    case 6: Mine(2, 3); Mine(6, 5); balls = 8; break;
                    case 7:
                        Rect(2, 3, 5, 3);
                        for (int x = 3; x <= 5; x++) { cells[4][x] = '.'; mines.Add(new GimmickSpec {type="mine",col=x,row=4}); }
                        cells[5][4] = '.'; balls = 8; break;
                    case 8: Mine(2, 3); Rect(6, 2, 2, 3); balls = 12; break;
                    default: Mine(4, 4); Rect(1, 1, 2, 1); Rect(6, 1, 2, 1); balls = 10; break;
                }
                // Seeded symmetry variation preserves clearance and each lesson's structure.
                if (random.Next(2) == 1)
                {
                    foreach (var row in cells) Array.Reverse(row);
                    foreach (var mine in mines) mine.col = 8 - mine.col;
                }
                levels[i] = new BounceModule.Level { balls = balls, rows = cells.Select(r => new string(r)).ToArray(), gimmicks = mines.ToArray() };
            }
            return new BounceModule.Levels { levels = levels };
        }
    }
}
