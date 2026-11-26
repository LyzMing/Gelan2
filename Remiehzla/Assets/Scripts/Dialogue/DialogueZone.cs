using UnityEngine;
using Remiehzla.Explore;
using Remiehzla.Flow;

namespace Remiehzla.Dialogue
{
    /// <summary>
    /// 区域触发对话：玩家走进这个范围就自动播字幕。
    ///
    /// · 不用物理触发器（我们的角色没有刚体），直接算坐标是不是在框里
    /// · 有 BoxCollider2D 时用它的范围（方便在场景里拖），没有就用 Size 字段
    /// · 播放期间不锁玩家移动
    /// · 别的对话正在播时不会触发，而且只会触发一次
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueZone : MonoBehaviour
    {
        [Header("对话")]
        public DialogueAsset dialogue;

        [Tooltip("只能触发一次")]
        public bool oneShot = true;

        [Tooltip("进入时是否锁住玩家移动。区域触发一般不需要")]
        public bool lockMovement;

        [Header("条件")]
        [Tooltip("需要先拿到这个物品 id 才能触发。留空 = 无条件")]
        public string requiredItemId;

        [Header("触发范围")]
        [Tooltip("没有 BoxCollider2D 时用这个尺寸（世界单位，以本物体为中心）")]
        public Vector2 size = new Vector2(2f, 2f);

        Collider2D areaCollider;
        Transform player;
        float nextPlayerSearch;
        bool inside;
        bool played;

        /// <summary>条件是否满足。</summary>
        bool ConditionMet =>
            string.IsNullOrEmpty(requiredItemId) || ProgressService.HasCollected(requiredItemId);

        void Awake()
        {
            areaCollider = GetComponent<Collider2D>();
        }

        void Update()
        {
            if (oneShot && played) return;

            Transform target = ResolvePlayer();

            if (target == null) return;

            bool nowInside = Contains(target.position);

            // 只在"刚进来"的那一帧触发，避免站在里面反复触发
            bool entered = nowInside && !inside;

            inside = nowInside;

            if (!entered) return;

            // 条件不满足：静默跳过，不算触发过。拿到物品后重新走进来才会触发。
            if (!ConditionMet) return;

            // 别的对话正在播 → 这次不触发（也不算触发过，出去再进来可以补上）
            if (SubtitlePlayer.IsAnyPlaying) return;

            TryPlay();
        }

        void TryPlay()
        {
            if (dialogue == null)
            {
                Debug.LogWarning($"[DialogueZone] {name} 没有配对话数据", this);
                return;
            }

            System.Action onComplete = null;

            if (dialogue.goToNextLevelWhenFinished)
                onComplete = ExploreSceneController.LeaveCurrentScene;

            if (!SubtitlePlayer.Instance.Play(dialogue, onComplete, lockMovement)) return;

            played = true;

            Debug.Log($"[DialogueZone] {name} 触发对话");
        }

        bool Contains(Vector3 worldPosition)
        {
            if (areaCollider != null)
            {
                Bounds bounds = areaCollider.bounds;

                // 只比 x/y：2D 碰撞体在 z 上的厚度是 0，比 z 会误判
                return worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x &&
                       worldPosition.y >= bounds.min.y && worldPosition.y <= bounds.max.y;
            }

            Vector3 scale = transform.lossyScale;

            var area = new Bounds(
                transform.position,
                new Vector3(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y), 1f));

            return worldPosition.x >= area.min.x && worldPosition.x <= area.max.x &&
                   worldPosition.y >= area.min.y && worldPosition.y <= area.max.y;
        }

        Transform ResolvePlayer()
        {
            if (player != null) return player;
            if (Time.unscaledTime < nextPlayerSearch) return null;

            nextPlayerSearch = Time.unscaledTime + 1f;

            var animator = FindObjectOfType<PlayerAnimator>();

            if (animator != null)
            {
                player = animator.transform;
                return player;
            }

            var controller = FindObjectOfType<TempPlayerController>();

            if (controller != null)
            {
                player = controller.transform;
                return player;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");

            if (tagged != null) player = tagged.transform;

            return player;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);

            var collider2D = GetComponent<Collider2D>();

            if (collider2D != null)
            {
                Bounds bounds = collider2D.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
                return;
            }

            Vector3 scale = transform.lossyScale;

            Gizmos.DrawWireCube(transform.position,
                new Vector3(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y), 0.05f));
        }
    }
}
