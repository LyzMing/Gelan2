using UnityEngine;

namespace Remiehzla.Explore
{
    /// <summary>
    /// ⚠️ 临时用的角色移动，只为验证"拼图 → 探索 → 交互"整条链路能不能跑通。
    /// 等对方的正式移动系统接进来后，把这个文件整个删掉即可。
    ///
    /// 不带物理、不带碰撞，方向键/WASD 直接推 transform。
    /// 对外提供和 <see cref="Remiehzla.Flow.IPlayerControl"/> 同名的方法，
    /// 方便到时候直接换成对方的实现。
    /// </summary>
    public class TempPlayerController : MonoBehaviour
    {
        [Tooltip("移动速度（世界单位/秒）")]
        public float speed = 3.5f;

        [Tooltip("关掉之后不再响应输入")]
        public bool controlEnabled = true;

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

            transform.position += dir * (speed * Time.deltaTime);
        }
    }
}
