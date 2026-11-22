using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 4x4 拼图原型（策划案第一关"家"的解谜机制）。
    ///
    /// 玩法：
    ///   1. 开局把整图切成 gridSize × gridSize 块，随机打乱。
    ///   2. 鼠标左键点选一块，选中块会亮边框。
    ///   3. 按 ↑ ↓ ← → 让选中块和相邻格互换位置（撞边界则不动）。
    ///   4. 再点一次取消选择；R 重新打乱；Esc 取消选择。
    ///
    /// 所有 UI（Canvas / 背景 / 格子 / 提示）都在运行时生成，
    /// 场景里只需要一个挂着本脚本的空物体。
    /// </summary>
    [DisallowMultipleComponent]
    public class PuzzleGrid : MonoBehaviour
    {
        [Header("图片")]
        [Tooltip("要切分的整图，建议正方形。留空时会自动生成一张彩色调试图。")]
        public Texture2D sourceImage;

        [Header("网格")]
        [Range(2, 8)]
        [Tooltip("几乘几。策划案第一关先用 4x4。")]
        public int gridSize = 4;

        [Tooltip("单格边长（Canvas 参考分辨率下的像素）。")]
        public float tileSize = 150f;

        [Tooltip("格子之间的缝隙。")]
        public float spacing = 6f;

        [Header("外观")]
        public Color backgroundColor = new Color(0.09f, 0.10f, 0.13f, 1f);
        public Color borderIdleColor = new Color(0f, 0f, 0f, 0.55f);
        public Color borderSelectedColor = new Color(1f, 0.78f, 0.25f, 1f);

        [Tooltip("边框粗细，同时也是图块相对格子的内缩量。")]
        public float borderThickness = 3f;

        [Tooltip("选中时放大到多少倍。")]
        public float selectedScale = 1.06f;

        [Header("移动动画")]
        [Tooltip("一次交换滑动的时长（秒）。设为 0 就退回瞬间平移。")]
        public float moveDuration = 0.14f;

        [Header("提示")]
        [Tooltip("在屏幕下方显示一行操作提示。")]
        public bool showHint = true;

        /// <summary>当前是否已经拼好。</summary>
        public bool IsSolved { get; private set; }

        /// <summary>本次打乱以来移动了几步（第一关的步数限制以后可以接这里）。</summary>
        public int MoveCount { get; private set; }

        /// <summary>当前选中的格子，没有选中时为 null。</summary>
        public PuzzleTile Selected => selected;

        readonly List<PuzzleTile> tiles = new List<PuzzleTile>();

        PuzzleTile selected;
        RectTransform boardRect;
        Texture2D texture;
        float step;

        // ------------------------------------------------------------------ 生命周期

        void Awake()
        {
            EnsureEventSystem();
            EnsureTexture();
            BuildUI();
        }

        void Start()
        {
            Shuffle();
        }

        void Update()
        {
            // 动画先推进，避免输入一多就积压着不动
            float dt = Time.deltaTime;
            for (int i = 0; i < tiles.Count; i++) tiles[i].TickSlide(dt);

            HandleInput();
        }

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Shuffle();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Select(null);
                return;
            }

            int dx = 0, dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;         // 行号向上减小
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            else return;

            TrySwapWithNeighbor(dx, dy);
        }

        // ------------------------------------------------------------------ 对外接口

        /// <summary>重新随机打乱（不会打乱成已复原的样子）。</summary>
        public void Shuffle()
        {
            int n = tiles.Count;
            if (n == 0) return;

            var pieces = new int[n];
            for (int i = 0; i < n; i++) pieces[i] = i;

            // Fisher–Yates 洗牌
            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = pieces[i];
                pieces[i] = pieces[j];
                pieces[j] = tmp;
            }

            // 极小概率一开局就是拼好的，换两块再走
            if (n > 1 && IsIdentity(pieces))
            {
                int tmp = pieces[0];
                pieces[0] = pieces[1];
                pieces[1] = tmp;
            }

            for (int i = 0; i < n; i++)
            {
                PuzzleTile tile = tiles[i];
                tile.CellIndex = i;                 // 格子位置不变，只换里面装哪块图
                DisplayPiece(tile, pieces[i]);
                PlaceTile(tile, false);             // 打乱是瞬间铺开，不做位移动画
            }

            MoveCount = 0;
            IsSolved = false;
            Select(null);

            Debug.Log($"[PuzzleGrid] 已打乱（{gridSize}×{gridSize}）");
        }

        /// <summary>
        /// 把选中的块往 (dx, dy) 方向推一格，和目标格互换。
        /// 越界、没选中、目标格是自己都返回 false。
        /// </summary>
        public bool TrySwapWithNeighbor(int dx, int dy)
        {
            if (selected == null) return false;
            if (dx == 0 && dy == 0) return false;

            int fromCell = selected.CellIndex;
            int col = fromCell % gridSize;
            int row = fromCell / gridSize;

            int targetCol = col + dx;
            int targetRow = row + dy;

            // 撞到棋盘边界就不动
            if (targetCol < 0 || targetCol >= gridSize) return false;
            if (targetRow < 0 || targetRow >= gridSize) return false;

            int targetCell = targetRow * gridSize + targetCol;

            PuzzleTile other = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].CellIndex != targetCell) continue;
                other = tiles[i];
                break;
            }

            if (other == null || other == selected) return false;

            // 只交换"占位"，图跟着块走：
            // 选中的这块带着自己的图挪到目标格，目标格那块被挤回原来的格，
            // 两块在视觉上就是互换位置了。
            other.CellIndex = fromCell;
            selected.CellIndex = targetCell;

            PlaceTile(other, true);
            PlaceTile(selected, true);

            MoveCount++;
            CheckSolved();
            return true;
        }

        // ------------------------------------------------------------------ 内部：切图与布局

        void DisplayPiece(PuzzleTile tile, int pieceIndex)
        {
            tile.Display(pieceIndex, texture, UvRect(pieceIndex));
        }

        /// <summary>原图第 pieceIndex 块在整图里的 UV 矩形（0 = 左上角，行优先）。</summary>
        Rect UvRect(int pieceIndex)
        {
            float g = gridSize;
            int col = pieceIndex % gridSize;
            int row = pieceIndex / gridSize;

            // RawImage 的 UV 原点在左下角，所以行号要上下翻过来
            float u = col / g;
            float v = 1f - (row + 1) / g;

            // 各内缩半个像素，避免放大时相邻块颜色渗进来
            float insetU = 0.5f / Mathf.Max(1, texture.width);
            float insetV = 0.5f / Mathf.Max(1, texture.height);

            return new Rect(u + insetU, v + insetV, 1f / g - insetU * 2f, 1f / g - insetV * 2f);
        }

        /// <summary>把格子摆到它当前 CellIndex 对应的位置。animate = 滑动过去，否则直接落位。</summary>
        void PlaceTile(PuzzleTile tile, bool animate)
        {
            float half = (gridSize - 1) * 0.5f;
            int col = tile.CellIndex % gridSize;
            int row = tile.CellIndex / gridSize;

            // 第 0 行在最上面
            var position = new Vector2((col - half) * step, (half - row) * step);

            if (animate) tile.SlideTo(position, moveDuration);
            else tile.SnapTo(position);
        }

        void CheckSolved()
        {
            bool solved = true;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].PieceIndex == tiles[i].CellIndex) continue;

                solved = false;
                break;
            }

            IsSolved = solved;

            if (solved) Debug.Log($"[PuzzleGrid] 复原完成，用了 {MoveCount} 步。");
        }

        static bool IsIdentity(int[] pieces)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != i) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ 内部：选格

        internal void OnTileClicked(PuzzleTile tile)
        {
            Select(selected == tile ? null : tile);
        }

        void Select(PuzzleTile tile)
        {
            if (selected != null) selected.SetSelected(false);

            selected = tile;

            if (selected != null) selected.SetSelected(true);
        }

        // ------------------------------------------------------------------ 内部：搭 UI

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            // 点选需要事件系统，场景里没有就自己建一个
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform, false);
        }

        void EnsureTexture()
        {
            if (sourceImage != null)
            {
                texture = sourceImage;
            }
            else
            {
                texture = BuildDebugTexture();
                Debug.LogWarning("[PuzzleGrid] Source Image 没赋值，先用自动生成的彩色调试图。" +
                                 "把整图拖到 Inspector 的 Source Image 就能切真图。");
            }

            // 防止相邻块在边界处互相渗色
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        /// <summary>没给图时的兜底：按格子用不同色相画一张，交换机制照样能测。</summary>
        Texture2D BuildDebugTexture()
        {
            int g = Mathf.Max(2, gridSize);
            const int cell = 64;
            int size = g * cell;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "PuzzleDebugTexture"
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int col = x / cell;
                    int row = y / cell;                     // 0 = 最下面一行
                    int index = row * g + col;

                    Color c = Color.HSVToRGB((index + 0.5f) / (g * g), 0.62f, 0.95f);

                    // 每格描一圈深色边，方便看清切块边界
                    if (x % cell == 0 || y % cell == 0) c = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        void BuildUI()
        {
            step = tileSize + spacing;
            float boardSize = gridSize * step - spacing;

            // Canvas：覆盖屏幕，不依赖场景相机
            var canvasGo = new GameObject("PuzzleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 全屏背景
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);

            StretchFull((RectTransform)bgGo.transform);

            var bg = bgGo.GetComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            // 棋盘容器
            var boardGo = new GameObject("Board", typeof(RectTransform));
            boardGo.transform.SetParent(canvasGo.transform, false);

            boardRect = (RectTransform)boardGo.transform;
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.sizeDelta = new Vector2(boardSize, boardSize);
            boardRect.anchoredPosition = new Vector2(0f, 30f);    // 稍微上移，给提示留位置

            // 铺格子
            tiles.Clear();
            for (int i = 0; i < gridSize * gridSize; i++) tiles.Add(CreateTile(i));

            if (showHint) BuildHint(canvasGo.transform);
        }

        PuzzleTile CreateTile(int index)
        {
            var go = new GameObject($"Tile_{index:00}", typeof(RectTransform));
            go.transform.SetParent(boardRect, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(tileSize, tileSize);

            // 边框：铺满整格，未选中时就是一圈暗色缝
            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)borderGo.transform);

            var border = borderGo.GetComponent<Image>();
            border.color = borderIdleColor;
            border.raycastTarget = false;

            // 图块：内缩 borderThickness，露出边框
            var pieceGo = new GameObject("Piece", typeof(RectTransform), typeof(RawImage));
            pieceGo.transform.SetParent(go.transform, false);

            var pieceRect = (RectTransform)pieceGo.transform;
            pieceRect.anchorMin = Vector2.zero;
            pieceRect.anchorMax = Vector2.one;
            pieceRect.offsetMin = new Vector2(borderThickness, borderThickness);
            pieceRect.offsetMax = new Vector2(-borderThickness, -borderThickness);

            var piece = pieceGo.GetComponent<RawImage>();
            piece.texture = texture;
            piece.raycastTarget = true;

            var tile = go.AddComponent<PuzzleTile>();
            tile.Init(this, piece, border, rect);
            tile.Configure(borderIdleColor, borderSelectedColor, selectedScale);
            tile.CellIndex = index;

            return tile;
        }

        void BuildHint(Transform parent)
        {
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1100f, 44f);
            rect.anchoredPosition = new Vector2(0f, 22f);

            var text = go.GetComponent<Text>();
            text.font = LoadFont();
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 1f, 1f, 0.72f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = "左键点选拼图格　↑↓←→ 与相邻格交换　R 重新打乱　Esc 取消选择";
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>优先用系统里的中文字体，内置的 LegacyRuntime.ttf 没有中文字形。</summary>
        static Font LoadFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "PingFang SC", "Arial" }, 24);
                if (font != null) return font;
            }
            catch
            {
                // 忽略，继续试内置字体
            }

            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null) return font;
            }
            catch
            {
                // 忽略
            }

            return null;
        }
    }
}
