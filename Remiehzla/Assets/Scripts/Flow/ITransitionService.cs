using System;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 转场（淡入淡出 / 加载遮罩）。视觉和时长归实现方，流程方只关心两个时间点：
    ///
    ///   atMidpoint  画面全黑的那一刻 —— 流程方在这里切换阶段 / 加载场景
    ///   onComplete  淡入结束、玩家能看见画面了
    ///
    /// 典型调用：
    ///   transition.Play(() => { 关掉拼图; 打开探索; }, () => { 打日志 });
    /// </summary>
    public interface ITransitionService
    {
        /// <param name="atMidpoint">全黑时调用</param>
        /// <param name="onComplete">淡入结束时调用，可为 null</param>
        /// <param name="fadeOut">变黑用时（秒）</param>
        /// <param name="fadeIn">变亮用时（秒）</param>
        void Play(Action atMidpoint, Action onComplete = null, float fadeOut = 0.4f, float fadeIn = 0.4f);

        /// <summary>转场是否正在进行（防止重复触发）。</summary>
        bool IsPlaying { get; }
    }
}
