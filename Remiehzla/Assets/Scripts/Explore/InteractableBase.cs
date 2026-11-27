using UnityEngine;
using Remiehzla.Flow;
using Remiehzla.Dialogue;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 可交互物基类：统一处理"能不能交互"、条件判断、埋点事件和提示文字。
    /// 具体行为在子类的 <see cref="OnInteract"/> 里实现。
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("通用")]
        [Tooltip("埋点/条件用的 id，可留空")]
        public string interactableId;

        [Tooltip("提示文字，留空用子类的默认值")]
        public string promptOverride;

        [Tooltip("高亮锚点。留空 = 用自己")]
        public Transform highlightTarget;

        [Tooltip("初始就禁用交互（可以让别的脚本/机关打开）")]
        public bool startDisabled;

        [Header("条件")]
        [Tooltip("需要先拿到这些物品（全部拿到才满足）。留空 = 无条件")]
        public string[] requiredItems = new string[0];

        [Tooltip("条件不满足时按 E 弹出的提示")]
        public string conditionLockedHint = "暂未满足条件";

        /// <summary>运行时开关，给机关/条件用。</summary>
        public bool Disabled { get; set; }

        /// <summary>条件是否满足（列表里的物品全部拿到）。提示照常显示，只有真正交互时才拦。</summary>
        public virtual bool ConditionMet => ProgressService.HasCollectedAll(requiredItems);

        public virtual string Prompt => string.IsNullOrEmpty(promptOverride) ? "交互" : promptOverride;

        public virtual bool CanInteract =>
            isActiveAndEnabled && !Disabled && !startDisabled &&
            !SubtitlePlayer.IsAnyPlaying &&    // 字幕播放期间锁住所有交互（移动不受影响）
            !SceneFlow.IsTransitioning;        // 转场期间不响应（提示也不会冒出来）

        public virtual Transform HighlightTarget => highlightTarget != null ? highlightTarget : transform;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            // 条件不满足：提示照常出现，但按下去只弹一句话，不做任何事
            if (!ConditionMet)
            {
                SubtitlePlayer.ShowHint(string.IsNullOrEmpty(conditionLockedHint) ? "暂未满足条件" : conditionLockedHint);

                Debug.Log($"[Interactable] {name} 条件未满足，还缺：{ProgressService.DescribeMissing(requiredItems)}");

                return;
            }

            if (!string.IsNullOrEmpty(interactableId))
                GameEvents.RaiseInteractableTriggered(interactableId);

            OnInteract(interactor);
        }

        protected abstract void OnInteract(GameObject interactor);
    }
}
