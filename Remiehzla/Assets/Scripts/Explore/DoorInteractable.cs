using UnityEngine;
using Remiehzla.Flow;
using Remiehzla.Dialogue;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 门：交互后结束本关。
    ///
    /// 两种接法都支持：
    ///   · 探索场景里有 <see cref="ExploreSceneController"/> → 交给它（加载下一关的拼图场景）
    ///   · 探索内容和拼图在同一个场景里（旧模式）→ 交给 LevelDirector
    /// </summary>
    public class DoorInteractable : InteractableBase
    {
        [Header("门")]
        [Tooltip("留空会自动在场景里找")]
        public LevelDirector director;

        [Tooltip("下一关场景名（给转场系统用，本组件只负责把 id 放进事件里）")]
        public string targetSceneName;

        [Tooltip("要求拼图阶段已经通过才能进")]
        public bool requirePuzzleSolved = true;

        [Header("开门前")]
        [Tooltip("开门前先播这段字幕，播完才换场景。留空 = 直接开门")]
        public DialogueAsset openDialogue;

        [Tooltip("拿到此物品时跳过开门对白，留空则照常播放")]
        public string skipDialogueWithItem;

        [Tooltip("播字幕期间禁用玩家移动")]
        public bool lockMovementWhileTalking = true;

        ExploreSceneController exploreController;
        bool searchedExploreController;

        public override string Prompt => string.IsNullOrEmpty(promptOverride) ? "进入" : promptOverride;

        public override bool CanInteract
        {
            get
            {
                if (!base.CanInteract) return false;
                if (!requirePuzzleSolved) return true;

                // 跨场景模式：探索场景里有控制器，说明现在本来就是探索阶段
                if (ResolveExploreController() != null) return true;

                LevelDirector target = ResolveDirector();

                return target != null && target.Current == LevelDirector.Phase.Explore;
            }
        }

        ExploreSceneController ResolveExploreController()
        {
            if (searchedExploreController) return exploreController;

            searchedExploreController = true;
            exploreController = FindObjectOfType<ExploreSceneController>();

            return exploreController;
        }

        LevelDirector ResolveDirector()
        {
            if (director != null) return director;

            director = FindObjectOfType<LevelDirector>();

            return director;
        }

        protected override void OnInteract(GameObject interactor)
        {
            // 没配开门前的对话 → 直接过关
            if (openDialogue == null || (!string.IsNullOrEmpty(skipDialogueWithItem) && ProgressService.HasCollected(skipDialogueWithItem)))
            {
                LeaveLevel();
                return;
            }

            // 保险：正在播别的对话就先别动
            if (SubtitlePlayer.IsAnyPlaying) return;

            bool started = SubtitlePlayer.Instance.Play(
                openDialogue,
                LeaveLevel,
                lockMovementWhileTalking);

            if (started)
            {
                Debug.Log($"[Door] {name} 播放开门前的对话，播完再过关");
                return;
            }

            // 播不起来（数据为空之类）→ 别把玩家卡在门口
            LeaveLevel();
        }

        void LeaveLevel()
        {
            ExploreSceneController controller = ResolveExploreController();

            if (controller != null)
            {
                controller.RequestLeaveLevel();
                return;
            }

            LevelDirector target = ResolveDirector();

            if (target == null)
            {
                Debug.LogWarning("[Door] 场景里既没有 ExploreSceneController 也没有 LevelDirector，无法过关");
                return;
            }

            Debug.Log($"[Door] 进入下一关：{(string.IsNullOrEmpty(targetSceneName) ? "（未配置目标场景）" : targetSceneName)}");

            target.NotifyDoorReached();
        }
    }
}
