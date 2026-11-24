using UnityEngine;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 门：交互后通知 <see cref="LevelDirector"/> 本关结束。
    /// 真正的转场/加载下一关由转场系统负责（它会订阅 LevelCleared 事件）。
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

        public override string Prompt => string.IsNullOrEmpty(promptOverride) ? "进入" : promptOverride;

        public override bool CanInteract
        {
            get
            {
                if (!base.CanInteract) return false;
                if (!requirePuzzleSolved) return true;

                LevelDirector target = ResolveDirector();

                return target != null && target.Current == LevelDirector.Phase.Explore;
            }
        }

        LevelDirector ResolveDirector()
        {
            if (director != null) return director;

            director = FindObjectOfType<LevelDirector>();

            return director;
        }

        protected override void OnInteract(GameObject interactor)
        {
            LevelDirector target = ResolveDirector();

            if (target == null)
            {
                Debug.LogWarning("[Door] 场景里没有 LevelDirector，无法过关");
                return;
            }

            Debug.Log($"[Door] 进入下一关：{(string.IsNullOrEmpty(targetSceneName) ? "（未配置目标场景）" : targetSceneName)}");

            target.NotifyDoorReached();
        }
    }
}
