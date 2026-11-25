using UnityEngine;

namespace Remiehzla.Explore
{
    /// <summary>
    /// ⚠️ 临时用的角色移动，只为验证「拼图 → 探索 → 交互」整条链路能不能跑通。
    /// 等对方的正式移动系统接进来后，把这个文件整个删掉即可。
    ///
    /// 碰撞处理：不做物理模拟，而是每帧用 Physics2D.BoxCast 探测"脚下的小盒子"
    /// 会不会撞到东西，撞了就把这一轴的位移截断 —— 所以会停、会贴墙滑，但不会互相推挤。
    /// 角色自己**不需要**挂碰撞体（这样也不会撞到自己）。
    /// </summary>
    public class TempPlayerController : MonoBehaviour
    {
        [Header("移动")]
        [Tooltip("移动速度（世界单位/秒）")]
        public float speed = 3.5f;

        [Tooltip("关掉之后不再响应输入")]
        public bool controlEnabled = true;

        [Header("碰撞")]
        [Tooltip("勾上 = 被碰撞体挡住；关掉 = 直接穿墙（原来的行为）")]
        public bool useCollision = true;

        [Tooltip("哪些层会挡住玩家。默认全部")]
        public LayerMask blockingLayers = ~0;

        [Tooltip("脚部探测盒尺寸（本地单位，会乘以物体缩放）")]
        public Vector2 footSize = new Vector2(0.28f, 0.22f);

        [Tooltip("脚盒相对「脚下」的额外偏移（本地单位）。x 会跟着角色左右翻转镜像，y 往上为正")]
        public Vector2 footOffset = Vector2.zero;

        [Tooltip("贴墙留的缝隙，太小容易抖")]
        public float skin = 0.02f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;
        }

        void Update()
        {
            if (!controlEnabled) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            var dir = new Vector3(h, v, 0f);

            if (dir.sqrMagnitude > 1f) dir.Normalize();
            if (dir.sqrMagnitude <= 0f) return;

            Vector3 step = dir * (speed * Time.deltaTime);

            if (!useCollision)
            {
                transform.position += step;
                return;
            }

            // 已经卡在碰撞体里（出生点压到墙、或者手滑把玩家放进去了）：
            // 放行，别把人冻死，走两步就出来了
            if (AlreadyOverlapping())
            {
                transform.position += step;
                return;
            }

            // 分轴移动：撞墙时停住，但另一个轴还能走 → 贴着墙自然滑动
            transform.position += (Vector3)MoveAxis(new Vector2(step.x, 0f));
            transform.position += (Vector3)MoveAxis(new Vector2(0f, step.y));
        }

        /// <summary>脚部探测盒当前是不是已经和碰撞体重叠了。</summary>
        bool AlreadyOverlapping()
        {
            Collider2D hit = Physics2D.OverlapBox(FootOrigin(), FootWorldSize(), 0f, blockingLayers);

            return hit != null && hit.gameObject != gameObject && !hit.isTrigger;
        }

        /// <summary>尝试沿一个轴移动，被挡住就截断。返回实际位移。</summary>
        Vector2 MoveAxis(Vector2 delta)
        {
            float distance = delta.magnitude;

            if (distance <= 0.0001f) return Vector2.zero;

            Vector2 direction = delta / distance;

            int count = Physics2D.BoxCastNonAlloc(
                FootOrigin(), FootWorldSize(), 0f, direction, hits, distance + skin, blockingLayers);

            float closest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D other = hits[i].collider;

                if (other == null) continue;
                if (other.isTrigger) continue;
                if (other.gameObject == gameObject) continue;      // 别撞自己

                if (hits[i].distance < closest) closest = hits[i].distance;
            }

            if (closest == float.MaxValue) return delta;

            float allowed = Mathf.Max(0f, closest - skin);

            return direction * Mathf.Min(allowed, distance);
        }

        /// <summary>脚部探测盒的世界尺寸。</summary>
        Vector2 FootWorldSize()
        {
            Vector3 scale = transform.lossyScale;

            return new Vector2(footSize.x * Mathf.Abs(scale.x), footSize.y * Mathf.Abs(scale.y));
        }

        /// <summary>
        /// 脚部探测盒的世界中心。
        /// 基准是"pivot 往上半个盒高"（盒子底边正好落在脚下），再叠加 footOffset。
        /// </summary>
        Vector2 FootOrigin()
        {
            Vector2 size = FootWorldSize();
            Vector3 scale = transform.lossyScale;

            // 偏移量用本地单位，跟着物体缩放走；x 用带符号的缩放，角色左右翻转时偏移也跟着镜像
            var nudge = new Vector2(footOffset.x * scale.x, footOffset.y * scale.y);

            return new Vector2(transform.position.x, transform.position.y + size.y * 0.5f) + nudge;
        }

        /// <summary>脚部探测盒的世界中心，给别的系统用（交互距离、摄像机等）。</summary>
        public Vector2 FootPoint => FootOrigin();

        /// <summary>调脚盒偏移（本地单位）。运行时或从别的脚本调用。</summary>
        public void SetFootOffset(Vector2 localOffset)
        {
            footOffset = localOffset;
        }

        /// <summary>调脚盒尺寸（本地单位）。</summary>
        public void SetFootSize(Vector2 localSize)
        {
            footSize = localSize;
        }

        void OnDrawGizmosSelected()
        {
            // 画出实际用来撞墙的那个盒子，方便和碰撞体对齐
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireCube(FootOrigin(), FootWorldSize());

            // 再画一个小十字标出 pivot（transform.position）本身，方便看出偏移了多少
            Vector3 pivot = transform.position;
            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.9f);
            Gizmos.DrawLine(pivot + Vector3.left * 0.12f, pivot + Vector3.right * 0.12f);
            Gizmos.DrawLine(pivot + Vector3.down * 0.12f, pivot + Vector3.up * 0.12f);
        }
    }
}
