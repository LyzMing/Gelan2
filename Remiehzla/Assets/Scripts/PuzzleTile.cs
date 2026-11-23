using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 单块拼图。
    ///
    /// 负责三件事：
    ///   1. 记录自身状态：显示原图第几块、坐在网格第几格、转过几次、是否镜像；
    ///   2. 推进自己的动画：位移、90° 旋转、水平翻转；
    ///   3. 把点击 / 按下 / 拖拽事件转发给 <see cref="PuzzleGrid"/>。
    ///
    /// 所有规则判定（谁和谁交换、能不能放置）都在 PuzzleGrid 里。
    /// </summary>
    public class PuzzleTile : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>这一格当前显示的是原图的第几块（0 = 左上角，按行优先）。</summary>
        public int PieceIndex { get; private set; }

        /// <summary>这一格当前在网格里的格子下标（0 = 左上角，按行优先）。</summary>
        public int CellIndex { get; set; }

        /// <summary>
        /// 顺时针转 90° 的累计步数，对 4 取模。
        /// 0 = 正立；1 = 顺时针 90°；3 = 逆时针 90°。
        /// </summary>
        public int TurnSteps { get; private set; }

        /// <summary>是否水平镜像。</summary>
        public bool FlippedX { get; private set; }

        /// <summary>方向正立且未镜像（这一块算复位了）。</summary>
        public bool IsUpright => TurnSteps == 0 && !FlippedX;

        /// <summary>位置或朝向任意一项还在动。</summary>
        public bool IsAnimating { get; private set; }

        /// <summary>旋转动画是否还在播（播完之前不接受新的旋转输入）。</summary>
        public bool IsRotating => rotating;

        /// <summary>当前旋转角的动画目标（撤销记录用）。</summary>
        public float AngleTarget => angleTarget;

        /// <summary>本格自己的 RectTransform，用来摆位置。</summary>
        public RectTransform TileRect { get; private set; }

        PuzzleGrid owner;
        RawImage piece;
        Image border;
        RectTransform flipRoot;
        RectTransform spinRoot;

        Color borderIdleColor = Color.black;
        Color borderSelectedColor = Color.yellow;
        float selectedScale = 1f;
        float dragScale = 1.1f;

        // ---- 位移动画 ----
        Vector2 slideFrom;
        Vector2 slideTo;
        float slideTime;
        float slideDuration;
        bool sliding;

        // ---- 旋转动画 ----
        float angle;            // 当前显示的旋转角（度，正 = 逆时针）
        float angleFrom;
        float angleTo;
        float angleTarget;      // 逻辑目标角，每次成功旋转固定累加 ±90°
        float angleTime;
        float angleDuration;
        bool rotating;

        // ---- 翻转动画 ----
        float flip = 1f;        // 当前显示的横轴缩放，-1 = 已镜像
        float flipFrom;
        float flipTo;
        float flipTime;
        float flipDuration;
        bool flipping;

        bool draggedSincePress;

        // ------------------------------------------------------------------ 初始化

        public void Init(PuzzleGrid grid, RawImage pieceImage, Image borderImage, RectTransform tileRect,
            RectTransform flipRootTransform, RectTransform spinRootTransform)
        {
            owner = grid;
            piece = pieceImage;
            border = borderImage;
            TileRect = tileRect;
            flipRoot = flipRootTransform;
            spinRoot = spinRootTransform;
        }

        /// <summary>设置选中/未选中的配色、选中放大倍数、拖拽放大倍数。</summary>
        public void Configure(Color idle, Color highlight, float scaleWhenSelected, float scaleWhenDragged)
        {
            borderIdleColor = idle;
            borderSelectedColor = highlight;
            selectedScale = Mathf.Max(0.1f, scaleWhenSelected);
            dragScale = Mathf.Max(0.1f, scaleWhenDragged);
        }

        /// <summary>把这一格显示成原图的第 pieceIndex 块。</summary>
        public void Display(int pieceIndex, Texture texture, Rect uvRect)
        {
            PieceIndex = pieceIndex;

            if (piece == null) return;

            piece.texture = texture;
            piece.uvRect = uvRect;
        }

        // ------------------------------------------------------------------ 选中 / 拖拽外观

        /// <summary>选中时亮边框 + 轻微放大，并把这块提到最上层避免被邻居压住。</summary>
        public void SetSelected(bool isSelected)
        {
            if (border != null) border.color = isSelected ? borderSelectedColor : borderIdleColor;
            if (TileRect != null) TileRect.localScale = Vector3.one * (isSelected ? selectedScale : 1f);
            if (isSelected) BringToFront();
        }

        /// <summary>拖起来时再放大一点，让人看清"这块现在跟着鼠标"。</summary>
        public void SetDragVisual(bool isDragging)
        {
            if (TileRect != null)
                TileRect.localScale = Vector3.one * (isDragging ? dragScale : selectedScale);

            if (isDragging) BringToFront();
        }

        /// <summary>把这块提到所有拼图的最上层。</summary>
        public void BringToFront()
        {
            transform.SetAsLastSibling();
        }

        /// <summary>拖拽时给"光标下面那块、即将被换位置"的拼图压暗。</summary>
        public void SetDimmed(bool dimmed, Color dimColor)
        {
            if (piece == null) return;

            piece.color = dimmed ? dimColor : Color.white;
        }

        // ------------------------------------------------------------------ 位置动画

        /// <summary>直接落到目标位置，不做动画（打乱时用）。</summary>
        public void SnapTo(Vector2 position)
        {
            if (TileRect == null) return;

            TileRect.anchoredPosition = position;
            slideFrom = position;
            slideTo = position;
            sliding = false;
        }

        /// <summary>
        /// 从"现在的位置"滑到目标位置。
        /// 上一段滑动没播完时再调一次，会从当前位置重新起步，
        /// 所以连按方向键不会卡住或跳一下。
        /// </summary>
        public void SlideTo(Vector2 position, float duration)
        {
            if (TileRect == null) return;

            if (duration <= 0f)
            {
                SnapTo(position);
                return;
            }

            slideFrom = TileRect.anchoredPosition;
            slideTo = position;
            slideTime = 0f;
            slideDuration = duration;
            sliding = true;
        }

        /// <summary>打断正在进行的滑动（开始拖拽时用，避免和拖拽抢位置）。</summary>
        public void StopSlide()
        {
            sliding = false;
        }

        // ------------------------------------------------------------------ 旋转 / 翻转

        /// <summary>
        /// 转 90°。clockwise = true 为顺时针。
        /// 上一次旋转动画没播完时直接忽略本次输入（返回 false），
        /// 这样每成功一次就一定是整 90°，不会因为连按而少转。
        /// </summary>
        public bool Rotate(bool clockwise, float duration)
        {
            if (rotating) return false;

            // 顺时针记 1 步，逆时针等价于 3 步（-1 mod 4）
            TurnSteps = (TurnSteps + (clockwise ? 1 : 3)) % 4;

            // 目标角是"绝对累加"的：每成功一次固定 ±90°，
            // 不拿当前插值角当基准，避免动画中途按下的累计误差。
            angleTarget += clockwise ? -90f : 90f;

            angleFrom = angle;
            angleTo = angleTarget;
            angleTime = 0f;
            angleDuration = Mathf.Max(0.0001f, duration);
            rotating = true;

            return true;
        }

        /// <summary>水平镜像翻转。</summary>
        public void ToggleFlip(float duration)
        {
            FlippedX = !FlippedX;

            flipFrom = flip;
            flipTo = FlippedX ? -1f : 1f;
            flipTime = 0f;
            flipDuration = Mathf.Max(0.0001f, duration);
            flipping = true;
        }

        /// <summary>瞬间回到正立、未镜像（打乱时用）。</summary>
        public void ResetOrientation()
        {
            TurnSteps = 0;
            FlippedX = false;

            angle = 0f;
            angleFrom = 0f;
            angleTo = 0f;
            angleTarget = 0f;
            rotating = false;

            flip = 1f;
            flipFrom = 1f;
            flipTo = 1f;
            flipping = false;

            ApplyOrientation();
        }

        /// <summary>
        /// 直接把朝向恢复成指定状态（撤销、复制时用）。
        /// 和 Rotate / ToggleFlip 那套"相对"操作不同，这里是绝对赋值，
        /// 所以哪怕动画正在播、或者中间漏按了几次，也能精确回到记录时的样子。
        /// duration <= 0 立即生效，否则滑过去。
        /// </summary>
        public void RestoreOrientation(int turnSteps, bool flippedX, float angleTargetValue, float duration)
        {
            TurnSteps = ((turnSteps % 4) + 4) % 4;
            FlippedX = flippedX;
            angleTarget = angleTargetValue;

            if (duration <= 0f)
            {
                angle = angleTarget;
                rotating = false;

                flip = FlippedX ? -1f : 1f;
                flipping = false;

                ApplyOrientation();
                return;
            }

            angleFrom = angle;
            angleTo = angleTarget;
            angleTime = 0f;
            angleDuration = duration;
            rotating = Mathf.Abs(angleTo - angleFrom) > 0.01f;      // 已经正了就别空转

            flipFrom = flip;
            flipTo = FlippedX ? -1f : 1f;
            flipTime = 0f;
            flipDuration = duration;
            flipping = Mathf.Abs(flipTo - flipFrom) > 0.001f;
        }

        void ApplyOrientation()
        {
            // 旋转作用在 SpinRoot 上：图块和描边是同一条链上的兄弟，
            // 所以描边会跟着拼图一起转、一起翻，始终贴着这块图。
            if (spinRoot != null)
                spinRoot.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (flipRoot == null) return;

            // 镜像放在外层 FlipRoot：轴永远是格子横轴（= 屏幕横轴）
            // 缩放正好经过 0 时矩阵不可逆，会让 UI 射线检测算出 NaN，这里留一点余量
            float scaleX = flip;
            if (Mathf.Abs(scaleX) < 0.001f) scaleX = scaleX >= 0f ? 0.001f : -0.001f;

            flipRoot.localScale = new Vector3(scaleX, 1f, 1f);
        }

        // ------------------------------------------------------------------ 每帧推进

        /// <summary>每帧推进一步位移 / 旋转 / 翻转。</summary>
        public void TickAnimation(float deltaTime)
        {
            if (sliding && TileRect != null)
            {
                slideTime += deltaTime;

                float t = Mathf.Clamp01(slideTime / slideDuration);
                float eased = 1f - (1f - t) * (1f - t);        // ease-out：起步快、落位稳

                TileRect.anchoredPosition = Vector2.LerpUnclamped(slideFrom, slideTo, eased);

                if (t >= 1f)
                {
                    TileRect.anchoredPosition = slideTo;
                    sliding = false;
                }
            }

            if (rotating)
            {
                angleTime += deltaTime;

                float t = Mathf.Clamp01(angleTime / angleDuration);
                float eased = 1f - (1f - t) * (1f - t);
                angle = Mathf.LerpUnclamped(angleFrom, angleTo, eased);

                if (t >= 1f)
                {
                    angle = angleTo;
                    rotating = false;
                }

                ApplyOrientation();
            }

            if (flipping)
            {
                flipTime += deltaTime;

                float t = Mathf.Clamp01(flipTime / flipDuration);
                float eased = 1f - (1f - t) * (1f - t);
                // 从 +1 线性走到 -1，中途经过 0，视觉上就是"立起来翻过去"
                flip = Mathf.LerpUnclamped(flipFrom, flipTo, eased);

                if (t >= 1f)
                {
                    flip = flipTo;
                    flipping = false;
                }

                ApplyOrientation();
            }

            IsAnimating = sliding || rotating || flipping;
        }

        // ------------------------------------------------------------------ 事件转发

        public void OnPointerDown(PointerEventData eventData)
        {
            draggedSincePress = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            // 刚拖过就不算点击，否则拖完会被当成一次"切换选中"。
            // 但复制模式例外：那时左键的语义是"放下"，松手就得放下。
            if (draggedSincePress && !owner.IsCopyMode) return;

            owner.OnTileClicked(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner == null) return;
            // 只有左键拖拼图；中键留给"拖动画面"
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            draggedSincePress = true;
            owner.OnTileBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (owner == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            owner.OnTileDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (owner == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            owner.OnTileEndDrag(this, eventData);
        }
    }
}
