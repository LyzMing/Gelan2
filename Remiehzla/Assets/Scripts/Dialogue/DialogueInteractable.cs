using UnityEngine;
using Remiehzla.Flow;
using Remiehzla.Explore;

namespace Remiehzla.Dialogue
{
    /// <summary>
    /// 交互触发对话：走近按 E 看一段字幕。
    ///
    /// · 播放期间**锁住玩家移动**（和区域触发相反）
    /// · 播放期间所有可交互物（包括自己）都被锁住，播完才能继续
    /// · 只会触发一次
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueInteractable : InteractableBase
    {
        [Header("对话")]
        public DialogueAsset dialogue;

        [Tooltip("只能触发一次")]
        public bool oneShot = true;

        [Tooltip("播放期间禁用玩家移动")]
        public bool lockMovement = true;

        bool played;

        public override string Prompt =>
            !string.IsNullOrEmpty(promptOverride) ? promptOverride : "查看";

        public override bool CanInteract =>
            base.CanInteract && !(oneShot && played);

        protected override void OnInteract(GameObject interactor)
        {
            if (dialogue == null)
            {
                Debug.LogWarning($"[DialogueInteractable] {name} 没有配对话数据", this);
                return;
            }

            System.Action onComplete = null;

            if (dialogue.goToNextLevelWhenFinished)
                onComplete = ExploreSceneController.LeaveCurrentScene;

            if (SubtitlePlayer.Instance.Play(dialogue, onComplete, lockMovement)) played = true;
        }
    }
}
