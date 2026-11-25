using System;
using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 2D 可对话物品：角色进入 Trigger（或靠近到一定距离）时展开对话。
    /// 触发同时使用 Trigger 事件与距离兜底，确保「接触即对话」。
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableItem2D : MonoBehaviour
    {
        [Header("对话内容")]
        public string itemName = "物品";
        public DialogueData dialogue;
        [Tooltip("勾选后，整局只会触发一次对话")]
        public bool oneShot = true;

        [Header("触发")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("触发器之外的距离兜底检测半径；设为 0 关闭")]
        [SerializeField, Min(0f)] private float proximityRadius = 1.25f;
        [Tooltip("离开后多久内不允许再次触发")]
        [SerializeField, Min(0f)] private float reenterCooldown = 0.8f;
        [Tooltip("对话结束后，站在旁边按此键可再次交谈（oneShot 物品除外）")]
        [SerializeField] private KeyCode retalkKey = KeyCode.F;

        [Header("表现")]
        [Tooltip("做浮动/摇摆的视觉子物体；留空则用自身")]
        public Transform visual;
        [Tooltip("物品底部的柔光，会随角色靠近变亮")]
        public SpriteRenderer glow;
        [SerializeField] private bool bob = true;
        [SerializeField, Min(0f)] private float bobHeight = 0.09f;
        [SerializeField, Min(0f)] private float bobSpeed = 2f;
        [SerializeField] private float swayAngle = 6f;
        [SerializeField, Min(0f)] private float swaySpeed = 1.4f;
        [SerializeField] private Color idleGlowColor = new Color(0.4f, 0.8f, 1f, 0.22f);
        [SerializeField] private Color hotGlowColor = new Color(1f, 0.86f, 0.4f, 0.85f);

        public event Action<InteractableItem2D> Triggered;
        public bool HasBeenTriggered { get; private set; }
        public bool PlayerInside => playerInside;

        bool playerInside;
        bool triggerInside;
        float leftAt = -999f;
        Vector3 visualOrigin;
        Quaternion visualOriginRotation;
        float phase;
        float glowAmount;
        Transform player;

        void Awake()
        {
            if (visual == null) visual = transform;
            visualOrigin = visual.localPosition;
            visualOriginRotation = visual.localRotation;
            phase = Mathf.Repeat(GetInstanceID() * 0.173f, 12f);

            // 接触区域必须是触发器，不能挡住角色
            foreach (var col in GetComponents<Collider2D>())
            {
                col.isTrigger = true;
            }

            if (glow != null) glow.color = idleGlowColor;
        }

        void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null) player = playerObject.transform;
        }

        void Update()
        {
            Animate();
            UpdateContact();
            UpdateRetalk();
        }

        void Animate()
        {
            float time = Time.unscaledTime;

            if (visual != transform)
            {
                if (bob)
                {
                    visual.localPosition = visualOrigin +
                        Vector3.up * (Mathf.Sin((time + phase) * bobSpeed) * bobHeight);
                }

                float sway = Mathf.Sin((time + phase) * swaySpeed) * swayAngle;
                visual.localRotation = visualOriginRotation * Quaternion.Euler(0f, 0f, sway);
            }

            if (glow != null)
            {
                float target = playerInside ? 1f : 0f;
                glowAmount = Mathf.MoveTowards(glowAmount, target, Time.unscaledDeltaTime * 4f);
                glow.color = Color.Lerp(idleGlowColor, hotGlowColor, glowAmount);
            }
        }

        void UpdateContact()
        {
            bool nearby = false;
            if (player != null && proximityRadius > 0f)
            {
                Vector2 flat = transform.position - player.position;
                nearby = flat.sqrMagnitude <= proximityRadius * proximityRadius;
            }

            SetPlayerInside(triggerInside || nearby);
        }

        void UpdateRetalk()
        {
            if (!playerInside || !Input.GetKeyDown(retalkKey)) return;

            var manager = DialogueManager.Instance;
            if (manager == null || manager.IsDialogueActive) return;
            TryTrigger();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            triggerInside = true;
            if (player == null) player = other.transform;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            triggerInside = false;
        }

        void SetPlayerInside(bool inside)
        {
            if (playerInside == inside) return;

            playerInside = inside;
            if (!inside)
            {
                leftAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - leftAt < reenterCooldown) return;
            TryTrigger();
        }

        void TryTrigger()
        {
            if (dialogue == null)
            {
                Debug.LogWarning($"[DialogueDemo2D] 「{itemName}」没有配置对话数据。", this);
                return;
            }

            if (oneShot && HasBeenTriggered) return;

            var manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[DialogueDemo2D] 场景里没有 DialogueManager，无法开始对话。", this);
                return;
            }

            if (manager.IsDialogueActive) return;

            HasBeenTriggered = true;

            if (player != null)
            {
                var controller = player.GetComponent<PlayerController2D>();
                if (controller != null) controller.FaceTowards(transform.position);
            }

            if (!manager.StartDialogue(dialogue))
            {
                HasBeenTriggered = false;
                return;
            }

            Triggered?.Invoke(this);
        }

        /// <summary>允许这段对话再次被触发（例如任务重置）。</summary>
        public void ResetTrigger()
        {
            HasBeenTriggered = false;
        }
    }
}
