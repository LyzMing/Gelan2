using System;
using UnityEngine;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 一关允许的操作。逐关在 PuzzleConfig.allowedOps 里勾选，代码不做任何硬编码。
    /// </summary>
    [Flags]
    public enum PuzzleOps
    {
        None = 0,

        /// <summary>左键点选。</summary>
        [InspectorName("点选")]
        Select = 1 << 0,

        /// <summary>↑↓←→ 与相邻格交换。</summary>
        [InspectorName("平移（方向键交换相邻格）")]
        SwapAdjacent = 1 << 1,

        /// <summary>H / K 旋转 90°。</summary>
        [InspectorName("旋转（H/K）")]
        Rotate = 1 << 2,

        /// <summary>J 水平镜像翻转。</summary>
        [InspectorName("水平翻转（J）")]
        Flip = 1 << 3,

        /// <summary>左键拖拽交换。</summary>
        [InspectorName("拖动")]
        Drag = 1 << 4,

        /// <summary>C 复制 + 左键放下覆盖。</summary>
        [InspectorName("复制（C）")]
        Copy = 1 << 5,

        /// <summary>Z 撤销上一步。</summary>
        [InspectorName("撤回（Z）")]
        Undo = 1 << 6,

        /// <summary>关卡内存在错乱块（影响显示与判定，不拦截输入）。</summary>
        [InspectorName("错乱块")]
        Corrupted = 1 << 7,
    }

    /// <summary>
    /// 单个格子的状态：装的是原图第几块、转了几圈、有没有镜像。
    /// 初始布局、正解布局、运行时快照都用这个结构描述。
    /// </summary>
    [Serializable]
    public struct CellState
    {
        [Tooltip("原图第几块（0 = 左上角，行优先）。-1 = 错乱块（不显示图块，可被复制覆盖）")]
        public int pieceIndex;

        [Range(0, 3)]
        [Tooltip("顺时针转了几个 90°")]
        public int turnSteps;

        [Tooltip("是否水平镜像")]
        public bool flipped;

        public CellState(int pieceIndex, int turnSteps = 0, bool flipped = false)
        {
            this.pieceIndex = pieceIndex;
            this.turnSteps = turnSteps;
            this.flipped = flipped;
        }

        public bool IsCorrupted => pieceIndex < 0;

        public bool SameAs(CellState other)
        {
            return pieceIndex == other.pieceIndex
                   && turnSteps == other.turnSteps
                   && flipped == other.flipped;
        }

        public override string ToString()
        {
            if (IsCorrupted) return "X";

            string s = pieceIndex.ToString();
            if (turnSteps != 0) s += "↻" + turnSteps;
            if (flipped) s += "⇄";
            return s;
        }
    }

    /// <summary>
    /// 一整张布局 = 每个格子一个状态，行优先、长度 = gridSize²。
    /// </summary>
    [Serializable]
    public class PuzzleLayout
    {
        public CellState[] cells = new CellState[0];

        public PuzzleLayout() { }

        public PuzzleLayout(int count)
        {
            cells = new CellState[count];
            for (int i = 0; i < count; i++) cells[i] = new CellState(i);
        }
    }

    /// <summary>
    /// 一个正解。solutionId 会传给探索阶段做分支（例如第 4 关的隐藏门）。
    /// </summary>
    [Serializable]
    public class PuzzleSolution
    {
        [Tooltip("传给探索阶段的分支键，例如 normal / hidden_twins")]
        public string solutionId = "normal";

        [Tooltip("隐藏解：不参与常规通关，用于解锁隐藏内容")]
        public bool isHidden;

        [Tooltip("这个解对应的探索场景名。留空 = 用关卡的默认探索场景")]
        public string exploreSceneName = "";

        public CellState[] cells = new CellState[0];

        [Tooltip("只判定这些格子（从左上角 0 开始）。留空时判定全盘。")]
        public int[] requiredCells;

        public PuzzleSolution() { }

        public PuzzleSolution(int count)
        {
            cells = new CellState[count];
            for (int i = 0; i < count; i++) cells[i] = new CellState(i);
        }
    }

    /// <summary>
    /// 玩家点"确定"后得到的判定结果。solutionId 会传给探索阶段做分支。
    /// </summary>
    [Serializable]
    public struct PuzzleResult
    {
        public string levelId;
        public string solutionId;
        public bool isHidden;
        public bool solved;
        public int moveCount;

        public override string ToString()
        {
            return solved
                ? $"{levelId}: 通过 {solutionId}{(isHidden ? "（隐藏解）" : "")}，{moveCount} 次交换"
                : $"{levelId}: 未通过，{moveCount} 次交换";
        }
    }
}
