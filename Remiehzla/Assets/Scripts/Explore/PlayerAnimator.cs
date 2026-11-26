using UnityEngine;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 四向走路动画（不用 Animator，直接换 Sprite）。
    ///
    /// 用法：
    ///   1. 挂在角色上（或角色的 Sprite 子物体上）
    ///   2. 四个字段各拖一张图：正面 / 背面 / 朝左 / 朝右
    ///      每张图是"同一朝向 4 帧横着排"（例：144×36 → 4 帧 36×36）
    ///   3. 贴图必须勾 Read/Write Enabled（菜单：工具 → 美术 → 4）
    ///
    /// 朝向判定：按输入的主轴。斜着走时默认优先左右（preferHorizontalOnDiagonal）。
    /// 走路时循环 4 帧，站着时停在第一帧。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        public enum Facing
        {
            Down = 0,   // 正面
            Up = 1,     // 背面
            Left = 2,
            Right = 3
        }

        [Header("四向动画图（每张 = 同一朝向 4 帧横排）")]
        [Tooltip("正面（朝镜头）")]
        public Texture2D frontSheet;

        [Tooltip("背面（背对镜头）")]
        public Texture2D backSheet;

        [Tooltip("朝左")]
        public Texture2D leftSheet;

        [Tooltip("朝右")]
        public Texture2D rightSheet;

        [Header("渲染")]
        [Tooltip("留空 = 自己身上的 SpriteRenderer，再找不到就找子物体")]
        public SpriteRenderer target;

        [Tooltip("单帧边长（像素）。0 = 用贴图高度自动推断（144×36 → 36）")]
        public int frameSize;

        [Tooltip("每像素多少世界单位。要和场景里其他素材一致（背景是 25）")]
        public float pixelsPerUnit = 25f;

        [Tooltip("锚点。默认底部中点，这样 transform.position 就是脚点，和 YSort 对得上")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("播放")]
        [Tooltip("每秒几帧。4 帧 × 8fps = 0.5 秒一个循环")]
        public float framesPerSecond = 8f;

        [Tooltip("斜着走时优先用左右朝向；关掉则优先上下")]
        public bool preferHorizontalOnDiagonal = true;

        [Tooltip("勾上就自己读 Input.GetAxisRaw；关掉则由外部每帧调 SetMoveInput")]
        public bool readInput = true;

        [Header("初始状态")]
        public Facing startFacing = Facing.Down;

        Sprite[][] anims;
        Sprite currentSprite;
        Facing facing;
        int frame;
        float timer;
        bool moving;
        bool controlEnabled = true;

        public Facing CurrentFacing => facing;
        public bool IsMoving => moving;

        void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>(true);

            anims = new Sprite[4][];
            anims[(int)Facing.Down] = Slice(frontSheet);
            anims[(int)Facing.Up] = Slice(backSheet);
            anims[(int)Facing.Left] = Slice(leftSheet);
            anims[(int)Facing.Right] = Slice(rightSheet);

            facing = startFacing;

            ApplyFrame(0);

            WarnMissing();
        }

        void WarnMissing()
        {
            if (target == null)
                Debug.LogWarning("[PlayerAnimator] 找不到 SpriteRenderer，动画不会显示", this);

            if (frontSheet == null && backSheet == null && leftSheet == null && rightSheet == null)
                Debug.LogWarning("[PlayerAnimator] 四张动画图都没配", this);
        }

        /// <summary>把一张横排贴图切成逐帧 Sprite。贴图必须可读，否则 Sprite.Create 会失败。</summary>
        Sprite[] Slice(Texture2D sheet)
        {
            if (sheet == null) return null;

            int size = frameSize > 0 ? frameSize : Mathf.Min(sheet.width, sheet.height);

            if (size <= 0) return null;

            int count = Mathf.Max(1, sheet.width / size);
            var sprites = new Sprite[count];

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(i * size, 0f, size, size);

                sprites[i] = Sprite.Create(sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            return sprites;
        }

        void Update()
        {
            if (readInput && controlEnabled)
            {
                var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                SetMoveInput(input);
            }

            Advance();
        }

        // ------------------------------------------------------------------ 对外

        /// <summary>喂输入（不需要归一化）。零向量 = 站着。</summary>
        public void SetMoveInput(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                moving = false;
                return;
            }

            moving = true;

            bool horizontal = preferHorizontalOnDiagonal
                ? Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                : Mathf.Abs(input.x) > Mathf.Abs(input.y);

            Facing next = horizontal
                ? (input.x > 0f ? Facing.Right : Facing.Left)
                : (input.y > 0f ? Facing.Up : Facing.Down);

            SetFacing(next);
        }

        /// <summary>直接指定朝向（交互时让角色面朝目标用）。</summary>
        public void SetFacing(Facing next)
        {
            if (next == facing) return;

            facing = next;
            timer = 0f;
            frame = 0;

            ApplyFrame(0);
        }

        /// <summary>让角色面朝世界上某个点（横向/纵向取主轴）。</summary>
        public void FaceTowards(Vector3 worldPoint, bool movingState = false)
        {
            Vector2 direction = worldPoint - transform.position;

            if (direction.sqrMagnitude < 0.0001f) return;

            SetMoveInput(direction);
            moving = movingState;
        }

        /// <summary>关掉之后不再自己读输入（对话、过场时用）。</summary>
        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;

            if (!enabled) moving = false;
        }

        // ------------------------------------------------------------------ 内部

        void Advance()
        {
            Sprite[] frames = anims != null ? anims[(int)facing] : null;

            if (frames == null || frames.Length == 0) return;

            if (!moving || frames.Length == 1)
            {
                frame = 0;
                ApplyFrame(0);
                return;
            }

            timer += Time.deltaTime * Mathf.Max(1f, framesPerSecond);

            frame = Mathf.FloorToInt(timer) % frames.Length;

            ApplyFrame(frame);
        }

        void ApplyFrame(int index)
        {
            if (anims == null || target == null) return;

            Sprite[] frames = anims[(int)facing];

            if (frames == null || frames.Length == 0) return;

            Sprite sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];

            if (sprite == null || sprite == currentSprite) return;

            currentSprite = sprite;
            target.sprite = sprite;
        }
    }
}
