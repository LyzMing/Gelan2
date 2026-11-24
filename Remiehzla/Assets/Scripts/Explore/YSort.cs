using UnityEngine;
using UnityEngine.Rendering;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 2D 遮挡排序：按"脚点"的 y 决定 sortingOrder，越靠下（y 越小）画得越靠前。
    ///
    /// 用法：挂在角色、桌子、箱子、前景装饰上；同一场景里所有需要互相遮挡的物体都要挂。
    /// 脚点默认取 transform.position（所以美术切图时把 pivot 放在底部中点为好）。
    ///
    /// ⚠️ 工作原理决定了它只会"整块换层"，没有渐变：
    ///     两个物体只比较脚点 y，交叉的那一帧就整体换前后。
    ///     一张 sprite 只有一个 sortingOrder，做不到"上半截被挡、下半截露出"。
    ///
    /// 想让它看起来自然，靠这三条：
    ///   1. 美术切开：物件不要画成实心矩形，带腿/缝隙/透明区域，
    ///      玩家站在后面时天然就只有一部分被挡，"弹出来"的突兀感会消失；
    ///   2. 用 sortBias 手动微调个别物件（地毯、挂墙上的画、天花板等不合适按 y 排的）；
    ///   3. 一个物件有多张 sprite 时，在它的根节点加 SortingGroup，
    ///      本组件会自动改 SortingGroup 的 order，让整个物件作为一层排序。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class YSort : MonoBehaviour
    {
        [Tooltip("参与排序的渲染器。留空 = 自动收集自己和自己下面的 SpriteRenderer。有 SortingGroup 时忽略")]
        public Renderer[] renderers;

        [Tooltip("脚点相对 transform.position 的 y 偏移（pivot 不在底部时用来补正）")]
        public float footOffset;

        [Tooltip("每 1 个世界单位对应多少 sortingOrder。够细就行，不用太大")]
        public float orderPerUnit = 100f;

        [Tooltip("排序基准，避免和背景层/UI 抢。一般填 0")]
        public int baseOrder;

        [Tooltip("手动微调：正数往前（盖住别人），负数往后。给不适合按 y 排的物件用")]
        public int sortBias;

        SortingGroup sortingGroup;
        int lastOrder = int.MinValue;

        void Awake()
        {
            sortingGroup = GetComponent<SortingGroup>();

            if (sortingGroup == null)
                sortingGroup = GetComponentInChildren<SortingGroup>();

            if (sortingGroup == null && (renderers == null || renderers.Length == 0))
                renderers = GetComponentsInChildren<Renderer>(true);
        }

        void LateUpdate()
        {
            Apply();
        }

        /// <summary>立刻重排一次（物体瞬间移动后可以主动调用）。</summary>
        public void Apply()
        {
            int order = ComputeOrder();

            // 值没变就不碰渲染器，省掉每帧的 native 调用
            if (order == lastOrder) return;

            lastOrder = order;

            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = order;
                return;
            }

            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                renderers[i].sortingOrder = order;
            }
        }

        /// <summary>当前脚点算出来的排序值（调试用）。</summary>
        public int ComputeOrder()
        {
            float footY = transform.position.y + footOffset;

            return baseOrder + sortBias - Mathf.RoundToInt(footY * orderPerUnit);
        }

        /// <summary>脚点在世界空间的位置。</summary>
        public Vector3 FootPoint => new Vector3(transform.position.x, transform.position.y + footOffset, transform.position.z);

        void OnDrawGizmosSelected()
        {
            // 选中物体时画出脚点，方便对美术的 pivot 和 footOffset
            Vector3 foot = FootPoint;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(foot, 0.08f);
            Gizmos.DrawLine(foot + Vector3.left * 0.6f, foot + Vector3.right * 0.6f);
        }
    }
}
