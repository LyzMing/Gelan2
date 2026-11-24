using UnityEngine;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 角色控制开关。任何"需要临时把玩家冻住"的系统都调这个：
    ///   - 拼图阶段（方向键要留给拼图，角色绝对不能动）
    ///   - 对话进行中
    ///   - 过场/转场
    ///
    /// 实现方式：你的 PlayerController 实现本接口，
    /// 并自己订阅 GameEvents.PuzzlePhaseEntered / ExplorePhaseEntered 来开关自己。
    /// </summary>
    public interface IPlayerControl
    {
        /// <summary>true = 玩家可以操作，false = 冻住（但不要隐藏角色）</summary>
        void SetControlEnabled(bool enabled);

        /// <summary>角色自己的 Transform，摄像机跟随、交互距离判断会用</summary>
        Transform Player { get; }
    }
}
