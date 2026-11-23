using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 拼图的纯逻辑：布局比对、求解匹配、配置校验。
    /// 这里不碰任何 Unity 运行时对象，方便以后直接写单元测试。
    /// </summary>
    public static class PuzzleRules
    {
        public const int CorruptedPiece = -1;

        public static int CellCount(int gridSize)
        {
            return Mathf.Max(0, gridSize) * Mathf.Max(0, gridSize);
        }

        /// <summary>两个布局逐格完全一致（图块 + 旋转 + 镜像）。</summary>
        public static bool LayoutMatches(CellState[] cells, CellState[] target)
        {
            if (cells == null || target == null) return false;
            if (cells.Length != target.Length) return false;

            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].SameAs(target[i])) return false;
            }

            return true;
        }

        /// <summary>
        /// 找出匹配的正解，返回 solutionId；没有匹配返回 null。
        /// 先比普通解、再比隐藏解 —— 隐藏解不应该抢掉常规通关。
        /// </summary>
        public static string MatchSolution(PuzzleSolution[] solutions, CellState[] cells, out bool isHidden)
        {
            isHidden = false;

            if (solutions == null || cells == null) return null;

            for (int pass = 0; pass < 2; pass++)
            {
                bool wantHidden = pass == 1;

                for (int i = 0; i < solutions.Length; i++)
                {
                    PuzzleSolution solution = solutions[i];
                    if (solution == null) continue;
                    if (solution.isHidden != wantHidden) continue;
                    if (!LayoutMatches(cells, solution.cells)) continue;

                    isHidden = solution.isHidden;

                    return string.IsNullOrEmpty(solution.solutionId) ? "normal" : solution.solutionId;
                }
            }

            return null;
        }

        /// <summary>是不是"每块都回到自己的格子、且全部正立"（原型模式用的判定）。</summary>
        public static bool IsIdentity(CellState[] cells)
        {
            if (cells == null) return false;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].pieceIndex != i) return false;
                if (cells[i].turnSteps != 0) return false;
                if (cells[i].flipped) return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- 校验

        /// <summary>
        /// 检查一份拼图配置是否可用。errors 非空就说明策划配错了。
        /// 编辑器菜单"工具/校验关卡定义"会把这里的结果打出来。
        /// </summary>
        public static void Validate(PuzzleConfig config, List<string> errors, List<string> warnings)
        {
            if (config == null)
            {
                errors.Add("PuzzleConfig 为空");
                return;
            }

            if (config.image == null) errors.Add("没有指定拼图整图（image）");

            if (config.gridSize < 2 || config.gridSize > 8)
                errors.Add($"gridSize = {config.gridSize}，只允许 2~8");

            int count = CellCount(config.gridSize);

            // ---- 图片尺寸 ----
            if (config.image != null)
            {
                if (config.image.width != config.image.height)
                    warnings.Add($"拼图整图不是正方形（{config.image.width}×{config.image.height}），切图会变形");

                if (config.gridSize > 0 && config.image.width % config.gridSize != 0)
                    warnings.Add($"图片宽度 {config.image.width} 不能被 {config.gridSize} 整除，边缘可能错位");
            }

            // ---- 初始布局 ----
            bool hasCorrupted = false;

            if (config.initial == null || config.initial.cells == null || config.initial.cells.Length == 0)
            {
                warnings.Add("没有配置初始布局，运行时会退回随机打乱（正式关卡应该手摆）");
            }
            else if (config.initial.cells.Length != count)
            {
                errors.Add($"初始布局有 {config.initial.cells.Length} 格，应该是 {count} 格");
            }
            else
            {
                hasCorrupted |= ContainsCorrupted(config.initial.cells);
                CheckPermutation(config.initial.cells, count, "初始布局", errors);
            }

            // ---- 正解 ----
            if (config.solutions == null || config.solutions.Length == 0)
            {
                errors.Add("没有配置任何正解（solutions 为空）");
            }
            else
            {
                bool hasNormal = false;
                var ids = new HashSet<string>();

                for (int i = 0; i < config.solutions.Length; i++)
                {
                    PuzzleSolution solution = config.solutions[i];
                    string label = $"解 #{i}";

                    if (solution == null)
                    {
                        errors.Add($"{label} 是空引用");
                        continue;
                    }

                    if (string.IsNullOrEmpty(solution.solutionId))
                        errors.Add($"{label} 的 solutionId 为空");
                    else
                    {
                        label = $"解 \"{solution.solutionId}\"";
                        if (!ids.Add(solution.solutionId))
                            errors.Add($"solutionId \"{solution.solutionId}\" 重复了");
                    }

                    if (!solution.isHidden) hasNormal = true;

                    if (solution.cells == null || solution.cells.Length != count)
                    {
                        errors.Add($"{label} 有 {(solution.cells == null ? 0 : solution.cells.Length)} 格，应该是 {count} 格");
                        continue;
                    }

                    hasCorrupted |= ContainsCorrupted(solution.cells);

                    for (int c = 0; c < solution.cells.Length; c++)
                    {
                        int piece = solution.cells[c].pieceIndex;
                        if (piece < CorruptedPiece || piece >= count)
                            errors.Add($"{label} 第 {c} 格的 pieceIndex = {piece}，超出范围（-1 或 0~{count - 1}）");
                    }

                    if (!AllowsCopy(config.allowedOps) && HasDuplicatePiece(solution.cells))
                        warnings.Add($"{label} 里出现了重复图块，但这一关没有开启复制操作");
                }

                if (!hasNormal) errors.Add("所有正解都标记成了隐藏解，没有常规通关解");
            }

            // ---- 操作集与错乱块的一致性 ----
            if (hasCorrupted && (config.allowedOps & PuzzleOps.Corrupted) == 0)
                warnings.Add("数据里存在错乱块，但 allowedOps 没有勾 Corrupted");

            if (!hasCorrupted && (config.allowedOps & PuzzleOps.Corrupted) != 0)
                warnings.Add("allowedOps 勾了 Corrupted，但数据里没有任何错乱块");
        }

        public static bool AllowsCopy(PuzzleOps ops)
        {
            return (ops & PuzzleOps.Copy) != 0;
        }

        static bool ContainsCorrupted(CellState[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsCorrupted) return true;
            }

            return false;
        }

        static bool HasDuplicatePiece(CellState[] cells)
        {
            var seen = new HashSet<int>();

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsCorrupted) continue;
                if (!seen.Add(cells[i].pieceIndex)) return true;
            }

            return false;
        }

        /// <summary>初始布局必须是 0..count-1 的完整排列（错乱块可以重复）。</summary>
        static void CheckPermutation(CellState[] cells, int count, string label, List<string> errors)
        {
            var used = new int[count];

            for (int i = 0; i < cells.Length; i++)
            {
                int piece = cells[i].pieceIndex;

                if (piece == CorruptedPiece) continue;

                if (piece < 0 || piece >= count)
                {
                    errors.Add($"{label} 第 {i} 格的 pieceIndex = {piece}，超出范围");
                    continue;
                }

                used[piece]++;
            }

            var missing = new StringBuilder();
            var repeated = new StringBuilder();

            for (int p = 0; p < count; p++)
            {
                if (used[p] == 0) missing.Append(p).Append(' ');
                else if (used[p] > 1) repeated.Append(p).Append($"×{used[p]} ");
            }

            if (missing.Length > 0) errors.Add($"{label} 缺少这些图块：{missing}");
            if (repeated.Length > 0) errors.Add($"{label} 这些图块重复了：{repeated}");
        }
    }
}
