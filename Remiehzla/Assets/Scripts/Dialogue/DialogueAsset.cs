using UnityEngine;

namespace Remiehzla.Dialogue
{
    /// <summary>一句字幕。</summary>
    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(2, 4)]
        [Tooltip("字幕正文。可以留空当停顿用")]
        public string text = "";

        [Min(0f)]
        [Tooltip("这句的打字速度（字/秒）。0 = 用播放器的默认值")]
        public float charsPerSecond;

        [Min(0f)]
        [Tooltip("这句播完后停留多久再进下一句。0 = 用播放器的默认值")]
        public float holdAfter;
    }

    /// <summary>
    /// 一段对话（字幕）。右键 Create → 剧情 → 对话 新建。
    ///
    /// 播放方式：屏幕下方逐字显示，一句播完停留一会儿自动进下一句，不需要玩家按键。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "剧情/对话", order = 0)]
    public class DialogueAsset : ScriptableObject
    {
        [Tooltip("调试用备注名，不显示在游戏里")]
        public string memo = "";

        [Tooltip("按顺序播放的字幕")]
        public DialogueLine[] lines = new DialogueLine[0];

        [Header("奖励（可选）")]
        [Tooltip("触发这段对话就获得的物品 id，例如 L01_KEY。留空 = 没有奖励")]
        public string rewardItemId = "";

        [Tooltip("提示里显示的名字，例如「老照片」。留空则显示 id")]
        public string rewardLabel = "";

        [Header("播放完之后")]
        [Tooltip("播完自动进入下一关（和走到门一样）。用于「看完这段就换场景」的剧情节点")]
        public bool goToNextLevelWhenFinished;

        public bool IsValid => lines != null && lines.Length > 0;

        public bool HasReward => !string.IsNullOrEmpty(rewardItemId);

        void OnValidate()
        {
            if (lines == null) lines = new DialogueLine[0];
        }
    }
}
