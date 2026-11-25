using UnityEngine;
using Remiehzla.Puzzle;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 跨场景传数据的小黑板。
    ///
    /// 拼图场景把结果放进来 → 加载探索场景 → 探索场景从里面读。
    /// 只放"这一关必须传过去的东西"，别的都不要往这里塞。
    ///
    /// 注意：静态字段在切场景时不会丢（这正是它能用的原因）；
    /// 每次进 Play 会被下面的方法重置成干净的。
    /// </summary>
    public static class GameSession
    {
        /// <summary>当前关的上下文：关卡资产、拼图判定结果、本关已收集。</summary>
        public static LevelContext Context;

        /// <summary>下一关要打哪个关卡资产。拼图场景启动时读它决定用哪套图。</summary>
        public static LevelDefinition PendingLevel;

        /// <summary>拼图场景名（一个拼图场景复用于所有关卡）。</summary>
        public static string PuzzleSceneName = "Puzzle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Context = null;
            PendingLevel = null;
        }

        /// <summary>确保有一个可用的上下文（探索场景单独打开调试时用）。</summary>
        public static LevelContext EnsureContext(LevelDefinition fallbackLevel = null)
        {
            if (Context == null) Context = new LevelContext { level = fallbackLevel };

            if (Context.level == null) Context.level = fallbackLevel;

            return Context;
        }
    }
}
