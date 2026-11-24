using System;
using UnityEngine;
using Remiehzla.Puzzle;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 跨系统的信号总线。谁发、谁收都写在事件名里，订阅方自己去挂。
    ///
    /// 约定：
    ///   - 流程相关的事件由 <see cref="LevelDirector"/> 发出；
    ///   - 其他系统（收集品、可交互物）只发 ItemCollected / InteractableTriggered；
    ///   - 任何人都不应该在这些事件里改别人的状态，只做自己的反应。
    /// </summary>
    public static class GameEvents
    {
        // ── 由 LevelDirector 发出 ──────────────────────────────
        public static event Action<LevelDefinition> LevelStarted;
        public static event Action<PuzzleConfig> PuzzlePhaseEntered;
        public static event Action<PuzzleResult> PuzzleAccepted;
        public static event Action<PuzzleResult> PuzzleRejected;
        public static event Action<LevelContext> ExplorePhaseEntered;
        public static event Action<LevelDefinition> LevelCleared;

        // ── 由其他系统发出 ────────────────────────────────────
        public static event Action<string> ItemCollected;
        public static event Action<string> InteractableTriggered;

        public static void RaiseLevelStarted(LevelDefinition level) => LevelStarted?.Invoke(level);

        public static void RaisePuzzlePhaseEntered(PuzzleConfig config) => PuzzlePhaseEntered?.Invoke(config);

        public static void RaisePuzzleAccepted(PuzzleResult result) => PuzzleAccepted?.Invoke(result);

        public static void RaisePuzzleRejected(PuzzleResult result) => PuzzleRejected?.Invoke(result);

        public static void RaiseExplorePhaseEntered(LevelContext context) => ExplorePhaseEntered?.Invoke(context);

        public static void RaiseLevelCleared(LevelDefinition level) => LevelCleared?.Invoke(level);

        public static void RaiseItemCollected(string id) => ItemCollected?.Invoke(id);

        public static void RaiseInteractableTriggered(string id) => InteractableTriggered?.Invoke(id);

        /// <summary>
        /// 关闭 Domain Reload 时静态事件会跨 Play 残留，
        /// 这里在进入 Play 前清一遍，避免上一局的订阅者还挂着。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LevelStarted = null;
            PuzzlePhaseEntered = null;
            PuzzleAccepted = null;
            PuzzleRejected = null;
            ExplorePhaseEntered = null;
            LevelCleared = null;
            ItemCollected = null;
            InteractableTriggered = null;
        }
    }
}
