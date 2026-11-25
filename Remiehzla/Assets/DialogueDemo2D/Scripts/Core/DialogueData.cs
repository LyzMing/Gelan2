using System.Collections.Generic;
using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 一段对话的数据资产。右键 Create → Dialogue Demo → Dialogue Data 即可新建。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue Demo/Dialogue Data", order = 0)]
    public class DialogueData : ScriptableObject
    {
        [Tooltip("开发用的备注名，不会显示在游戏里")]
        public string title = "未命名对话";

        [Tooltip("勾选后，这段对话只会被同一个物品触发一次")]
        public bool oneShot = true;

        [Tooltip("对话回放时的提示文案（预留字段）")]
        public string replayHint = "";

        [Tooltip("按顺序播放的台词")]
        public List<DialogueLine> lines = new List<DialogueLine>();

        public bool IsValid => lines != null && lines.Count > 0;

        void OnValidate()
        {
            if (lines == null) lines = new List<DialogueLine>();
            if (string.IsNullOrEmpty(title)) title = name;
        }
    }
}
