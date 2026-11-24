using System.Collections.Generic;
using Remiehzla.Puzzle;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 一局关卡的运行时上下文。拼图阶段的产出（solutionId）靠它传给探索阶段。
    ///
    /// 典型用法（探索阶段的控制器）：
    ///   var ctx = FindObjectOfType&lt;LevelDirector&gt;().Context;
    ///   if (ctx.puzzle.solutionId == "hidden_twins") { 生成隐藏内容 }
    /// </summary>
    public class LevelContext
    {
        /// <summary>当前关卡资产。</summary>
        public LevelDefinition level;

        /// <summary>拼图阶段的判定结果（没拼完时 solutionId 为空）。</summary>
        public PuzzleResult puzzle;

        /// <summary>本关已收集的物品 id（同时会写进全局存档）。</summary>
        public readonly List<string> collected = new List<string>();

        /// <summary>拼图阶段是否已经通过。</summary>
        public bool PuzzleSolved => puzzle.solved;

        public bool Has(string collectibleId)
        {
            return collected.Contains(collectibleId);
        }
    }
}
