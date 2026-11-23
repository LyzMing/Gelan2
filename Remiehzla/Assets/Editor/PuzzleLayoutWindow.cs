using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Remiehzla.Puzzle;

namespace Remiehzla.EditorTools
{
    /// <summary>
    /// 拼图布局编辑器：可视化地摆「初始打乱」和「正解」。
    ///
    /// 用法：
    ///   菜单 工具 → 拼图布局编辑器
    ///   选关卡 → 选编辑目标（初始布局 / 某个解）
    ///   点一格选中，再点另一格 = 两块互换
    ///   选中后用按钮（或下面的快捷键）旋转 / 翻转 / 标记错乱
    ///   改动会立即写回资产，不需要额外保存
    /// </summary>
    public class PuzzleLayoutWindow : EditorWindow
    {
        const float CellSize = 76f;
        const float CellGap = 4f;
        const float LabelHeight = 18f;

        LevelDefinition level;
        Vector2 scroll;
        int selectedCell = -1;
        int solutionIndex = -1;          // -1 = 编辑初始布局

        GUIStyle centerLabel;

        PuzzleConfig Config => level != null ? level.puzzle : null;

        [MenuItem("工具/拼图布局编辑器")]
        public static void Open()
        {
            var window = GetWindow<PuzzleLayoutWindow>(false, "拼图布局", true);
            window.minSize = new Vector2(420f, 560f);
            window.Show();
        }

        // ---------------------------------------------------------------- 界面

        void OnGUI()
        {
            if (centerLabel == null)
            {
                centerLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12
                };
                centerLabel.normal.textColor = Color.white;
            }

            EditorGUILayout.Space(6f);

            var picked = (LevelDefinition)EditorGUILayout.ObjectField("关卡资产", level, typeof(LevelDefinition), false);

            if (picked != level)
            {
                level = picked;
                selectedCell = -1;
                solutionIndex = -1;
            }

            if (level == null)
            {
                EditorGUILayout.HelpBox(
                    "选一个 LevelDefinition（右键 Create → Remiehzla → Level Definition 创建）。",
                    MessageType.Info);
                return;
            }

            if (level.puzzle == null)
            {
                EditorGUILayout.HelpBox("这个关卡没有拼图配置（纯剧情关？）。", MessageType.Warning);

                if (GUILayout.Button("为它创建拼图配置"))
                {
                    Undo.RecordObject(level, "创建拼图配置");
                    level.puzzle = new PuzzleConfig();
                    EditorUtility.SetDirty(level);
                }

                return;
            }

            PuzzleConfig config = level.puzzle;
            int count = Mathf.Clamp(config.gridSize, 2, 8);
            count *= count;

            EditorGUILayout.LabelField($"{level.levelId} · {level.displayName} · {config.gridSize}×{config.gridSize}",
                EditorStyles.boldLabel);

            if (config.image == null)
            {
                EditorGUILayout.HelpBox("还没指定拼图整图（在 Inspector 的 puzzle.image 里拖）。" +
                                        "没有图也能摆布局，只是看不到画面。", MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            DrawTargetToolbar(config);

            EditorGUILayout.Space(6f);
            DrawGrid(config, count);

            EditorGUILayout.Space(6f);
            DrawCellTools();

            EditorGUILayout.Space(6f);
            DrawValidation(config);
        }

        void DrawTargetToolbar(PuzzleConfig config)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("编辑目标", GUILayout.Width(56f));

                if (GUILayout.Toggle(solutionIndex < 0, "初始布局", EditorStyles.miniButton, GUILayout.Width(72f))
                    && solutionIndex >= 0)
                {
                    solutionIndex = -1;
                    selectedCell = -1;
                }

                for (int i = 0; i < config.solutions.Length; i++)
                {
                    PuzzleSolution solution = config.solutions[i];
                    if (solution == null) continue;

                    bool on = solutionIndex == i;
                    string label = string.IsNullOrEmpty(solution.solutionId) ? $"解{i}" : solution.solutionId;
                    if (solution.isHidden) label = "★" + label;

                    if (GUILayout.Toggle(on, label, EditorStyles.miniButton, GUILayout.Width(96f)) && !on)
                    {
                        solutionIndex = i;
                        selectedCell = -1;
                    }
                }

                if (GUILayout.Button("＋新解", EditorStyles.miniButton, GUILayout.Width(56f)))
                {
                    Undo.RecordObject(level, "新增正解");

                    var list = new List<PuzzleSolution>(config.solutions)
                    {
                        new PuzzleSolution(Mathf.Clamp(config.gridSize, 2, 8) * Mathf.Clamp(config.gridSize, 2, 8))
                    };

                    config.solutions = list.ToArray();
                    solutionIndex = config.solutions.Length - 1;
                    selectedCell = -1;
                    EditorUtility.SetDirty(level);
                }

                if (solutionIndex >= 0 && GUILayout.Button("－删除", EditorStyles.miniButton, GUILayout.Width(56f)))
                {
                    string name = config.solutions[solutionIndex].solutionId;
                    if (EditorUtility.DisplayDialog("删除正解", $"确定删除 \"{name}\"？", "删除", "取消"))
                    {
                        Undo.RecordObject(level, "删除正解");

                        var list = new List<PuzzleSolution>(config.solutions);
                        list.RemoveAt(solutionIndex);
                        config.solutions = list.ToArray();
                        solutionIndex = -1;
                        EditorUtility.SetDirty(level);
                    }
                }
            }

            if (solutionIndex >= 0)
            {
                PuzzleSolution editing = config.solutions[solutionIndex];

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();

                    string newId = EditorGUILayout.TextField("solutionId", editing.solutionId, GUILayout.Width(220f));
                    bool hidden = EditorGUILayout.ToggleLeft("隐藏解", editing.isHidden, GUILayout.Width(80f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(level, "修改正解信息");
                        editing.solutionId = newId;
                        editing.isHidden = hidden;
                        EditorUtility.SetDirty(level);
                    }
                }
            }
        }

        void DrawGrid(PuzzleConfig config, int count)
        {
            int g = config.gridSize;
            if (g < 2 || g > 8)
            {
                EditorGUILayout.HelpBox($"gridSize = {g} 不合法（2~8）", MessageType.Error);
                return;
            }

            EnsureCells(config, count);

            CellState[] cells = GetCells();
            if (cells == null)
            {
                EditorGUILayout.HelpBox("当前编辑目标的数组还没准备好。", MessageType.Warning);
                return;
            }

            float full = g * CellSize + (g - 1) * CellGap;

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(430f));

            Rect area = GUILayoutUtility.GetRect(full, full, GUILayout.Width(full), GUILayout.Height(full));

            for (int row = 0; row < g; row++)
            {
                for (int col = 0; col < g; col++)
                {
                    DrawCell(CellRect(area, g, row * g + col), config, cells[row * g + col], row * g + col);
                }
            }

            HandleGridClick(area, g);

            GUILayout.EndScrollView();
        }

        void DrawCell(Rect rect, PuzzleConfig config, CellState state, int cell)
        {
            bool selected = selectedCell == cell;

            EditorGUI.DrawRect(rect, selected
                ? new Color(1f, 0.78f, 0.25f, 0.45f)
                : new Color(0.17f, 0.18f, 0.21f, 1f));

            if (config.image != null && !state.IsCorrupted)
            {
                int g = config.gridSize;
                int col = state.pieceIndex % g;
                int row = state.pieceIndex / g;

                float u = col / (float)g;
                float v = 1f - (row + 1) / (float)g;

                Matrix4x4 old = GUI.matrix;
                var pivot = new Vector2(rect.center.x, rect.center.y);

                if (state.flipped) GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), pivot);
                // GUI 坐标 y 向下，顺时针为正角度
                if (state.turnSteps != 0) GUIUtility.RotateAroundPivot(90f * state.turnSteps, pivot);

                GUI.DrawTextureWithTexCoords(rect, config.image, new Rect(u, v, 1f / g, 1f / g));

                GUI.matrix = old;
            }
            else if (state.IsCorrupted)
            {
                Rect inner = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
                EditorGUI.DrawRect(inner, new Color(0.32f, 0.30f, 0.30f, 1f));
            }

            // 底部角标：格子序号 + 图块编号
            Rect bar = new Rect(rect.x, rect.yMax - LabelHeight, rect.width, LabelHeight);
            EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.62f));

            string text = state.IsCorrupted ? $"#{cell} X" : $"#{cell} ← {state.pieceIndex}";
            GUI.Label(bar, text, centerLabel);

            if (state.turnSteps != 0 || state.flipped)
            {
                GUI.Label(new Rect(rect.x + 3f, rect.y + 2f, rect.width - 6f, 16f),
                    (state.turnSteps != 0 ? "↻" + state.turnSteps : "") + (state.flipped ? "⇄" : ""),
                    EditorStyles.whiteMiniLabel);
            }

            DrawOutline(rect, selected ? new Color(1f, 0.78f, 0.25f) : new Color(0f, 0f, 0f, 0.75f));
        }

        void HandleGridClick(Rect area, int g)
        {
            Event e = Event.current;

            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (!area.Contains(e.mousePosition)) return;

            int col = Mathf.FloorToInt((e.mousePosition.x - area.x) / (CellSize + CellGap));
            int row = Mathf.FloorToInt((e.mousePosition.y - area.y) / (CellSize + CellGap));

            if (col < 0 || col >= g || row < 0 || row >= g) return;

            int cell = row * g + col;

            // 点在格缝里就忽略
            if (!CellRect(area, g, cell).Contains(e.mousePosition)) return;

            if (selectedCell < 0)
            {
                selectedCell = cell;
            }
            else if (selectedCell == cell)
            {
                selectedCell = -1;
            }
            else
            {
                SwapCells(selectedCell, cell);
                selectedCell = -1;
            }

            e.Use();
            Repaint();
        }

        void DrawCellTools()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedCell < 0))
                {
                    if (GUILayout.Button("↺ 逆时针")) RotateSelected(3);
                    if (GUILayout.Button("↻ 顺时针")) RotateSelected(1);
                    if (GUILayout.Button("⇄ 翻转")) FlipSelected();
                    if (GUILayout.Button("标记为错乱块")) MarkSelectedCorrupted();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重置为正解形态")) ResetToIdentity();
                if (GUILayout.Button("全部摆正")) ResetOrientations();
                if (GUILayout.Button("随机打乱位置")) RandomizePieces();
            }

            EditorGUILayout.HelpBox(
                "点一格选中，再点另一格 = 两块互换内容。\n" +
                "「重置为正解形态」= 每块回到自己格子且全部正立（配置正解时最常用）。\n" +
                "改动会立即写回资产。",
                MessageType.None);
        }

        void DrawValidation(PuzzleConfig config)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            PuzzleRules.Validate(config, errors, warnings);

            for (int i = 0; i < warnings.Count; i++) EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
            for (int i = 0; i < errors.Count; i++) EditorGUILayout.HelpBox(errors[i], MessageType.Error);

            if (errors.Count == 0 && warnings.Count == 0)
                EditorGUILayout.HelpBox("配置检查通过 ✔", MessageType.Info);
        }

        // ---------------------------------------------------------------- 数据操作

        CellState[] GetCells()
        {
            PuzzleConfig config = Config;
            if (config == null) return null;

            if (solutionIndex < 0) return config.initial != null ? config.initial.cells : null;
            if (solutionIndex >= config.solutions.Length) return null;

            PuzzleSolution solution = config.solutions[solutionIndex];
            return solution != null ? solution.cells : null;
        }

        void EnsureCells(PuzzleConfig config, int count)
        {
            if (solutionIndex < 0)
            {
                if (config.initial == null)
                {
                    Undo.RecordObject(level, "创建初始布局");
                    config.initial = new PuzzleLayout(count);
                    EditorUtility.SetDirty(level);
                }
                else if (config.initial.cells == null || config.initial.cells.Length != count)
                {
                    Undo.RecordObject(level, "调整初始布局格数");
                    config.initial.cells = Resize(config.initial.cells, count);
                    EditorUtility.SetDirty(level);
                }

                return;
            }

            if (solutionIndex >= config.solutions.Length) return;

            PuzzleSolution solution = config.solutions[solutionIndex];
            if (solution == null) return;

            if (solution.cells == null || solution.cells.Length != count)
            {
                Undo.RecordObject(level, "调整正解格数");
                solution.cells = Resize(solution.cells, count);
                EditorUtility.SetDirty(level);
            }
        }

        static CellState[] Resize(CellState[] old, int count)
        {
            var cells = new CellState[count];

            for (int i = 0; i < count; i++)
            {
                cells[i] = old != null && i < old.Length ? old[i] : new CellState(i);
            }

            return cells;
        }

        void Mutate(int cell, System.Func<CellState, CellState> mutate, string undoName)
        {
            CellState[] cells = GetCells();
            if (cells == null || cell < 0 || cell >= cells.Length) return;

            Undo.RecordObject(level, undoName);
            cells[cell] = mutate(cells[cell]);
            EditorUtility.SetDirty(level);
            Repaint();
        }

        void SwapCells(int a, int b)
        {
            CellState[] cells = GetCells();
            if (cells == null || a < 0 || b < 0 || a >= cells.Length || b >= cells.Length) return;

            Undo.RecordObject(level, "交换两格");

            CellState tmp = cells[a];
            cells[a] = cells[b];
            cells[b] = tmp;

            EditorUtility.SetDirty(level);
        }

        void RotateSelected(int delta)
        {
            Mutate(selectedCell, state =>
            {
                state.turnSteps = (state.turnSteps + delta) % 4;
                return state;
            }, "旋转图块");
        }

        void FlipSelected()
        {
            Mutate(selectedCell, state =>
            {
                state.flipped = !state.flipped;
                return state;
            }, "翻转图块");
        }

        void MarkSelectedCorrupted()
        {
            Mutate(selectedCell, state => new CellState(PuzzleRules.CorruptedPiece), "标记错乱块");
        }

        void ResetToIdentity()
        {
            CellState[] cells = GetCells();
            if (cells == null) return;

            Undo.RecordObject(level, "重置为正解形态");
            for (int i = 0; i < cells.Length; i++) cells[i] = new CellState(i);
            EditorUtility.SetDirty(level);
            Repaint();
        }

        void ResetOrientations()
        {
            CellState[] cells = GetCells();
            if (cells == null) return;

            Undo.RecordObject(level, "全部摆正");
            for (int i = 0; i < cells.Length; i++)
            {
                CellState state = cells[i];
                state.turnSteps = 0;
                state.flipped = false;
                cells[i] = state;
            }

            EditorUtility.SetDirty(level);
            Repaint();
        }

        void RandomizePieces()
        {
            CellState[] cells = GetCells();
            if (cells == null) return;

            var pieces = new List<int>();

            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].IsCorrupted) pieces.Add(cells[i].pieceIndex);
            }

            Undo.RecordObject(level, "随机打乱位置");

            for (int i = pieces.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = pieces[i];
                pieces[i] = pieces[j];
                pieces[j] = tmp;
            }

            int cursor = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsCorrupted) continue;

                CellState state = cells[i];
                state.pieceIndex = pieces[cursor++];
                cells[i] = state;
            }

            EditorUtility.SetDirty(level);
            Repaint();
        }

        // ---------------------------------------------------------------- 小工具

        static Rect CellRect(Rect area, int g, int cell)
        {
            int col = cell % g;
            int row = cell / g;

            return new Rect(
                area.x + col * (CellSize + CellGap),
                area.y + row * (CellSize + CellGap),
                CellSize,
                CellSize);
        }

        static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }
    }
}
