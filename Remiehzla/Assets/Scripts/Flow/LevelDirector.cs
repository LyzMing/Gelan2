using UnityEngine;
using UnityEngine.SceneManagement;
using Remiehzla.Puzzle;
using Remiehzla.UI;
using Remiehzla.Dialogue;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 拼图场景的流程入口：Puzzle → 判定 → 进探索。
    ///
    /// 两种接法（看 LevelDefinition.exploreSceneName）：
    ///   · 填了场景名 → 判定通过后加载那个探索场景（推荐，一关一个探索场景）
    ///   · 留空 → 在本场景里关掉 puzzleRoot、打开 exploreRoot（旧模式，仍可用）
    ///
    /// 跨场景的数据通过 <see cref="GameSession"/> 传，探索场景那边由
    /// <see cref="ExploreSceneController"/> 接手。
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
        [Tooltip("留空则用 PuzzleGrid 上挂的那个关卡资产；从上一关过来时优先用 GameSession.PendingLevel")]
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
            // 从上一关过来时，由 GameSession 指定这次打哪一关
            // （这里不要清空 PendingLevel：组件 Awake 顺序不确定，PuzzleGrid 也要读它）
            if (GameSession.PendingLevel != null) level = GameSession.PendingLevel;

            if (grid == null) grid = FindObjectOfType<PuzzleGrid>();
            if (puzzleUI == null) puzzleUI = FindObjectOfType<PuzzlePhaseUI>();

            // 以 PuzzleGrid 上挂的关卡为准（棋盘在 Awake 里已经按它建好了）
            if (level == null && grid != null) level = grid.level;

            GameEvents.ItemCollected += OnItemCollected;
        }

        void Start()
        {
            // 场景里有开始界面、而且本次运行还没开始过 → 等玩家点「开始游戏」再进拼图
            bool waitForStartMenu = !GameSession.HasStarted && FindObjectOfType<UI.StartMenu>() != null;

            if (waitForStartMenu)
            {
                GameSession.Started += OnGameStarted;

                Debug.Log("[LevelDirector] 等待开始界面");

                return;
            }

            EnterPuzzle();
        }

        void OnGameStarted()
        {
            GameSession.Started -= OnGameStarted;

            EnterPuzzle();
        }

        void OnDestroy()
        {
            GameEvents.ItemCollected -= OnItemCollected;
            GameSession.Started -= OnGameStarted;

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
            // 纯演出关（没有拼图配置）：跳过拼图阶段，直接进它的场景
            if (level != null && level.puzzle == null)
            {
                Context = new LevelContext { level = level };

                Debug.Log($"[LevelDirector] {level.levelId} 没有拼图配置（演出关），跳过拼图");

                EnterExplore();
                return;
            }

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

            PlayPuzzleIntroDialogue();
        }

        /// <summary>拼图环节的开场对话：播完之前不让操作拼图。</summary>
        void PlayPuzzleIntroDialogue()
        {
            if (level == null || level.puzzleDialogue == null) return;

            SubtitlePlayer subtitles = SubtitlePlayer.Instance;

            if (subtitles == null) return;

            if (grid != null) grid.SetInputEnabled(false);
            if (puzzleUI != null) puzzleUI.SetButtonsEnabled(false);

            bool started = subtitles.Play(level.puzzleDialogue, () =>
            {
                if (grid != null) grid.SetInputEnabled(true);
                if (puzzleUI != null) puzzleUI.SetButtonsEnabled(true);
            }, lockMovement: true);

            if (started)
            {
                Debug.Log("[LevelDirector] 播放拼图环节的开场对话，拼图暂时锁定");

                return;
            }

            // 已经有别的对话在播：先别锁死，免得玩家卡住
            if (grid != null) grid.SetInputEnabled(true);
            if (puzzleUI != null) puzzleUI.SetButtonsEnabled(true);
        }

        void OnPuzzleConfirmed(PuzzleResult result)
        {
            if (Current != Phase.Puzzle) return;

            Context.puzzle = result;

            // 存进跨场景的小黑板：探索场景要靠它拿到 solutionId
            GameSession.Context = Context;
            GameSession.PendingLevel = level;

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

            // 不同解法可以跳到不同的探索场景（隐藏解专用场景）
            string exploreScene = level != null
                ? level.ResolveExploreScene(Context != null ? Context.puzzle.solutionId : null)
                : null;

            // 配了探索场景名 → 加载那个场景（拼图场景整场卸载，数据靠 GameSession 带过去）
            if (!string.IsNullOrEmpty(exploreScene))
            {
                Debug.Log($"[LevelDirector] 解法「{Context.puzzle.solutionId}」→ 加载探索场景：{exploreScene}");

                GameSession.PuzzleSceneName = gameObject.scene.name;

                SceneFlow.Load(exploreScene);
                return;
            }

            // 旧模式：在本场景里切换根节点
            SetActive(puzzleRoot, false);
            SetActive(exploreRoot, true);

            GameEvents.RaiseExplorePhaseEntered(Context);

            Debug.Log($"[LevelDirector] 进入探索阶段（同场景），solutionId = {Context.puzzle.solutionId}" +
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
