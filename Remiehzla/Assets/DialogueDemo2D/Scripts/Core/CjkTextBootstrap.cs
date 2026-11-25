using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 给界面里所有 <see cref="UnityEngine.UI.Text"/> 挂上系统中文字体，
    /// 避免中文显示成方块。挂在 Canvas 根节点上即可。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class CjkTextBootstrap : MonoBehaviour
    {
        void Awake()
        {
            UIFontProvider.ApplyToChildren(gameObject);
        }
    }
}
