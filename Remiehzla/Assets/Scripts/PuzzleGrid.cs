using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 拼图原型：切图 → 打乱 → 交换 / 旋转 / 翻转 / 拖拽 → 复原。
    ///
    /// 操作：
    ///   左键点选一块                     选中
    ///   ↑ ↓ ← →                          选中块与相邻格互换
    ///   K / H                            选中块顺时针 / 逆时针转 90°
    ///   J                                选中块水平镜像翻转
    ///   左键按住拖动                      直接拖起来，松手与光标所在格互换；拖到外面则滑回原位
    ///   滚轮                              以光标为中心缩放画面
    ///   按住中键拖动                       平移整个画面
    ///   R / Esc                          重新打乱 / 取消选中
    ///
    /// 所有 UI 都在运行时生成，场景里只需要一个挂着本脚本的空物体。
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

        [Tooltip("拖拽中放大到多少倍（建议比选中略大）。")]
        public float dragScale = 1.12f;

        [Header("拖拽提示")]
        [Tooltip("拖拽时，光标下面那块即将被交换的拼图压暗成这个颜色。")]
        public Color hoverDimColor = new Color(0.42f, 0.44f, 0.52f, 1f);

        [Header("动画速度")]
        [Tooltip("交换 / 拖拽落位 / 拖出界归位的滑动时长（秒）。0 = 瞬间平移。")]
        public float moveDuration = 0.14f;

        [Tooltip("按 H / K 旋转 90° 的时长（秒）。0 = 瞬间旋转。")]
        public float rotateDuration = 0.18f;

        [Tooltip("按 J 水平翻转的时长（秒）。0 = 瞬间翻转。")]
        public float flipDuration = 0.16f;

        [Header("缩放 / 平移")]
        [Tooltip("滚轮灵敏度：每滚一格画面缩放的增量，0.15 = 约 15%。")]
        public float zoomSpeed = 0.15f;

        [Tooltip("画面最小缩放倍数。")]
        public float zoomMin = 0.5f;

        [Tooltip("画面最大缩放倍数。")]
        public float zoomMax = 3f;

        [Tooltip("中键平移的跟随倍率，1 = 画面完全跟手。")]
        public float panSpeed = 1f;

        [Header("提示")]
        [Tooltip("在屏幕下方显示一行操作提示。")]
        public bool showHint = true;

        /// <summary>当前是否已经拼好（位置对 + 方向都正立）。</summary>
        public bool IsSolved { get; private set; }

        /// <summary>本次打乱以来位置交换了几次（旋转/翻转不算步数）。</summary>
        public int MoveCount { get; private set; }

        /// <summary>当前选中的格子，没有选中时为 null。</summary>
        public PuzzleTile Selected => selected;

        readonly List<PuzzleTile> tiles = new List<PuzzleTile>();

        PuzzleTile selected;
        PuzzleTile dragging;
        PuzzleTile hoverTile;
        RectTransform boardRect;
        Canvas canvas;
        Texture2D texture;
        Sprite ringSprite;
        float step;

        Vector2 dragGrabOffset;
        Vector3 lastPanMousePosition;

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
            for (int i = 0; i < tiles.Count; i++) tiles[i].TickAnimation(dt);

            HandleCameraInput();
            HandleInput();
        }

        void HandleInput()
        {
            // 拖拽过程中不接受键盘操作，避免一边拖一边转
            if (dragging != null) return;

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

            if (Input.GetKeyDown(KeyCode.K)) { RotateSelected(true); return; }      // 顺时针
            if (Input.GetKeyDown(KeyCode.H)) { RotateSelected(false); return; }     // 逆时针
            if (Input.GetKeyDown(KeyCode.J)) { FlipSelected(); return; }            // 水平翻转

            int dx = 0, dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;         // 行号向上减小
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            else return;

            TrySwapWithNeighbor(dx, dy);
        }

        // ------------------------------------------------------------------ 打乱

        /// <summary>重新随机打乱（不会打乱成已复原的样子），同时把所有块摆正。</summary>
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
                tile.ResetOrientation();            // 旋转/翻转状态一起清掉
                DisplayPiece(tile, pieces[i]);
                PlaceTile(tile, false);             // 打乱是瞬间铺开，不做位移动画
            }

            MoveCount = 0;
            IsSolved = false;
            Select(null);

            Debug.Log($"[PuzzleGrid] 已打乱（{gridSize}×{gridSize}）");
        }

        // ------------------------------------------------------------------ 交换

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

            PuzzleTile other = FindTileByCell(targetCell);
            if (other == null || other == selected) return false;

            SwapCells(selected, other);
            return true;
        }

        /// <summary>两块互换所在格子，然后各自滑过去。</summary>
        void SwapCells(PuzzleTile a, PuzzleTile b)
        {
            // 只交换"占位"，图（和朝向）跟着块走：视觉上就是两块互换了位置
            int aCell = a.CellIndex;
            int bCell = b.CellIndex;

            a.CellIndex = bCell;
            b.CellIndex = aCell;

            PlaceTile(a, true);
            PlaceTile(b, true);

            MoveCount++;
            CheckSolved();
        }

        // ------------------------------------------------------------------ 旋转 / 翻转

        /// <summary>
        /// 旋转选中的块（clockwise = true 为顺时针 90°）。
        /// 上一段旋转动画没播完时本次输入无效，返回 false。
        /// </summary>
        public bool RotateSelected(bool clockwise)
        {
            if (selected == null) return false;

            // 由 PuzzleTile 把关：动画没播完就拒绝，保证每次成功都是整 90°
            if (!selected.Rotate(clockwise, rotateDuration)) return false;

            // 旋转时四个角会转出格子，得压在别的拼图上面
            selected.BringToFront();

            CheckSolved();
            return true;
        }

        /// <summary>水平镜像翻转选中的块。</summary>
        public void FlipSelected()
        {
            if (selected == null) return;

            selected.BringToFront();
            selected.ToggleFlip(flipDuration);
            CheckSolved();
        }

        // ------------------------------------------------------------------ 拖拽

        internal void OnTileBeginDrag(PuzzleTile tile, PointerEventData eventData)
        {
            if (tile == null) return;

            Select(tile);
            dragging = tile;

            tile.StopSlide();           // 打断没播完的滑动，免得和拖拽抢位置
            tile.SetDragVisual(true);   // 内部会把这档提到最上层
            ClearDragHover();

            Vector2 local;
            if (TryScreenToBoardLocal(eventData != null ? eventData.position : (Vector2)Input.mousePosition, out local))
                dragGrabOffset = tile.TileRect.anchoredPosition - local;
            else
                dragGrabOffset = Vector2.zero;
        }

        internal void OnTileDrag(PuzzleTile tile, PointerEventData eventData)
        {
            if (dragging != tile || tile == null) return;

            Vector2 local;
            if (!TryScreenToBoardLocal(eventData.position, out local)) return;

            // 保持"抓取时手指在图块上的相对位置"，拖起来才不会跳
            tile.TileRect.anchoredPosition = local + dragGrabOffset;

            UpdateDragHover(tile, local);
        }

        internal void OnTileEndDrag(PuzzleTile tile, PointerEventData eventData)
        {
            if (dragging != tile || tile == null) return;

            dragging = null;
            ClearDragHover();           // 松手立刻把压暗恢复
            tile.SetDragVisual(false);

            Vector2 local;
            if (!TryScreenToBoardLocal(eventData.position, out local))
            {
                PlaceTile(tile, true);      // 松手时连坐标都拿不到，直接回原位
                return;
            }

            int targetCell;
            if (!TryGetCellAtLocal(local, out targetCell) || targetCell == tile.CellIndex)
            {
                PlaceTile(tile, true);      // 拖到棋盘外，或拖回自己身上
                return;
            }

            PuzzleTile other = FindTileByCell(targetCell);
            if (other == null || other == tile)
            {
                PlaceTile(tile, true);
                return;
            }

            SwapCells(tile, other);
        }

        /// <summary>更新"光标下面那块"的压暗提示，以及拖拽期间的层级顺序。</summary>
        void UpdateDragHover(PuzzleTile dragged, Vector2 local)
        {
            PuzzleTile target = null;

            int cell;
            if (TryGetCellAtLocal(local, out cell))
            {
                PuzzleTile candidate = FindTileByCell(cell);
                if (candidate != null && candidate != dragged) target = candidate;
            }

            if (target == hoverTile) return;

            if (hoverTile != null) hoverTile.SetDimmed(false, hoverDimColor);

            hoverTile = target;

            if (hoverTile != null) hoverTile.SetDimmed(true, hoverDimColor);

            ApplyDragOrder(dragged, hoverTile);
        }

        /// <summary>
        /// 拖拽期间的层级：拖动的块在最上 → 被换位置的块在它下面 → 其他拼图在最下层。
        /// </summary>
        void ApplyDragOrder(PuzzleTile dragged, PuzzleTile partner)
        {
            if (dragged == null) return;

            dragged.BringToFront();

            if (partner == null || partner == dragged) return;

            // 父物体里最后一个是 dragged，把它排在倒数第二——正好压在其余拼图上面
            int partnerIndex = dragged.transform.parent.childCount - 2;
            if (partnerIndex >= 0 && partner.transform.GetSiblingIndex() != partnerIndex)
                partner.transform.SetSiblingIndex(partnerIndex);
        }

        void ClearDragHover()
        {
            if (hoverTile != null) hoverTile.SetDimmed(false, hoverDimColor);

            hoverTile = null;
        }

        // ------------------------------------------------------------------ 缩放 / 平移

        void HandleCameraInput()
        {
            // 滚轮：以光标为中心缩放
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f) ZoomAtCursor(scroll);

            // 中键：平移整个画面
            if (Input.GetMouseButtonDown(2))
            {
                lastPanMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButton(2))
            {
                Vector3 current = Input.mousePosition;
                Vector3 delta = current - lastPanMousePosition;
                lastPanMousePosition = current;

                if (boardRect != null)
                {
                    float scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                    boardRect.anchoredPosition += new Vector2(delta.x, delta.y) / scaleFactor * panSpeed;
                }
            }
        }

        /// <summary>以鼠标位置为锚点缩放画面（光标下的那块拼图尽量不动）。</summary>
        void ZoomAtCursor(float scroll)
        {
            if (boardRect == null || zoomSpeed <= 0f) return;

            float oldScale = boardRect.localScale.x;

            float lo = Mathf.Min(zoomMin, zoomMax);
            float hi = Mathf.Max(zoomMin, zoomMax);
            float factor = Mathf.Max(0.01f, 1f + scroll * zoomSpeed);
            float newScale = Mathf.Clamp(oldScale * factor, lo, hi);

            if (Mathf.Approximately(newScale, oldScale)) return;

            // 先把光标换算成棋盘本地坐标（要用缩放前的比例），
            // 再按"本地坐标 * 缩放"补回位置差，光标下的点就会停在原地。
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, Input.mousePosition, null, out local))
                boardRect.anchoredPosition += local * (oldScale - newScale);

            boardRect.localScale = new Vector3(newScale, newScale, 1f);
        }

        // ------------------------------------------------------------------ 切图与布局

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

        /// <summary>把棋盘本地坐标换算成格子下标；落在棋盘外返回 false。</summary>
        bool TryGetCellAtLocal(Vector2 local, out int cell)
        {
            float half = (gridSize - 1) * 0.5f;
            float boardHalf = (gridSize * step - spacing) * 0.5f;

            if (Mathf.Abs(local.x) > boardHalf || Mathf.Abs(local.y) > boardHalf)
            {
                cell = -1;
                return false;
            }

            int col = Mathf.RoundToInt(local.x / step + half);
            int row = Mathf.RoundToInt(half - local.y / step);

            // 落在格缝里就吸到最近的格子
            col = Mathf.Clamp(col, 0, gridSize - 1);
            row = Mathf.Clamp(row, 0, gridSize - 1);

            cell = row * gridSize + col;
            return true;
        }

        bool TryScreenToBoardLocal(Vector2 screenPosition, out Vector2 local)
        {
            // ScreenSpaceOverlay 下相机传 null 即可
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPosition, null, out local);
        }

        PuzzleTile FindTileByCell(int cell)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].CellIndex == cell) return tiles[i];
            }

            return null;
        }

        void CheckSolved()
        {
            bool solved = true;
            for (int i = 0; i < tiles.Count; i++)
            {
                PuzzleTile tile = tiles[i];

                // 位置对 + 方向正立 + 没镜像，才算这块复位
                if (tile.PieceIndex == tile.CellIndex && tile.IsUpright) continue;

                solved = false;
                break;
            }

            IsSolved = solved;

            if (solved) Debug.Log($"[PuzzleGrid] 复原完成，位置交换 {MoveCount} 次。");
        }

        static bool IsIdentity(int[] pieces)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != i) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ 选格

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

        // ------------------------------------------------------------------ 搭 UI

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            // 点选和拖拽都需要事件系统，场景里没有就自己建一个
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

        /// <summary>没给图时的兜底：按格子用不同色相画一张，交互逻辑照样能测。</summary>
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

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

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

            // 棋盘容器（缩放和平移都作用在它身上）
            var boardGo = new GameObject("Board", typeof(RectTransform));
            boardGo.transform.SetParent(canvasGo.transform, false);

            boardRect = (RectTransform)boardGo.transform;
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.sizeDelta = new Vector2(boardSize, boardSize);
            boardRect.anchoredPosition = new Vector2(0f, 30f);    // 稍微上移，给提示留位置

            // 铺格子
            ringSprite = BuildRingSprite();     // 16 个格子共用一张描边贴图
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

            // FlipRoot：只负责水平镜像。放在旋转的外层，
            // 镜像轴就永远是格子的横轴（也就是屏幕横轴）。
            var flipGo = new GameObject("FlipRoot", typeof(RectTransform));
            flipGo.transform.SetParent(go.transform, false);

            var flipRect = (RectTransform)flipGo.transform;
            StretchFull(flipRect);

            // SpinRoot：只负责旋转。图块和描边都是它的子物体，
            // 所以转动/翻动的时候描边会跟着这块拼图一起走。
            var spinGo = new GameObject("SpinRoot", typeof(RectTransform));
            spinGo.transform.SetParent(flipGo.transform, false);

            var spinRect = (RectTransform)spinGo.transform;
            StretchFull(spinRect);

            // 图块：内缩 borderThickness，正好被描边圈住
            var pieceGo = new GameObject("Piece", typeof(RectTransform), typeof(RawImage));
            pieceGo.transform.SetParent(spinGo.transform, false);

            var pieceRect = (RectTransform)pieceGo.transform;
            pieceRect.anchorMin = Vector2.zero;
            pieceRect.anchorMax = Vector2.one;
            pieceRect.offsetMin = new Vector2(borderThickness, borderThickness);
            pieceRect.offsetMax = new Vector2(-borderThickness, -borderThickness);

            var piece = pieceGo.GetComponent<RawImage>();
            piece.texture = texture;
            piece.raycastTarget = true;

            // 描边：空心方框，跟图块同一个 SpinRoot，画在图块之后（盖在边缘上）
            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(spinGo.transform, false);
            StretchFull((RectTransform)borderGo.transform);

            var border = borderGo.GetComponent<Image>();
            border.sprite = ringSprite;
            border.type = Image.Type.Sliced;
            border.color = borderIdleColor;
            border.raycastTarget = false;

            var tile = go.AddComponent<PuzzleTile>();
            tile.Init(this, piece, border, rect, flipRect, spinRect);
            tile.Configure(borderIdleColor, borderSelectedColor, selectedScale, dragScale);
            tile.CellIndex = index;

            return tile;
        }

        /// <summary>
        /// 造一张"空心方框"贴图给描边用。
        /// 走 9 宫格切片，所以放大缩小时线宽不会变形；16 个格子共用这一张。
        /// </summary>
        Sprite BuildRingSprite()
        {
            int thickness = Mathf.Max(1, Mathf.RoundToInt(borderThickness));
            int size = thickness * 2 + 4;          // 中间留透明区域给 9 宫格拉伸

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "PuzzleFrameRing",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onRing = x < thickness || x >= size - thickness
                                  || y < thickness || y >= size - thickness;

                    pixels[y * size + x] = onRing ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);        // 传完就不再需要 CPU 端副本

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(thickness, thickness, thickness, thickness));

            sprite.name = "PuzzleFrameRing";
            return sprite;
        }

        void BuildHint(Transform parent)
        {
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1400f, 44f);
            rect.anchoredPosition = new Vector2(0f, 20f);

            var text = go.GetComponent<Text>();
            text.font = LoadFont();
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 1f, 1f, 0.72f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = "左键点选 / 拖动交换　↑↓←→ 移动　H K 旋转　J 翻转　滚轮缩放　中键平移　R 重打乱";
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
