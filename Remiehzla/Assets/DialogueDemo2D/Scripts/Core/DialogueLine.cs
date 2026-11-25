using System;
using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 一句台词，作为 <see cref="DialogueData"/> 的一部分被序列化。
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("说话者名字；留空则不显示名字")]
        public string speaker = "";

        [TextArea(2, 6)]
        [Tooltip("台词正文。打字机效果按字符逐个显示，暂不支持富文本标签")]
        public string text = "";

        [Min(0f)]
        [Tooltip("这句台词的打字速度（字/秒）；0 表示使用对话管理器里的默认速度")]
        public float charsPerSecond;

        public DialogueLine()
        {
        }

        public DialogueLine(string speaker, string text, float charsPerSecond = 0f)
        {
            this.speaker = speaker;
            this.text = text;
            this.charsPerSecond = charsPerSecond;
        }
    }
}
