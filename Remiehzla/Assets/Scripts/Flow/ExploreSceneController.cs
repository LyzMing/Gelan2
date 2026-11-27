using UnityEngine;
using UnityEngine.SceneManagement;
using Remiehzla.Puzzle;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 探索场景的入口。
    ///
    /// 从 <see cref="GameSession"/> 接手本关上下文（拼图用的哪个解、已经捡了什么），
    /// 然后负责：广播"进入探索阶段"、收门的收尾、进下一关。
    ///
    /// 单独打开探索场景按 Play 也能跑：这时没有上下文，它会用 Inspector 上指定的关卡，
    /// 所有 ExploreVariant 保持你在编辑器里摆的状态（方便单独调场景）。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExploreSceneController : MonoBehaviour
    {
        [Header("关卡数据")]
        [Tooltip("单独调试探索场景时用。从拼图场景过来时，用 GameSession 里带过来的那一关")]
        public LevelDefinition level;

        [Header("流程")]
        [Tooltip("进下一关时加载的拼图场景名。留空则用 GameSession 里记的那个")]
        public string puzzleSceneName;

        [Tooltip("最后一关离场时启用的收尾演出，平时保持禁用")]
        public GameObject completionGroup;

        /// <summary>本关上下文（从拼图阶段带过来的）。</summary>
        public LevelContext Context { get; private set; }

        void Start()
        {
            // 没有拼图阶段传过来的数据 = 直接在这个场景按了 Play（单独调试）
            bool standalone = GameSession.Context == null;

            Context = GameSession.EnsureContext(level);

            // 从拼图场景过来时，以拼图阶段带过来的那一关为准（Inspector 上的只在单独调试时用）
            if (Context.level != null) level = Context.level;

            string solutionId = Context.puzzle.solutionId;

            Debug.Log($"[Explore] 进入 {DescribeLevel()}" +
                      (standalone ? "　⚠ 单独调试模式（没有从拼图阶段过来的数据，用的是 Inspector 上的 Level）" : "（从拼图阶段过来）") +
                      $"，解法 = {(string.IsNullOrEmpty(solutionId) ? "（无）" : solutionId)}");

            GameEvents.RaiseExplorePhaseEntered(Context);
        }

        string DescribeLevel()
        {
            return level != null ? $"{level.levelId}（{level.name}）" : "（未配关卡）";
        }

        /// <summary>
        /// 从任何地方结束本关（对话播完自动过关用）。
        /// 场景里没有控制器就什么都不做，只打一条警告。
        /// </summary>
        public static void LeaveCurrentScene()
        {
            var controller = FindObjectOfType<ExploreSceneController>();

            if (controller == null)
            {
                Debug.LogWarning("[Explore] 想结束本关，但当前场景没有 ExploreSceneController");
                return;
            }

            controller.RequestLeaveLevel();
        }

        /// <summary>门交互时调用：结束本关，加载下一关的拼图场景。</summary>
        public void RequestLeaveLevel()
        {
            GameEvents.RaiseLevelCleared(level);

            if (level == null)
            {
                Debug.LogWarning("[Explore] 没有关卡数据，无法判断下一关。" +
                                 "检查拼图阶段有没有正常传 GameSession.Context，或者在这个场景的 ExploreSceneController 上填 Level", this);
                return;
            }

            if (level.nextLevel == null)
            {
                if (completionGroup != null)
                {
                    completionGroup.SetActive(true);
                    return;
                }

                Debug.LogWarning($"[Explore] 当前关是 {DescribeLevel()}，它的 Next Level 为空 → 当作「最后一关」处理，流程停在这里。\n" +
                                 "如果它其实不是最后一关，检查两件事：\n" +
                                 "  1. 本场景 ExploreSceneController 的 Level 填的是【本关】而不是【下一关】\n" +
                                 "  2. 这个关卡资产的 Next Level 指向下一关", this);
                return;
            }

            LevelDefinition next = level.nextLevel;

            // 下一关是纯演出关（没有拼图配置）：不经过拼图场景，直接进它的场景
            if (next.puzzle == null)
            {
                string cutsceneScene = next.exploreSceneName;

                if (string.IsNullOrEmpty(cutsceneScene))
                {
                    Debug.LogError($"[Explore] 下一关 {next.levelId} 没有拼图配置，但也没填 Explore Scene Name，" +
                                   "不知道该加载哪个场景", this);
                    return;
                }

                Debug.Log($"[Explore] 本关结束 → 下一关 {next.levelId} 是演出关，直接加载 {cutsceneScene}");

                GameSession.StartNewContext(next);

                SceneFlow.Load(cutsceneScene);
                return;
            }

            GameSession.PendingLevel = next;

            string target = string.IsNullOrEmpty(puzzleSceneName)
                ? GameSession.PuzzleSceneName
                : puzzleSceneName;

            GameSession.PuzzleSceneName = target;

            Debug.Log($"[Explore] 本关结束 → 下一关 {next.levelId}，加载拼图场景 {target}");

            SceneFlow.Load(target);
        }
    }
}
