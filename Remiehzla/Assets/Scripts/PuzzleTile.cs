using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 单块拼图。
    /// 只负责两件小事：记录"我现在显示的是原图第几块、现在坐在网格第几格"，
    /// 以及把鼠标点击转发给 <see cref="PuzzleGrid"/>。
    /// 所有布局和交换逻辑都在 PuzzleGrid 里。
    /// </summary>
    public class PuzzleTile : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>这一格当前显示的是原图的第几块（0 = 左上角，按行优先）。</summary>
        public int PieceIndex { get; private set; }

        /// <summary>这一格当前在网格里的格子下标（0 = 左上角，按行优先）。</summary>
        public int CellIndex { get; set; }

        /// <summary>本格自己的 RectTransform，用来摆位置。</summary>
        public RectTransform TileRect { get; private set; }

        PuzzleGrid owner;
        RawImage piece;
        Image border;

        Color borderIdleColor = Color.black;
        Color borderSelectedColor = Color.yellow;
        float selectedScale = 1f;

        public void Init(PuzzleGrid grid, RawImage pieceImage, Image borderImage, RectTransform rect)
        {
            owner = grid;
            piece = pieceImage;
            border = borderImage;
            TileRect = rect;
        }

        /// <summary>设置选中/未选中的配色与放大倍数。</summary>
        public void Configure(Color idle, Color highlight, float scale)
        {
            borderIdleColor = idle;
            borderSelectedColor = highlight;
            selectedScale = Mathf.Max(0.1f, scale);
        }

        /// <summary>把这一格显示成原图的第 pieceIndex 块。</summary>
        public void Display(int pieceIndex, Texture texture, Rect uvRect)
        {
            PieceIndex = pieceIndex;

            if (piece == null) return;

            piece.texture = texture;
            piece.uvRect = uvRect;
        }

        /// <summary>选中时亮边框 + 轻微放大，并把这块提到最上层避免被邻居压住。</summary>
        public void SetSelected(bool isSelected)
        {
            if (border != null) border.color = isSelected ? borderSelectedColor : borderIdleColor;
            if (TileRect != null) TileRect.localScale = Vector3.one * (isSelected ? selectedScale : 1f);
            if (isSelected) transform.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ 位移动画

        /// <summary>当前是否正在滑动。</summary>
        public bool IsAnimating { get; private set; }

        Vector2 slideFrom;
        Vector2 slideTo;
        float slideTime;
        float slideDuration;

        /// <summary>直接落到目标位置，不做动画（打乱时用）。</summary>
        public void SnapTo(Vector2 position)
        {
            if (TileRect == null) return;

            TileRect.anchoredPosition = position;
            slideFrom = position;
            slideTo = position;
            IsAnimating = false;
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
            IsAnimating = true;
        }

        /// <summary>每帧推进一步滑动。</summary>
        public void TickSlide(float deltaTime)
        {
            if (!IsAnimating || TileRect == null) return;

            slideTime += deltaTime;

            float t = Mathf.Clamp01(slideTime / slideDuration);
            float eased = 1f - (1f - t) * (1f - t);        // ease-out：起步快、落位稳

            TileRect.anchoredPosition = Vector2.LerpUnclamped(slideFrom, slideTo, eased);

            if (t >= 1f)
            {
                TileRect.anchoredPosition = slideTo;
                IsAnimating = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            owner.OnTileClicked(this);
        }
    }
}
