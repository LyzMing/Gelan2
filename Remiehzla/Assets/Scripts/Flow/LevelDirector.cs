using UnityEngine;
using Remiehzla.Puzzle;
using Remiehzla.UI;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 关卡流程状态机（最小版）：Puzzle → 判定 → Explore →（门）→ Cleared。
    ///
    /// 它只管两件事：
    ///   1. 切换「拼图根节点」和「探索根节点」的激活状态（两者互斥）；
    ///   2. 通过 <see cref="GameEvents"/> 广播阶段变化，让对话/角色/转场系统自己响应。
    ///
    /// 对话、转场、场景加载都还没接（等对方的系统就位，M2 补）。
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelDirector : MonoBehaviour
    {
        public enum Phase
        {
            None,
            Puzzle,
            Explore,
            Cleared
        }

        [Header("关卡数据")]
        [Tooltip("留空则用 PuzzleGrid 上挂的那个关卡资产")]
        public LevelDefinition level;

        [Header("场景节点（互斥，留空则跳过）")]
        [Tooltip("拼图阶段的根节点：棋盘 + 拼图 UI")]
        public GameObject puzzleRoot;

        [Tooltip("探索阶段的根节点：场景美术 + 角色 + 跟随相机 + 可交互物")]
        public GameObject exploreRoot;

        [Header("组件（留空会自动找）")]
        public PuzzleGrid grid;
        public PuzzlePhaseUI puzzleUI;

        /// <summary>当前阶段的运行时上下文。</summary>
        public LevelContext Context { get; private set; }

        public Phase Current { get; private set; } = Phase.None;

        public LevelDefinition Level => level;

        void Awake()
        {
            if (grid == null) grid = FindObjectOfType<PuzzleGrid>();
            if (puzzleUI == null) puzzleUI = FindObjectOfType<PuzzlePhaseUI>();

            // 以 PuzzleGrid 上挂的关卡为准（棋盘在 Awake 里已经按它建好了）
            if (level == null && grid != null) level = grid.level;

            GameEvents.ItemCollected += OnItemCollected;
        }

        void Start()
        {
            EnterPuzzle();
        }

        void OnDestroy()
        {
            GameEvents.ItemCollected -= OnItemCollected;

            if (puzzleUI == null) return;

            puzzleUI.Confirmed -= OnPuzzleConfirmed;
            puzzleUI.Rejected -= OnPuzzleRejected;
        }

        /// <summary>收集品统一记进本关上下文（存档由 ProgressService 负责）。</summary>
        void OnItemCollected(string itemId)
        {
            if (Context == null || string.IsNullOrEmpty(itemId)) return;
            if (Context.collected.Contains(itemId)) return;

            Context.collected.Add(itemId);
        }

        // ------------------------------------------------------------------ 阶段切换

        void EnterPuzzle()
        {
            Current = Phase.Puzzle;

            Context = new LevelContext { level = level };

            SetActive(puzzleRoot, true);
            SetActive(exploreRoot, false);

            if (puzzleUI != null)
            {
                // 先退订再订阅，避免重复触发
                puzzleUI.Confirmed -= OnPuzzleConfirmed;
                puzzleUI.Rejected -= OnPuzzleRejected;
                puzzleUI.Confirmed += OnPuzzleConfirmed;
                puzzleUI.Rejected += OnPuzzleRejected;

                puzzleUI.RefreshStaticTexts();
            }

            GameEvents.RaiseLevelStarted(level);
            GameEvents.RaisePuzzlePhaseEntered(level != null ? level.puzzle : null);

            Debug.Log($"[LevelDirector] 进入拼图阶段：{(level != null ? level.levelId : "（未配关卡）")}");
        }

        void OnPuzzleConfirmed(PuzzleResult result)
        {
            if (Current != Phase.Puzzle) return;

            Context.puzzle = result;

            GameEvents.RaisePuzzleAccepted(result);

            Debug.Log($"[LevelDirector] 判定通过：{result}");

            EnterExplore();
        }

        void OnPuzzleRejected(PuzzleResult result)
        {
            if (Current != Phase.Puzzle) return;

            GameEvents.RaisePuzzleRejected(result);

            Debug.Log($"[LevelDirector] 判定未通过：{result}");
        }

        void EnterExplore()
        {
            Current = Phase.Explore;

            SetActive(puzzleRoot, false);
            SetActive(exploreRoot, true);

            GameEvents.RaiseExplorePhaseEntered(Context);

            Debug.Log($"[LevelDirector] 进入探索阶段，solutionId = {Context.puzzle.solutionId}" +
                      $"{(Context.puzzle.isHidden ? "（隐藏解）" : "")}");
        }

        // ------------------------------------------------------------------ 对外

        /// <summary>门交互时调用：结束本关（转场由对方的系统负责）。</summary>
        public void NotifyDoorReached()
        {
            if (Current != Phase.Explore) return;

            Current = Phase.Cleared;

            GameEvents.RaiseLevelCleared(level);

            Debug.Log($"[LevelDirector] 本关结束：{(level != null ? level.levelId : "")}");
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
