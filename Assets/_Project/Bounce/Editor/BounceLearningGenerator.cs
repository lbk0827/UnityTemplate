using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    // Explicit authoring only. The original asset GUID and archived 22 boards are preserved.
    public static class BounceLearningGenerator
    {
        public const int Seed = 20261026;
        public const string DataPath = "Assets/_Project/Bounce/Content/bounce-learning-levels.json";
        public const string LegacyPath = "Assets/_Project/Bounce/Content/bounce-levels.json";
        public const string PresentationPath = "Assets/_Project/Bounce/Content/Presentation.asset";

        // Archived prototype generator; release content is authored by BounceReleaseGenerator.
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
            EditorUtility.SetDirty(presentation);
            RippleBoxSetup.ExpandPalette();
            AssetDatabase.SaveAssets();
            Debug.Log("Applied ten dense, multicolour shape boards and nine block colours. Archived boards preserved.");
        }

        public static BounceModule.Levels Create(int seed)
        {
            // Hand-authored silhouettes. Keep the seed argument for existing tooling.
            return new BounceModule.Levels { levels = new[] {
                Shape("루비 하트", "두 갈래 곡선을 따라 반사시켜 넓은 하트 내부를 정리합니다.", 42,
                    ".555...555.", "55555.55555", "55855555555", "55855555555", "55555555555",
                    ".555555555.", "..5555555..", "...55555...", "....555....", ".....5....."),
                Shape("빨간 사과", "잎과 꼭지까지 남김없이 제거해야 합니다. 외곽 반사로 윗부분에 진입하세요.", 50,
                    "......9.11...", "......91111..", "......91.....", "...555.555...", "..558555555..",
                    ".55885555555.", ".55855555555.", ".55555555555.", ".55555555555.", "..555555555..",
                    "..555555555..", "...555.555..."),
                Shape("푸른 고래", "넓은 몸통과 떨어진 물줄기, 꼬리를 모두 공략하는 가로형 보드입니다.", 82,
                    "......7.7..........", ".....77.77.........", ".......7...........", ".....222222........",
                    "...2222222222......", "..222222222222.....", ".22222222222222..22", ".222922222222222222",
                    ".228888888222222222", "..8888888888822222.", "...8888888882222...", ".....2222.........."),
                Shape("봄 튤립", "꽃잎의 홈과 가는 줄기가 서로 다른 조준을 요구합니다.", 56,
                    "..3...3...3..", "..33.333.33..", "..333333333..", "..338333333..", "..338333333..",
                    "...3333333...", "....33333....", "......1......", ".11...1...11.", ".111..1..111.",
                    "..111111111..", "...1111111...", "......1......", "......1......"),
                Shape("무지개 나비", "양쪽 날개를 번갈아 공략합니다. 여러 색은 내구도가 아닌 그림의 일부입니다.", 73,
                    ".....9...9.....", ".33...9.9...33.", "33333..9..33333", "338333999333833", "334433999334433",
                    ".3443399933443.", "..33339993333..", "...222999222...", "..22729992722..", ".2227299927222.",
                    ".2222299922222.", "..2222.9.2222..", "...22..9..22..."),
                Shape("씨앗 딸기", "처음 등장한 기뢰가 주변 씨앗과 과육을 함께 제거합니다.", 55,
                    "......1......", "...11.1.11...", "..111111111..", "...1111111...", "..555555555..",
                    ".55455555455.", "5555555555555", "5555545555555", ".55555M55555.", ".55455555455.",
                    "..555555555..", "...5545555...", "....55555....", ".....555.....", "......5......"),
                Shape("바다거북", "등딱지 안의 기뢰와 바깥 지느러미를 분리해 공략합니다.", 64,
                    "......111......", "......191......", "..77..111..77..", ".7771111111777.", ".7711444441177.",
                    "..11411411411..", "..1141M41M411..", "..11411411411..", "..11444444411..", ".7711444441177.",
                    ".7771111111777.", "..77..111..77..", ".......1......."),
                Shape("별빛 로켓", "중앙 기뢰로 동체를 열고 양쪽 날개와 불꽃을 정리합니다.", 88,
                    ".......5.......", "......555......", ".....55555.....", ".....88888.....", "....8888888....",
                    "....8822288....", "....8277728....", "....8279728....", "....8822288....", "...5888M8885...",
                    "..55888888855..", ".5558888888555.", "5555888M8885555", "555588888885555", "....6699966....",
                    "....644M446....", ".....64446.....", "......646......", ".......6......."),
                Shape("황금 돛단배", "돛 사이 빈 통로에서 반사를 만들고 선체의 기뢰를 활용합니다.", 96,
                    "........9........", ".......49........", "......4498.......", ".....444988......", "....44449888.....",
                    "...4444498888....", "..444444988888...", ".44444449888888..", "4444444498888888.", "........9........",
                    "99999999999999999", ".66M666666666M66.", "..666666M666666..", "...99999999999...", "77777777777777777"),
                Shape("바닷가 성", "가장 큰 보드입니다. 성벽 기뢰와 창문 통로로 깊은 반사를 만드세요.", 75,
                    ".4.............4.", ".44...........44.", ".9.............9.", "89898.......89898", "88888..444..88888",
                    "88M88.44444.88M88", "88988888888888988", "88988888888888988", "88888889898888888", "88888889898888888",
                    "88888888888888888", "888M888...888M888", "8888888...8888888", "8888888...8888888", "9999999...9999999")
            }};
        }

        private static BounceModule.Level Shape(string title, string intent, int balls, params string[] art)
        {
            int width = art.Max(r => r.Length) + 2, height = art.Length + 5;
            var rows = Enumerable.Repeat(new string('.', width), height).ToArray();
            var gimmicks = new List<GimmickSpec>();
            for (int y = 0; y < art.Length; y++)
            {
                var line = art[y].PadRight(width - 2, '.').ToCharArray();
                for (int x = 0; x < line.Length; x++) if (line[x] == 'M')
                { gimmicks.Add(new GimmickSpec { type = "mine", col = x + 1, row = y + 1 }); line[x] = '.'; }
                rows[y + 1] = "." + new string(line) + ".";
            }
            return new BounceModule.Level { title = title, intent = intent, balls = balls, gridRows = height, rows = rows, gimmicks = gimmicks.ToArray() };
        }
    }
}
