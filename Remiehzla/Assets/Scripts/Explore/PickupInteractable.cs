using UnityEngine;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 可拾取物：交互一次记进存档、发事件、然后隐藏。
    /// 没有背包，只记 flag。id 用 "L01_PHOTO" 这种带关卡前缀的写法。
    /// </summary>
    public class PickupInteractable : InteractableBase
    {
        [Header("拾取")]
        [Tooltip("物品 id，建议 L01_PHOTO 这种格式")]
        public string itemId;

        [Tooltip("捡完是否隐藏物体")]
        public bool hideAfterPickup = true;

        public override string Prompt => string.IsNullOrEmpty(promptOverride) ? "捡起" : promptOverride;

        public override bool CanInteract =>
            base.CanInteract && !string.IsNullOrEmpty(itemId) && !ProgressService.HasCollected(itemId);

        protected override void OnInteract(GameObject interactor)
        {
            if (!ProgressService.Collect(itemId)) return;

            GameEvents.RaiseItemCollected(itemId);

            Debug.Log($"[Pickup] 获得 {itemId}");

            if (hideAfterPickup) gameObject.SetActive(false);
        }
    }
}
