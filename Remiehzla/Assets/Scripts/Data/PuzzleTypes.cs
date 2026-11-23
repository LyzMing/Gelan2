using System;
using UnityEngine;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 一关允许的操作。按策划案的逐关解锁顺序勾选。
    /// </summary>
    [Flags]
    public enum PuzzleOps
    {
        None = 0,

        /// <summary>左键点选。</summary>
        Select = 1 << 0,

        /// <summary>↑↓←→ 与相邻格交换。</summary>
        SwapAdjacent = 1 << 1,

        /// <summary>锁定 A 再选 B，任意两块互换（第 3 关 B 键）。</summary>
        SwapAny = 1 << 2,

        /// <summary>H / K 旋转 90°。</summary>
        Rotate = 1 << 3,

        /// <summary>J 水平镜像翻转。</summary>
        Flip = 1 << 4,

        /// <summary>C 复制 + 左键放下覆盖。</summary>
        Copy = 1 << 5,

        /// <summary>左键拖拽交换。</summary>
        Drag = 1 << 6,

        /// <summary>关卡内存在错乱块（影响显示与判定，不拦截输入）。</summary>
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

        public CellState[] cells = new CellState[0];

        public PuzzleSolution() { }

        public PuzzleSolution(int count)
        {
            cells = new CellState[count];
            for (int i = 0; i < count; i++) cells[i] = new CellState(i);
        }
    }
}
