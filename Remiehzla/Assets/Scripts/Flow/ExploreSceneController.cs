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

        /// <summary>本关上下文（从拼图阶段带过来的）。</summary>
        public LevelContext Context { get; private set; }

        void Start()
        {
            Context = GameSession.EnsureContext(level);

            if (level == null) level = Context.level;

            string solutionId = Context.puzzle.solutionId;

            Debug.Log($"[Explore] 进入 {(level != null ? level.levelId : "（未配关卡）")}，" +
                      $"解法 = {(string.IsNullOrEmpty(solutionId) ? "（无，单独调试）" : solutionId)}");

            GameEvents.RaiseExplorePhaseEntered(Context);
        }

        /// <summary>门交互时调用：结束本关，加载下一关的拼图场景。</summary>
        public void RequestLeaveLevel()
        {
            GameEvents.RaiseLevelCleared(level);

            if (level == null || level.nextLevel == null)
            {
                Debug.LogWarning("[Explore] 没有配置下一关（LevelDefinition.nextLevel 为空），流程停在这里");
                return;
            }

            GameSession.PendingLevel = level.nextLevel;

            string target = string.IsNullOrEmpty(puzzleSceneName)
                ? GameSession.PuzzleSceneName
                : puzzleSceneName;

            GameSession.PuzzleSceneName = target;

            Debug.Log($"[Explore] 本关结束 → 下一关 {level.nextLevel.levelId}，加载拼图场景 {target}");

            SceneManager.LoadScene(target);
        }
    }
}
