using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 2D 俯视角移动：Rigidbody2D.MovePosition 驱动，对话期间锁定操作，
    /// 并可以让角色转向正在交互的物品。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField, Min(0f)] private float moveSpeed = 4.2f;
        [SerializeField, Min(0f)] private float rotationSpeed = 14f;
        [Tooltip("负责朝向的视觉子物体；留空则旋转自身")]
        [SerializeField] private Transform visual;

        Rigidbody2D body;
        Vector2 moveInput;
        Quaternion? lookTarget;

        public bool InputEnabled { get; set; } = true;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (visual == null) visual = transform;
        }

        /// <summary>由场景搭建脚本注入负责朝向的视觉子物体。</summary>
        public void Configure(Transform visualTransform)
        {
            visual = visualTransform;
        }

        void Update()
        {
            bool locked = !InputEnabled ||
                          (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive);

            if (locked)
            {
                moveInput = Vector2.zero;
                if (lookTarget.HasValue) RotateTowards(lookTarget.Value);
                return;
            }

            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            if (moveInput.sqrMagnitude > 0.01f)
            {
                lookTarget = RotationFor(moveInput);
            }

            if (lookTarget.HasValue) RotateTowards(lookTarget.Value);
        }

        void FixedUpdate()
        {
            if (moveInput.sqrMagnitude < 0.0001f) return;
            body.MovePosition(body.position + moveInput * (moveSpeed * Time.fixedDeltaTime));
        }

        /// <summary>让角色面朝世界中的某个点（对话开始时用）。</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector2 direction = (Vector2)(worldPoint - transform.position);
            if (direction.sqrMagnitude < 0.001f) return;
            lookTarget = RotationFor(direction);
        }

        void RotateTowards(Quaternion target)
        {
            visual.rotation = Quaternion.Slerp(visual.rotation, target, rotationSpeed * Time.deltaTime);
            if (Quaternion.Angle(visual.rotation, target) < 0.5f) lookTarget = null;
        }

        static Quaternion RotationFor(Vector2 direction)
        {
            // 精灵默认朝上（+Y），所以需要减去 90°
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            return Quaternion.Euler(0f, 0f, angle);
        }
    }
}
