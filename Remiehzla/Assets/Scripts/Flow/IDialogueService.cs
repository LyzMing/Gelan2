using System;
using Remiehzla.Dialogue;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 字幕播放服务。流程方只依赖这个接口，具体实现（目前是 <see cref="Remiehzla.Dialogue.SubtitlePlayer"/>）可以换。
    /// </summary>
    public interface IDialogueService
    {
        /// <summary>是否正在播放。播放期间其他可交互物应当被锁住。</summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 开始播放。已经在播、或者数据为空时返回 false。
        /// </summary>
        /// <param name="lockMovement">true = 播放期间禁用玩家移动（物品交互触发用）；false = 玩家可以继续走（区域触发用）</param>
        bool Play(DialogueAsset dialogue, Action onComplete = null, bool lockMovement = false);

        /// <summary>立刻结束当前对话。</summary>
        void Skip();
    }
}
