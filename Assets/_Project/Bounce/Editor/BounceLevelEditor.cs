using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BK.Kit.Editor
{
    public sealed class BounceLevelEditor : EditorWindow
    {
        public const string DataPath = BounceLearningGenerator.DataPath;
        private enum Brush { Select, Block, Erase, Tube, Cupboard, Panel, Mine, Switch, Door, Combo, Armor }
        [SerializeField] private BounceLevelDraft draft;
        [SerializeField] private int stage, selected = -1, colour = 1;
        [SerializeField] private Brush brush;
        [SerializeField] private string diskText;
        private Vector2 scroll;
        private string feedback;
        private BounceModule.Level Level => draft.library.levels[stage];

        [MenuItem("BK Kit/Level Editor")]
        public static void Open() => GetWindow<BounceLevelEditor>("Bounce Level Editor");

        private void OnEnable()
        {
            minSize = new Vector2(780, 650);
            saveChangesMessage = "레벨 변경 사항을 저장하시겠습니까?";
            Undo.undoRedoPerformed += OnUndo;
            if (draft == null) Load();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndo;
        private void OnDestroy() { if (draft != null) DestroyImmediate(draft); }
        private void OnUndo() { stage = Mathf.Clamp(stage, 0, draft.library.levels.Length - 1); selected = -1; hasUnsavedChanges = true; Repaint(); }

        private void Load()
        {
            if (hasUnsavedChanges && !EditorUtility.DisplayDialog("다시 불러오기", "저장하지 않은 레벨 변경을 버릴까요?", "불러오기", "취소")) return;
            if (draft == null) { draft = CreateInstance<BounceLevelDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
            diskText = File.ReadAllText(DataPath);
            draft.library = JsonUtility.FromJson<BounceModule.Levels>(diskText);
            stage = Mathf.Clamp(stage, 0, draft.library.levels.Length - 1);
            selected = -1; hasUnsavedChanges = false; feedback = null;
            Undo.ClearUndo(draft);
        }

        private void Change(string label, Action change)
        {
            Undo.RecordObject(draft, label); change(); EditorUtility.SetDirty(draft); hasUnsavedChanges = true;
        }

        public override void SaveChanges()
        {
            if (Save()) base.SaveChanges();
        }

        private bool Save()
        {
            for (int i = 0; i < draft.library.levels.Length; i++)
            {
                var errors = BounceLevelLayout.Validate(draft.library.levels[i]);
                if (errors.Count == 0) continue;
                stage = i; selected = -1; feedback = "저장 실패: 스테이지 " + (i + 1) + "의 배치를 수정하세요."; return false;
            }
            if (File.ReadAllText(DataPath) != diskText)
            { feedback = "파일이 외부에서 변경되었습니다. 덮어쓰지 않았습니다. 변경 사항을 확인한 뒤 다시 불러오세요."; return false; }
            var json = JsonUtility.ToJson(draft.library, true) + "\n";
            File.WriteAllText(DataPath, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(DataPath);
            diskText = json; hasUnsavedChanges = false; feedback = "전체 레벨 저장 완료."; return true;
        }

        private void OnGUI()
        {
            if (draft == null || draft.library == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60))) Save();
                if (GUILayout.Button("다시 불러오기", EditorStyles.toolbarButton, GUILayout.Width(110))) Load();
                GUILayout.FlexibleSpace();
                GUILayout.Label(hasUnsavedChanges ? "저장되지 않은 변경 사항" : "저장됨");
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int next = EditorGUILayout.Popup("스테이지", stage, Enumerable.Range(1, draft.library.levels.Length).Select(i => "Stage " + i).ToArray());
            if (next != stage) { stage = next; selected = -1; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("빈 스테이지 추가")) Add(false);
                if (GUILayout.Button("현재 스테이지 복제")) Add(true);
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || KitApp.Instance == null || KitApp.Instance.IsLoading))
                    if (GUILayout.Button("저장하고 플레이"))
                    {
                        if (Save())
                        {
                            var app = KitApp.Instance;
                            if (app.TrySetSandboxProgress(stage + 1, (int)Math.Min(int.MaxValue, app.Gold), 10)) app.Play(stage + 1);
                        }
                    }
            }
            EditorGUILayout.LabelField("Play sets the local sandbox stage and booster inventory.", EditorStyles.miniLabel);
            int balls = EditorGUILayout.IntField("발사 가능한 공", Level.balls);
            if (balls != Level.balls) Change("Change ball budget", () => Level.balls = balls);
            brush = (Brush)GUILayout.SelectionGrid((int)brush, new[] { "\uC120\uD0DD", "\uBE14\uB85D", "\uC9C0\uC6B0\uAC1C" }.Concat(BounceLevelLayout.Labels).ToArray(), 6);
            if (brush == Brush.Block) colour = EditorGUILayout.IntSlider("블록 색상", colour, 1, 9);
            EditorGUILayout.HelpBox("도구를 선택하고 칸을 클릭해 배치하세요. 우클릭은 삭제, Ctrl+Z는 실행 취소입니다. 금속관의 용광로는 관 시작 칸의 왼쪽(세로는 위쪽) 한 칸을 차지합니다.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGrid();
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(310))) DrawSelection();
            }
            foreach (var error in BounceLevelLayout.Validate(Level)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!string.IsNullOrEmpty(feedback)) EditorGUILayout.HelpBox(feedback, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void Add(bool duplicate)
        {
            var value = duplicate ? JsonUtility.FromJson<BounceModule.Level>(JsonUtility.ToJson(Level)) :
                new BounceModule.Level { balls = 20, rows = Enumerable.Repeat(".........", BounceLevelLayout.MaxRows).ToArray(), gimmicks = new GimmickSpec[0] };
            Change("Add stage", () => draft.library.levels = draft.library.levels.Concat(new[] { value }).ToArray());
            stage = draft.library.levels.Length - 1; selected = -1;
        }

        private void DrawGrid()
        {
            int columns = Level.rows[0].Length;
            const float cellSize = 34;
            var area = GUILayoutUtility.GetRect(columns * cellSize, BounceLevelLayout.MaxRows * cellSize, GUILayout.ExpandWidth(false));
            var specs = Level.gimmicks ?? new GimmickSpec[0];
            for (int row = 0; row < BounceLevelLayout.MaxRows; row++) for (int col = 0; col < columns; col++)
            {
                var rect = new Rect(area.x + col * cellSize, area.y + row * cellSize, cellSize - 2, cellSize - 2);
                var point = new Vector2Int(col, row);
                int index = Find(point);
                var color = BounceLevelLayout.HasBlock(Level, col, row) ? Color.HSVToRGB((Level.rows[row][col] - '1') / 9f, .6f, .65f) : new Color(.19f, .27f, .29f);
                string label = BounceLevelLayout.HasBlock(Level, col, row) ? Level.rows[row][col].ToString() : "";
                if (index >= 0)
                {
                    var spec = specs[index];
                    color = spec.type switch { "tube" => new Color(.4f,.5f,.65f), "cupboard" => new Color(.55f,.32f,.13f), "mine" => new Color(.7f,.2f,.15f), "switch" => new Color(.1f,.6f,.6f), "door" => new Color(.5f,.4f,.15f), "combo" => new Color(.5f,.2f,.7f), "armor" => new Color(.2f,.3f,.45f), _ => new Color(.65f,.5f,.13f) };
                    label = BounceLevelLayout.Labels[Math.Max(0, Array.IndexOf(BounceLevelLayout.Types, spec.type))];
                    if (spec.type == "tube" && point == BounceLevelLayout.Cells(spec).First()) { color = new Color(.45f, .16f, .65f); label = "용광로"; }
                }
                EditorGUI.DrawRect(rect, color);
                if (index == selected && selected >= 0) Outline(rect, new Color(1, .88f, .3f));
                GUI.Label(rect, new GUIContent(label, "열 " + col + ", 행 " + row), new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = Color.white } });
                var ev = Event.current;
                if (ev.type == EventType.MouseDown && rect.Contains(ev.mousePosition) && (ev.button == 0 || ev.button == 1))
                { Apply(point, ev.button == 1); ev.Use(); GUIUtility.ExitGUI(); }
            }
        }

        private static void Outline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), color); EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), color); EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), color);
        }

        private int Find(Vector2Int point)
        {
            var specs = Level.gimmicks ?? new GimmickSpec[0];
            for (int i = specs.Length - 1; i >= 0; i--)
                if (specs[i].type == "panel" ? BounceLevelLayout.PanelArea(specs[i]).Contains(point) : BounceLevelLayout.Cells(specs[i]).Contains(point)) return i;
            return -1;
        }

        private void Apply(Vector2Int point, bool erase)
        {
            int index = Find(point);
            if (erase || brush == Brush.Erase)
            {
                Change("Erase board item", () =>
                {
                    if (index >= 0) Level.gimmicks = Level.gimmicks.Where((_, i) => i != index).ToArray();
                    else SetBlock(point, '.');
                });
                selected = -1; return;
            }
            if (brush == Brush.Select) { selected = index; return; }
            if (brush == Brush.Block)
            {
                if (index >= 0) { feedback = "기믹이 차지하는 칸입니다. 먼저 이동하거나 삭제하세요."; return; }
                Change("Paint block", () => SetBlock(point, (char)('0' + colour))); return;
            }
            if (index >= 0) { selected = index; brush = Brush.Select; return; }
            var spec = new GimmickSpec { type = BounceLevelLayout.Types[(int)brush - 3], col = point.x, row = point.y };
            Change("Place gimmick", () => Level.gimmicks = (Level.gimmicks ?? new GimmickSpec[0]).Concat(new[] {spec}).ToArray());
            selected = Level.gimmicks.Length - 1; brush = Brush.Select;
        }

        private void SetBlock(Vector2Int point, char value)
        {
            if (point.x < 0 || point.x >= Level.rows[0].Length || point.y < 0 || point.y >= BounceLevelLayout.MaxRows) return;
            if (point.y >= Level.rows.Length)
                Level.rows = Level.rows.Concat(Enumerable.Repeat(new string('.', Level.rows[0].Length), point.y + 1 - Level.rows.Length)).ToArray();
            var row = Level.rows[point.y].ToCharArray(); row[point.x] = value; Level.rows[point.y] = new string(row);
        }

        private void DrawSelection()
        {
            if (selected < 0 || Level.gimmicks == null || selected >= Level.gimmicks.Length)
            { EditorGUILayout.LabelField("기믹을 선택하면 설정이 표시됩니다."); return; }
            var spec = Level.gimmicks[selected];
            EditorGUILayout.LabelField(BounceLevelLayout.Labels[Math.Max(0, Array.IndexOf(BounceLevelLayout.Types, spec.type))], EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int col = EditorGUILayout.IntField("열 (0부터)", spec.col);
            int row = EditorGUILayout.IntField("행 (0부터)", spec.row);
            int length = spec.length; bool vertical = spec.vertical;
            float width = spec.width, travel = spec.travel, speed = spec.speed;
            string link = spec.link; int weakSide = spec.weakSide;
            if (spec.type == "tube")
            {
                length = EditorGUILayout.IntSlider("관 길이 (용광로 제외)", length, 1, 13);
                vertical = EditorGUILayout.Popup("방향", vertical ? 1 : 0, new[] { "가로 →", "세로 ↓" }) == 1;
                EditorGUILayout.HelpBox("타격마다 한 칸이 용광로로 들어가 녹습니다. 관을 모두 녹이면 용광로가 꺼지고 충돌도 해제됩니다.", MessageType.None);
            }
            if (spec.type == "panel")
            {
                width = EditorGUILayout.Slider("판 너비 (칸)", width, .5f, 5);
                travel = EditorGUILayout.Slider("중심에서 이동 거리 (칸)", travel, .25f, 5);
                float period = EditorGUILayout.Slider("왕복 주기 (초)", 2 * Mathf.PI / Mathf.Max(.01f, speed), 2.1f, 30);
                speed = 2 * Mathf.PI / period;
            }
            if (spec.type == "switch" || spec.type == "door")
            {
                link = EditorGUILayout.TextField("연결 ID", link);
                EditorGUILayout.HelpBox("같은 연결 ID의 스위치를 맞히면 연결된 문이 모두 열립니다. 연결선은 게임 화면에도 표시됩니다.", MessageType.None);
            }
            if (spec.type == "armor") weakSide = EditorGUILayout.Popup("약점 방향", weakSide, new[] { "아래 (발사대)", "오른쪽", "위", "왼쪽" });
            if (spec.type == "combo") EditorGUILayout.HelpBox("동일한 공으로 두 번 맞혀야 파괴됩니다. 해당 공이 사라지면 외피가 다시 닫힙니다.", MessageType.None);
            if (spec.type == "mine") EditorGUILayout.HelpBox("한 번 맞으면 주변 8칸에 피해를 주며 다른 기뢰를 연쇄 폭발시킵니다. 금속관은 폭발당 한 칸만 감소합니다.", MessageType.None);
            if (EditorGUI.EndChangeCheck()) Change("Configure gimmick", () =>
            { spec.col = col; spec.row = row; spec.length = length; spec.vertical = vertical; spec.width = width; spec.travel = travel; spec.speed = speed; spec.link = link; spec.weakSide = weakSide; });
            if (spec.type == "cupboard")
            {
                EditorGUILayout.HelpBox("3×2칸을 차지합니다. 상하좌우 인접 블록을 타격할 때 접시가 하나씩 깨집니다.", MessageType.None);
                if (GUILayout.Button("주변 블록 채우기")) Change("Fill cupboard neighbours", () =>
                {
                    var cells = BounceLevelLayout.Cells(spec).ToArray();
                    foreach (var cell in cells) foreach (var step in new[] {Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down})
                    {
                        var other = cell + step;
                        if (!cells.Contains(other) && Find(other) < 0 && !BounceLevelLayout.HasBlock(Level, other.x, other.y)) SetBlock(other, '2');
                    }
                });
            }
            if (GUILayout.Button("기믹 프리팹 열기"))
            {
                var config = AssetDatabase.LoadAssetAtPath<IngameConfig>("Assets/_Project/Bounce/Content/IngameConfig.asset");
                if (config != null && config.gimmickPrefabs != null)
                {
                    var prefab = config.gimmickPrefabs.Find(spec.type);
                    if (prefab != null) AssetDatabase.OpenAsset(prefab.gameObject);
                }
            }
            if (GUILayout.Button("선택한 기믹 삭제"))
            { Change("Delete gimmick", () => Level.gimmicks = Level.gimmicks.Where((_, i) => i != selected).ToArray()); selected = -1; }
        }
    }
}
