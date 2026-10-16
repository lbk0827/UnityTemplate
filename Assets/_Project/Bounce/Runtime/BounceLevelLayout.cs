using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BK.Kit
{
    // Shared by the authoring grid and validation; coordinates are column/row, not world X/Z.
    public static class BounceLevelLayout
    {
        public const int MaxRows = 13;

        public static IEnumerable<Vector2Int> Cells(GimmickSpec spec)
        {
            var start = new Vector2Int(spec.col, spec.row);
            if (spec.type == "tube")
            {
                var axis = spec.vertical ? Vector2Int.up : Vector2Int.right;
                yield return start - axis; // Furnace occupies its own cell.
                for (int i = 0; i < spec.length; i++) yield return start + axis * i;
            }
            else if (spec.type == "cupboard")
                for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
                    yield return start + new Vector2Int(col, row);
        }

        public static Rect PanelArea(GimmickSpec spec) => new Rect(spec.col - spec.travel - spec.width / 2,
            spec.row - .2f, spec.width + spec.travel * 2, .4f);

        public static bool HasBlock(BounceModule.Level level, int col, int row) => row >= 0 && row < level.rows.Length &&
            col >= 0 && col < level.rows[row].Length && level.rows[row][col] >= '1' && level.rows[row][col] <= '9';

        public static List<string> Validate(BounceModule.Level level)
        {
            var errors = new List<string>();
            if (level == null || level.rows == null || level.rows.Length == 0 || string.IsNullOrEmpty(level.rows[0]))
            { errors.Add("보드 데이터가 없습니다."); return errors; }
            int columns = level.rows[0].Length;
            if (columns < 3 || columns > 15) errors.Add("보드 폭은 3~15칸이어야 합니다.");
            if (level.rows.Length > MaxRows) errors.Add("보드 높이는 최대 13칸입니다.");
            if (level.balls < 1) errors.Add("발사할 공이 1개 이상 필요합니다.");
            if (level.rows.Any(r => r == null || r.Length != columns || r.Any(c => c != '.' && (c < '1' || c > '9'))))
            { errors.Add("모든 행의 폭이 같아야 하며 빈칸(.)과 색상(1~9)만 사용할 수 있습니다."); return errors; }
            var occupied = new HashSet<Vector2Int>();
            for (int row = 0; row < level.rows.Length; row++) for (int col = 0; col < columns; col++)
                if (HasBlock(level, col, row)) occupied.Add(new Vector2Int(col, row));
            bool hasGoal = occupied.Count > 0;
            var panels = new List<Rect>();
            foreach (var spec in level.gimmicks ?? new GimmickSpec[0])
            {
                if (spec == null) { errors.Add("비어 있는 기믹 항목이 있습니다."); continue; }
                string label = spec.type + " (" + spec.col + ", " + spec.row + ")";
                if (spec.type == "panel")
                {
                    if (!Finite(spec.width) || !Finite(spec.travel) || !Finite(spec.speed) ||
                        spec.width < .5f || spec.travel <= 0 || spec.speed <= 0 || spec.speed > 3)
                    { errors.Add(label + ": 폭 ≥ 0.5, 이동 거리 > 0, 속도 0~3 범위로 설정하세요."); continue; }
                    var area = PanelArea(spec);
                    if (area.xMin < -.4f || area.xMax > columns - .6f || area.yMin < -.5f || area.yMax > MaxRows - .5f)
                        errors.Add(label + ": 이동 범위가 벽 밖으로 나갑니다.");
                    panels.Add(area);
                    continue;
                }
                if (spec.type != "tube" && spec.type != "cupboard")
                { errors.Add(label + ": 알 수 없는 기믹입니다."); continue; }
                hasGoal = true;
                if (spec.type == "tube" && (spec.length < 1 || spec.length > MaxRows))
                { errors.Add(label + ": 관 길이는 1~13칸입니다."); continue; }
                foreach (var cell in Cells(spec))
                {
                    if (cell.x < 0 || cell.x >= columns || cell.y < 0 || cell.y >= MaxRows)
                        errors.Add(label + ": 용광로를 포함한 기믹이 보드 밖으로 나갑니다.");
                    if (!occupied.Add(cell)) errors.Add(label + ": 다른 기믹 또는 블록과 겹칩니다.");
                }
                if (spec.type == "cupboard")
                {
                    var neighbours = new HashSet<Vector2Int>();
                    foreach (var cell in Cells(spec)) foreach (var step in new[] {Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down})
                    {
                        var other = cell + step;
                        if (HasBlock(level, other.x, other.y)) neighbours.Add(other);
                    }
                    if (neighbours.Count < 10) errors.Add(label + ": 접시 10개를 깰 인접 블록 10개가 필요합니다. '주변 블록 채우기'를 사용하세요.");
                }
            }
            for (int i = 0; i < panels.Count; i++)
            {
                if (occupied.Any(c => panels[i].Overlaps(new Rect(c.x - .5f, c.y - .5f, 1, 1))))
                    errors.Add("이동 판의 왕복 경로가 블록 또는 기믹과 겹칩니다.");
                for (int j = 0; j < i; j++) if (panels[i].Overlaps(panels[j])) errors.Add("이동 판끼리 왕복 경로가 겹칩니다.");
            }
            if (!hasGoal) errors.Add("파괴 목표가 최소 하나 필요합니다. 이동 판은 목표에 포함되지 않습니다.");
            return errors.Distinct().ToList();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
