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

        [Header("判定反馈")]
        [Tooltip("点“确定”但没拼对时的抖动时长（秒），0 = 不抖")]
        public float shakeDuration = 0.28f;

        [Tooltip("抖动幅度（像素）")]
        public float shakeStrength = 16f;

        [Tooltip("抖动频率")]
        public float shakeFrequency = 26f;

        [Header("关卡数据")]
        [Tooltip("指向本关的 LevelDefinition。填了就由它决定 gridSize / 图片 / 初始布局 / 正解；" +
                 "留空则用上面 Inspector 里的裸参数随机打乱（原型模式）")]
        public LevelDefinition level;

        /// <summary>当前是否已经拼好（与某一个正解完全一致）。</summary>
        public bool IsSolved { get; private set; }

        /// <summary>拼好时匹配到的解 id；没拼好为 null。</summary>
        public string SolutionId { get; private set; }

        /// <summary>当前匹配的解是否隐藏解。</summary>
        public bool IsHiddenSolution { get; private set; }

        /// <summary>本次打乱以来位置交换了几次（旋转/翻转不算步数）。</summary>
        public int MoveCount { get; private set; }

        /// <summary>当前选中的格子，没有选中时为 null。</summary>
        public PuzzleTile Selected => selected;

        /// <summary>是否正处于"复制中，等左键放下"的状态。</summary>
        public bool IsCopyMode => copyMode;

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

        // ---- 平移 / 抖动 ----
        Vector2 panPosition;
        Vector2 shakeOffset;
        float shakeTime;
        bool shaking;

        // ---- 复制 / 粘贴 ----
        bool copyMode;              // true = 手上拿着一块"复制体"，等左键放下
        PuzzleTile copySource;      // 复制源
        PuzzleTile ghost;           // 跟着光标的复制体（不在 tiles 里，不参与格子逻辑）
        int copyPiece;
        int copyTurns;
        bool copyFlipped;
        float copyAngle;

        // ---- 撤销 ----
        enum ActionKind { Swap, Rotate, Flip, Overwrite }

        struct PuzzleAction
        {
            public ActionKind Kind;
            public PuzzleTile A;
            public PuzzleTile B;
            public int PrevPiece;
            public int PrevTurns;
            public bool PrevFlipped;
            public float PrevAngle;
        }

        const int MaxHistory = 64;

        readonly List<PuzzleAction> history = new List<PuzzleAction>();

        // ------------------------------------------------------------------ 生命周期

        void Awake()
        {
            ApplyLevelConfig();

            EnsureEventSystem();
            EnsureTexture();
            BuildUI();
        }

        void Start()
        {
            if (!LoadInitialLayout()) Shuffle();
        }

        /// <summary>用关卡资产覆盖 Inspector 上的裸参数（图片 / 格数）。</summary>
        void ApplyLevelConfig()
        {
            if (level == null || level.puzzle == null) return;

            gridSize = Mathf.Clamp(level.puzzle.gridSize, 2, 8);

            if (level.puzzle.image != null) sourceImage = level.puzzle.image;
        }

        /// <summary>载入策划摆好的初始布局；没有可用布局时返回 false（由调用方退回随机打乱）。</summary>
        public bool LoadInitialLayout()
        {
            if (level == null || level.puzzle == null) return false;

            PuzzleLayout layout = level.puzzle.initial;

            if (layout == null || layout.cells == null || layout.cells.Length != tiles.Count)
            {
                Debug.LogWarning($"[PuzzleGrid] {(level == null ? "关卡" : level.levelId)} 没有可用的初始布局，" +
                                 "已退回随机打乱。请用 工具 → 拼图布局编辑器 摆好初始状态。");
                return false;
            }

            ApplyLayout(layout);

            MoveCount = 0;
            Select(null);
            CheckSolved();

            Debug.Log($"[PuzzleGrid] 已载入关卡 {level.levelId} 的初始布局（{gridSize}×{gridSize}）");

            return true;
        }

        /// <summary>把一份布局铺到棋盘上（不做动画）。</summary>
        public void ApplyLayout(PuzzleLayout layout)
        {
            if (layout == null) return;

            for (int cell = 0; cell < tiles.Count; cell++)
            {
                PuzzleTile tile = tiles[cell];
                CellState state = cell < layout.cells.Length ? layout.cells[cell] : new CellState(cell);

                tile.CellIndex = cell;
                DisplayPiece(tile, state.pieceIndex);
                tile.RestoreOrientation(state.turnSteps, state.flipped, -90f * state.turnSteps, 0f);
                PlaceTile(tile, false);
            }
        }

        /// <summary>把当前棋盘状态抓成一份布局快照（按格子下标，行优先）。</summary>
        public CellState[] SnapshotCells()
        {
            var cells = new CellState[tiles.Count];

            for (int i = 0; i < tiles.Count; i++)
            {
                PuzzleTile tile = tiles[i];
                cells[tile.CellIndex] = new CellState(tile.PieceIndex, tile.TurnSteps, tile.FlippedX);
            }

            return cells;
        }

        // ------------------------------------------------------------------ 操作开关 / 判定

        /// <summary>这一关是否允许某个操作。没配关卡资产时全部开放（原型模式）。</summary>
        public bool Allows(PuzzleOps op)
        {
            if (level == null || level.puzzle == null) return true;

            return (level.puzzle.allowedOps & op) != 0;
        }

        /// <summary>这一关实际生效的操作集（没配关卡时全开）。给 UI 生成提示用。</summary>
        public PuzzleOps ActiveOps => level != null && level.puzzle != null
            ? level.puzzle.allowedOps
            : (PuzzleOps)(-1);

        /// <summary>当前关卡资产，可能为 null（原型模式）。</summary>
        public LevelDefinition Level => level;

        /// <summary>是否允许游戏内重打乱（调试用，正式关卡默认关）。</summary>
        public bool CanShuffle => level == null || level.puzzle == null || level.puzzle.allowShuffleInGame;

        /// <summary>
        /// 玩家点"确定"时调用：重新判定一次并返回结果。
        /// 不通过时由调用方决定要不要播抖动反馈。
        /// </summary>
        public bool Confirm(out PuzzleResult result)
        {
            CheckSolved();

            result = new PuzzleResult
            {
                levelId = level != null ? level.levelId : "",
                solutionId = SolutionId,
                isHidden = IsHiddenSolution,
                solved = IsSolved,
                moveCount = MoveCount
            };

            return IsSolved;
        }

        /// <summary>判定失败的抖动反馈。</summary>
        public void PlayWrongShake()
        {
            if (shakeDuration <= 0f) return;

            shaking = true;
            shakeTime = 0f;
        }

        void TickShake(float deltaTime)
        {
            if (!shaking) return;

            shakeTime += deltaTime;

            float t = Mathf.Clamp01(shakeTime / Mathf.Max(0.0001f, shakeDuration));
            float damp = 1f - t;

            shakeOffset = new Vector2(Mathf.Sin(shakeTime * shakeFrequency) * shakeStrength * damp, 0f);

            if (t >= 1f)
            {
                shaking = false;
                shakeOffset = Vector2.zero;
            }

            ApplyBoardTransform();
        }

        /// <summary>棋盘位置 = 平移量 + 抖动偏移。所有改位置的地方都要走这里。</summary>
        void ApplyBoardTransform()
        {
            if (boardRect == null) return;

            boardRect.anchoredPosition = panPosition + shakeOffset;
        }

        void Update()
        {
            // 动画先推进，避免输入一多就积压着不动
            float dt = Time.deltaTime;
            for (int i = 0; i < tiles.Count; i++) tiles[i].TickAnimation(dt);

            TickShake(dt);
            HandleCameraInput();
            HandleInput();
            UpdateGhost(dt);
        }

        void HandleInput()
        {
            // 拖拽过程中不接受键盘操作，避免一边拖一边转
            if (dragging != null) return;

            // 复制模式：只响应 Esc 取消，左键由点击事件负责放下
            if (copyMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CancelCopy();

                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                // 正式关卡默认关掉重打乱，避免绕过策划摆好的初始状态
                if (CanShuffle) Shuffle();

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Select(null);
                return;
            }

            // 下面每一项都由关卡的 allowedOps 决定，配了哪些就只响应哪些
            if (Allows(PuzzleOps.Undo) && Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }                  // 撤回
            if (Allows(PuzzleOps.Copy) && Input.GetKeyDown(KeyCode.C)) { BeginCopy(); return; }              // 复制
            if (Allows(PuzzleOps.Rotate) && Input.GetKeyDown(KeyCode.K)) { RotateSelected(true); return; }   // 顺时针
            if (Allows(PuzzleOps.Rotate) && Input.GetKeyDown(KeyCode.H)) { RotateSelected(false); return; }  // 逆时针
            if (Allows(PuzzleOps.Flip) && Input.GetKeyDown(KeyCode.J)) { FlipSelected(); return; }           // 水平翻转

            if (!Allows(PuzzleOps.SwapAdjacent)) return;

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

            CancelCopy();
            history.Clear();

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

        /// <summary>两块互换所在格子，然后各自滑过去（并记入撤销栈）。</summary>
        void SwapCells(PuzzleTile a, PuzzleTile b)
        {
            PushUndo(CaptureAction(ActionKind.Swap, a, b));

            ApplySwap(a, b);

            MoveCount++;
            CheckSolved();
        }

        /// <summary>只做交换本身，不记撤销、不算步数（撤销时复用）。</summary>
        void ApplySwap(PuzzleTile a, PuzzleTile b)
        {
            // 只交换"占位"，图（和朝向）跟着块走：视觉上就是两块互换了位置
            int aCell = a.CellIndex;
            int bCell = b.CellIndex;

            a.CellIndex = bCell;
            b.CellIndex = aCell;

            PlaceTile(a, true);
            PlaceTile(b, true);
        }

        // ------------------------------------------------------------------ 旋转 / 翻转

        /// <summary>
        /// 旋转选中的块（clockwise = true 为顺时针 90°）。
        /// 上一段旋转动画没播完时本次输入无效，返回 false。
        /// </summary>
        public bool RotateSelected(bool clockwise)
        {
            if (selected == null) return false;

            // 先记下"转之前"的样子，只有真正转成功了才进撤销栈
            PuzzleAction snapshot = CaptureAction(ActionKind.Rotate, selected);

            // 由 PuzzleTile 把关：动画没播完就拒绝，保证每次成功都是整 90°
            if (!selected.Rotate(clockwise, rotateDuration)) return false;

            PushUndo(snapshot);

            // 旋转时四个角会转出格子，得压在别的拼图上面
            selected.BringToFront();

            CheckSolved();
            return true;
        }

        /// <summary>水平镜像翻转选中的块。</summary>
        public void FlipSelected()
        {
            if (selected == null) return;

            PuzzleAction snapshot = CaptureAction(ActionKind.Flip, selected);

            selected.BringToFront();
            selected.ToggleFlip(flipDuration);
            PushUndo(snapshot);

            CheckSolved();
        }

        // ------------------------------------------------------------------ 拖拽

        internal void OnTileBeginDrag(PuzzleTile tile, PointerEventData eventData)
        {
            if (tile == null) return;

            // 复制模式 / 复制体还在飞的时候，左键是用来放下的，不响应拖拽
            if (copyMode || ghost != null) return;

            if (!Allows(PuzzleOps.Drag)) return;

            Select(tile);
            dragging = tile;

            tile.StopSlide();           // 打断没播完的滑动，免得和拖拽抢位置
            tile.SetDragVisual(true);   // 内部会把这档提到最上层
            ClearHover();

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
            // 复制模式下"拖了一下再松手"也算放下（此时 uGUI 不会派发 click）
            if (copyMode)
            {
                PlaceCopyAtCursor();
                return;
            }

            if (dragging != tile || tile == null) return;

            dragging = null;
            ClearHover();               // 松手立刻把压暗恢复
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

        /// <summary>点在背景上（棋盘外或格缝里）。复制模式下按"就近放下 / 放弃"处理。</summary>
        void OnBackgroundClicked()
        {
            if (!copyMode) return;

            PlaceCopyAtCursor();
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

        void ClearHover()
        {
            if (hoverTile != null) hoverTile.SetDimmed(false, hoverDimColor);

            hoverTile = null;
        }

        // ------------------------------------------------------------------ 复制 / 粘贴

        /// <summary>
        /// 复制选中的块：生成一个跟着光标的"复制体"，
        /// 之后左键放在哪个格子，就把那块覆盖成复制体的样子；Esc 取消。
        /// </summary>
        public bool BeginCopy()
        {
            if (selected == null || copyMode || dragging != null) return false;

            CancelCopy();       // 清掉可能还在飞的上一块

            copySource = selected;
            copyPiece = selected.PieceIndex;
            copyTurns = selected.TurnSteps;
            copyFlipped = selected.FlippedX;
            copyAngle = selected.AngleTarget;

            // 复用 CreateTile 造一块"幽灵"，它不在 tiles 里，不参与格子逻辑
            ghost = CreateTile(selected.CellIndex);
            ghost.Display(copyPiece, texture, UvRect(copyPiece));
            ghost.RestoreOrientation(copyTurns, copyFlipped, copyAngle, 0f);
            ghost.SetSelected(true);
            ghost.SetDragVisual(true);          // 放大 + 提到最上层
            ghost.TileRect.anchoredPosition = selected.TileRect.anchoredPosition;

            copyMode = true;
            ClearHover();

            Debug.Log("[PuzzleGrid] 复制中：左键放下覆盖，Esc 取消");

            return true;
        }

        /// <summary>取消复制（Esc、点到棋盘外、或重新开始复制）。</summary>
        public void CancelCopy()
        {
            copyMode = false;
            ClearHover();
            DestroyGhost();
        }

        void DestroyGhost()
        {
            if (ghost == null) return;

            Destroy(ghost.gameObject);
            ghost = null;
        }

        /// <summary>把复制体放到光标所在的格子，覆盖那一格的拼图。</summary>
        void PlaceCopyAtCursor()
        {
            if (!copyMode) return;

            Vector2 local;
            int cell;

            // 棋盘外 / 取不到坐标：当作放弃
            if (!TryScreenToBoardLocal(Input.mousePosition, out local) || !TryGetCellAtLocal(local, out cell))
            {
                CancelCopy();
                return;
            }

            PuzzleTile target = FindTileByCell(cell);

            // 放到自己身上没有意义，直接取消
            if (target == null || target == copySource)
            {
                CancelCopy();
                return;
            }

            // 记下被覆盖前的样子，F/Z 能撤回
            PushUndo(CaptureAction(ActionKind.Overwrite, target));

            // 覆盖：目标格变成复制体的图 + 朝向
            DisplayPiece(target, copyPiece);
            target.RestoreOrientation(copyTurns, copyFlipped, copyAngle, rotateDuration);

            copyMode = false;
            ClearHover();
            CheckSolved();

            // 复制体滑到目标格再消失，视觉上就是"放下去了"
            if (ghost != null) ghost.SlideTo(CellPosition(cell), moveDuration);
        }

        void UpdateGhost(float deltaTime)
        {
            if (ghost == null) return;

            if (copyMode)
            {
                // 跟手：复制体中心贴着光标
                Vector2 local;
                if (TryScreenToBoardLocal(Input.mousePosition, out local))
                {
                    ghost.TileRect.anchoredPosition = local;
                    UpdateCopyHover(local);
                }

                return;
            }

            // 放下之后：让复制体滑进格子，落位就销毁
            ghost.TickAnimation(deltaTime);

            if (!ghost.IsAnimating) DestroyGhost();
        }

        /// <summary>复制模式下压暗光标下面那一格（源块自身不算目标）。</summary>
        void UpdateCopyHover(Vector2 local)
        {
            PuzzleTile target = null;

            int cell;
            if (TryGetCellAtLocal(local, out cell))
            {
                PuzzleTile candidate = FindTileByCell(cell);
                if (candidate != null && candidate != copySource) target = candidate;
            }

            if (target == hoverTile) return;

            if (hoverTile != null) hoverTile.SetDimmed(false, hoverDimColor);

            hoverTile = target;

            if (hoverTile != null) hoverTile.SetDimmed(true, hoverDimColor);
        }

        // ------------------------------------------------------------------ 撤销

        PuzzleAction CaptureAction(ActionKind kind, PuzzleTile tile, PuzzleTile other = null)
        {
            return new PuzzleAction
            {
                Kind = kind,
                A = tile,
                B = other,
                PrevPiece = tile != null ? tile.PieceIndex : 0,
                PrevTurns = tile != null ? tile.TurnSteps : 0,
                PrevFlipped = tile != null && tile.FlippedX,
                PrevAngle = tile != null ? tile.AngleTarget : 0f
            };
        }

        void PushUndo(PuzzleAction action)
        {
            history.Add(action);

            // 只保留最近若干步，避免长时间游玩一直攒
            if (history.Count > MaxHistory) history.RemoveAt(0);
        }

        /// <summary>撤销上一步操作，返回是否真的撤了。</summary>
        public bool Undo()
        {
            if (history.Count == 0) return false;
            if (dragging != null || copyMode) return false;

            int last = history.Count - 1;
            PuzzleAction action = history[last];
            history.RemoveAt(last);

            switch (action.Kind)
            {
                case ActionKind.Swap:
                    if (action.A != null && action.B != null)
                    {
                        ApplySwap(action.A, action.B);      // 再换回来
                        MoveCount = Mathf.Max(0, MoveCount - 1);
                    }
                    break;

                case ActionKind.Overwrite:
                    if (action.A != null)
                    {
                        DisplayPiece(action.A, action.PrevPiece);
                        action.A.RestoreOrientation(action.PrevTurns, action.PrevFlipped, action.PrevAngle, rotateDuration);
                    }
                    break;

                case ActionKind.Rotate:
                case ActionKind.Flip:
                    if (action.A != null)
                    {
                        // 绝对恢复，所以哪怕动画还在播也能准确回到记录时的角度
                        action.A.RestoreOrientation(action.PrevTurns, action.PrevFlipped, action.PrevAngle, rotateDuration);
                    }
                    break;
            }

            CheckSolved();
            return true;
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
                    panPosition += new Vector2(delta.x, delta.y) / scaleFactor * panSpeed;
                    ApplyBoardTransform();
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
                panPosition += local * (oldScale - newScale);

            boardRect.localScale = new Vector3(newScale, newScale, 1f);
            ApplyBoardTransform();
        }

        // ------------------------------------------------------------------ 切图与布局

        void DisplayPiece(PuzzleTile tile, int pieceIndex)
        {
            // 错乱块（-1）没有对应的图块区域，给个整图 UV 让它真的透明
            Rect uv = pieceIndex >= 0 ? UvRect(pieceIndex) : new Rect(0f, 0f, 1f, 1f);

            tile.Display(pieceIndex, texture, uv);
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

        /// <summary>格子下标对应的棋盘本地坐标（第 0 行在最上面）。</summary>
        Vector2 CellPosition(int cell)
        {
            float half = (gridSize - 1) * 0.5f;
            int col = cell % gridSize;
            int row = cell / gridSize;

            return new Vector2((col - half) * step, (half - row) * step);
        }

        /// <summary>把格子摆到它当前 CellIndex 对应的位置。animate = 滑动过去，否则直接落位。</summary>
        void PlaceTile(PuzzleTile tile, bool animate)
        {
            Vector2 position = CellPosition(tile.CellIndex);

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

        /// <summary>
        /// 判定当前棋盘是否与某个正解完全一致。
        /// 有 LevelDefinition 时按 solutions 比对；没有时退回"每块回原位且正立"的原型判定。
        /// 注意：这里只是更新状态，真正的"过关"要等玩家点确认（由流程层调用）。
        /// </summary>
        void CheckSolved()
        {
            CellState[] cells = SnapshotCells();

            bool wasSolved = IsSolved;
            bool hidden = false;
            string matched = null;

            if (level != null && level.puzzle != null
                && level.puzzle.solutions != null && level.puzzle.solutions.Length > 0)
            {
                matched = PuzzleRules.MatchSolution(level.puzzle.solutions, cells, out hidden);
            }
            else if (PuzzleRules.IsIdentity(cells))
            {
                matched = "normal";
            }

            IsSolved = matched != null;
            SolutionId = matched;
            IsHiddenSolution = hidden;

            if (IsSolved && !wasSolved)
                Debug.Log($"[PuzzleGrid] 与正解一致：{matched}{(hidden ? "（隐藏解）" : "")}，交换 {MoveCount} 次。");
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
            // 复制模式下左键 = 把这个复制体放下去（点哪一格由光标位置决定）
            if (copyMode)
            {
                PlaceCopyAtCursor();
                return;
            }

            // 复制体是临时物体，不算可选的格子
            if (tile == null || tile == ghost) return;

            if (!Allows(PuzzleOps.Select)) return;

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

            // 背景要能被点到：复制模式下点棋盘外/格缝 = 放弃或就近放下
            bg.raycastTarget = true;

            var bgTrigger = bgGo.AddComponent<EventTrigger>();
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ => OnBackgroundClicked());
            bgTrigger.triggers.Add(clickEntry);

            // 棋盘容器（缩放和平移都作用在它身上）
            var boardGo = new GameObject("Board", typeof(RectTransform));
            boardGo.transform.SetParent(canvasGo.transform, false);

            boardRect = (RectTransform)boardGo.transform;
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.sizeDelta = new Vector2(boardSize, boardSize);

            panPosition = new Vector2(0f, 30f);     // 稍微上移，给底部 UI 留位置
            ApplyBoardTransform();

            // 铺格子
            ringSprite = BuildRingSprite();     // 16 个格子共用一张描边贴图
            tiles.Clear();
            for (int i = 0; i < gridSize * gridSize; i++) tiles.Add(CreateTile(i));
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

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
