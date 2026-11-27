using System;
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

        /// <summary>本次运行是否已经点过「开始游戏」。</summary>
        public static bool HasStarted { get; private set; }

        /// <summary>点了开始游戏时触发。LevelDirector 靠它决定什么时候真正开始拼图。</summary>
        public static event Action Started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Context = null;
            PendingLevel = null;
            HasStarted = false;
            Started = null;
        }

        /// <summary>标记"已经开始了"，并广播一次（重复调用不会重复广播）。</summary>
        public static void MarkStarted()
        {
            if (HasStarted) return;

            HasStarted = true;
            Started?.Invoke();
        }

        /// <summary>重置本次运行的状态（结束界面"回到开始"用）。保留拼图场景名。</summary>
        public static void Reset()
        {
            Context = null;
            PendingLevel = null;
            HasStarted = false;
            Started = null;
        }

        /// <summary>确保有一个可用的上下文（探索场景单独打开调试时用）。</summary>
        public static LevelContext EnsureContext(LevelDefinition fallbackLevel = null)
        {
            if (Context == null) Context = new LevelContext { level = fallbackLevel };

            if (Context.level == null) Context.level = fallbackLevel;

            return Context;
        }

        /// <summary>把上下文切成新的一关（纯演出关直接进场景时用）。</summary>
        public static void StartNewContext(LevelDefinition level)
        {
            Context = new LevelContext { level = level };
        }
    }
}
