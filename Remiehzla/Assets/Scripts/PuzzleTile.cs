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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            owner.OnTileClicked(this);
        }
    }
}
