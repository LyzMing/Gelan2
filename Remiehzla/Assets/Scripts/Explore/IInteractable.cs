using UnityEngine;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 所有可交互物的统一入口。
    /// 交互检测（谁最近、朝向判断、按键提示 UI）由角色那套系统负责，
    /// 它只需要找到目标后调用 <see cref="Interact"/>，并按 <see cref="Prompt"/> 显示提示文字。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>提示文字（"查看" / "捡起" / "进入"）。</summary>
        string Prompt { get; }

        /// <summary>当前能不能交互（条件未满足时返回 false，例如门需要先拼对）。</summary>
        bool CanInteract { get; }

        /// <summary>做"边缘高亮描边"时用的锚点（一般是物件自己的 transform 或某个子节点）。</summary>
        Transform HighlightTarget { get; }

        /// <summary>执行交互。interactor 是发起交互的角色。</summary>
        void Interact(GameObject interactor);
    }
}
