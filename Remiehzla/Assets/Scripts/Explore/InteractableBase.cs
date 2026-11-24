using UnityEngine;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 可交互物基类：统一处理"能不能交互"、埋点事件和提示文字。
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

        /// <summary>运行时开关，给机关/条件用。</summary>
        public bool Disabled { get; set; }

        public virtual string Prompt => string.IsNullOrEmpty(promptOverride) ? "交互" : promptOverride;

        public virtual bool CanInteract =>
            isActiveAndEnabled && !Disabled && !startDisabled;

        public virtual Transform HighlightTarget => highlightTarget != null ? highlightTarget : transform;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            if (!string.IsNullOrEmpty(interactableId))
                GameEvents.RaiseInteractableTriggered(interactableId);

            OnInteract(interactor);
        }

        protected abstract void OnInteract(GameObject interactor);
    }
}
