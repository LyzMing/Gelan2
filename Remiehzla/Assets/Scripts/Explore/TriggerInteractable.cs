using UnityEngine;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 通用触发器：不做具体行为，只发一个带 id 的事件，由别的系统接。
    ///
    /// 适合：地板机关、剧情触发点、以及"对话点"——等对话系统就位后，
    /// 把它的对话入口接到 GameEvents.InteractableTriggered 上就行。
    ///
    /// 也可以直接勾 useAsFlag 记一个存档 flag（不需要背包的那种"来过/看过"）。
    /// </summary>
    public class TriggerInteractable : InteractableBase
    {
        [Header("触发")]
        [Tooltip("触发后记一个存档 flag（可选）")]
        public bool useAsFlag;

        [Tooltip("flag id，留空则用 interactableId")]
        public string flagId;

        [Tooltip("触发后是否只生效一次")]
        public bool once = true;

        bool fired;

        public override string Prompt => string.IsNullOrEmpty(promptOverride) ? "查看" : promptOverride;

        public override bool CanInteract => base.CanInteract && (!once || !fired);

        protected override void OnInteract(GameObject interactor)
        {
            fired = true;

            if (useAsFlag)
            {
                string id = string.IsNullOrEmpty(flagId) ? interactableId : flagId;
                if (!string.IsNullOrEmpty(id)) ProgressService.Collect(id);
            }

            if (string.IsNullOrEmpty(interactableId))
                Debug.LogWarning($"[Trigger] {name} 的 interactableId 为空，事件订阅方收不到", this);
        }
    }
}
