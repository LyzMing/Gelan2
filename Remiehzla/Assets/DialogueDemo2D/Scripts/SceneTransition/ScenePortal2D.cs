using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 2D 场景传送门：角色进入 Trigger 后触发 SceneTransitionManager 切换场景。
    /// 默认“接触即走”，也可以改成按 F 确认。
    /// </summary>
    [DisallowMultipleComponent]
    public class ScenePortal2D : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private string targetScene = "";
        [Tooltip("勾选：碰到门就切换；取消：站在门里按 Interact Key")]
        [SerializeField] private bool autoEnter = true;
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        [Header("表现")]
        public SpriteRenderer glow;
        [SerializeField] private Color idleGlowColor = new Color(0.4f, 0.8f, 1f, 0.3f);
        [SerializeField] private Color hotGlowColor = new Color(0.9f, 1f, 1f, 0.9f);
        [SerializeField] private float pulseSpeed = 1.8f;

        public string TargetScene => targetScene;

        bool playerInside;
        bool used;
        float phase;

        public void Configure(string sceneName, SpriteRenderer glowRenderer = null,
            Color? idleColor = null, Color? hotColor = null)
        {
            targetScene = sceneName;
            if (glowRenderer != null) glow = glowRenderer;
            if (idleColor.HasValue) idleGlowColor = idleColor.Value;
            if (hotColor.HasValue) hotGlowColor = hotColor.Value;
        }

        void Awake()
        {
            phase = Mathf.Repeat(GetInstanceID() * 0.37f, 10f);

            foreach (var col in GetComponents<Collider2D>())
            {
                col.isTrigger = true;
            }

            if (glow != null) glow.color = idleGlowColor;
        }

        void Update()
        {
            if (glow != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime + phase) * pulseSpeed);
                Color color = Color.Lerp(idleGlowColor, hotGlowColor, pulse);
                if (playerInside) color = Color.Lerp(color, hotGlowColor, 0.5f);
                glow.color = color;
            }

            if (!autoEnter && playerInside && Input.GetKeyDown(interactKey))
            {
                Enter();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            playerInside = true;
            if (autoEnter) Enter();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            playerInside = false;
        }

        void Enter()
        {
            if (used || string.IsNullOrEmpty(targetScene)) return;

            var manager = SceneTransitionManager.Instance;
            if (manager == null) manager = SceneTransitionManager.EnsureInstance();
            if (manager.IsTransitioning) return;

            used = true;
            if (!manager.LoadScene(targetScene))
            {
                used = false;
            }
        }
    }
}
