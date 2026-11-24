using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    /// <summary>
    /// ⚠️ 占位用的探索场景：房间、家具、可拾取物、门、玩家全部在运行时用色块搭出来，
    /// 只为验证「拼图 → 判定 → 探索 → 拾取 → 进门」这条链路。
    /// 美术资源到位、正式场景做好之后，把这个文件整个删掉即可。
    ///
    /// 场景要长这样：一个空物体挂本组件（放在 ExploreRoot 下面），相机用场景里的正交相机。
    /// </summary>
    public class ExploreDemo : MonoBehaviour
    {
        [Header("占位颜色")]
        public Color groundColor = new Color(0.16f, 0.17f, 0.20f, 1f);
        public Color propColor = new Color(0.32f, 0.30f, 0.36f, 1f);
        public Color playerColor = new Color(0.95f, 0.85f, 0.55f, 1f);
        public Color pickupColor = new Color(1f, 0.78f, 0.25f, 1f);
        public Color doorColor = new Color(0.35f, 0.85f, 0.55f, 1f);
        public Color variantColor = new Color(0.65f, 0.45f, 0.95f, 1f);

        [Header("调试")]
        [Tooltip("是否显示临时操作提示")]
        public bool showHint = true;

        static Sprite pixelSprite;

        Text statusText;
        float statusTimer;

        void Start()
        {
            Build();
        }

        void Update()
        {
            if (statusText == null) return;

            statusTimer -= Time.deltaTime;

            if (statusTimer > 0f) return;

            statusTimer = 0.5f;
            statusText.text = BuildStatusText();
        }

        // ------------------------------------------------------------------ 搭建

        void Build()
        {
            // 地面
            CreateQuad("Ground", new Vector3(0f, -4.5f, 0f), new Vector2(16f, 9f), groundColor, -2000, false);

            // 家具（挂 YSort，玩家走到后面会被挡住）
            // 桌子特意做成「桌面 + 两条腿」，中间是空的：
            // 玩家站到后面时是从腿之间露出来，而不是被一整块实心矩形盖住 —— 正式美术就该这么切。
            // 三块共用一个 SortingGroup + 一个 YSort，作为一个整体参与排序。
            Transform table = CreateGroup("Table", new Vector3(2f, -1.2f, 0f));
            CreateQuadChild("Top", table, new Vector3(0f, 0.95f, 0f), new Vector2(2.4f, 0.2f), propColor, 0);
            CreateQuadChild("Leg_L", table, new Vector3(-1.05f, 0f, 0f), new Vector2(0.18f, 1f), propColor, 0);
            CreateQuadChild("Leg_R", table, new Vector3(1.05f, 0f, 0f), new Vector2(0.18f, 1f), propColor, 0);

            CreateQuad("Shelf", new Vector3(-3.2f, -0.6f, 0f), new Vector2(1.2f, 2f), propColor, 0, true);
            CreateQuad("Crate", new Vector3(5f, -2f, 0f), new Vector2(1f, 1f), propColor, 0, true);

            // 可拾取物
            Transform pickup = CreateQuad("Pickup_Photo", new Vector3(3.4f, -0.9f, 0f), new Vector2(0.45f, 0.45f), pickupColor, 0, true);
            var pickupBehaviour = pickup.gameObject.AddComponent<PickupInteractable>();
            pickupBehaviour.itemId = "L01_PHOTO";
            pickupBehaviour.promptOverride = "捡起合照";

            // 门
            Transform door = CreateQuad("Door", new Vector3(7f, -1.4f, 0f), new Vector2(1.1f, 2.2f), doorColor, 0, true);
            var doorBehaviour = door.gameObject.AddComponent<DoorInteractable>();
            doorBehaviour.targetSceneName = "L02_Classroom";
            doorBehaviour.promptOverride = "进入下一关";

            // 解法分支演示：只在 normal 解下出现（把 showForSolutions 改掉就会消失）
            Transform variant = CreateQuad("Variant_NormalOnly", new Vector3(-6f, 2.2f, 0f), new Vector2(1.2f, 1.2f), variantColor, 0, true);
            var variantBehaviour = variant.gameObject.AddComponent<ExploreVariant>();
            variantBehaviour.showForSolutions = new[] { "normal" };

            // 玩家
            Transform player = CreateQuad("Player", new Vector3(-5f, -3f, 0f), new Vector2(0.6f, 1f), playerColor, 0, true);
            player.gameObject.AddComponent<TempPlayerController>();

            BuildUI(player.gameObject);
        }

        Transform CreateQuad(string quadName, Vector3 position, Vector2 size, Color color, int sortingOrder, bool sortByY)
        {
            Transform quad = CreateQuadChild(quadName, transform, position, size, color, sortingOrder);

            if (sortByY) quad.gameObject.AddComponent<YSort>();

            return quad;
        }

        Transform CreateQuadChild(string quadName, Transform parent, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(quadName, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = Pixel();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            return go.transform;
        }

        /// <summary>一个多 sprite 的物件：SortingGroup + 一个 YSort 让整体算一层。</summary>
        Transform CreateGroup(string groupName, Vector3 position)
        {
            var go = new GameObject(groupName, typeof(SortingGroup));
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.AddComponent<YSort>();

            return go.transform;
        }

        void BuildUI(GameObject player)
        {
            var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            if (showHint)
            {
                Text hint = CreateText("Hint", canvasGo.transform, 16, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(1000f, 26f));
                hint.text = "临时调试：方向键 / WASD 移动　E 交互　（正式移动系统接进来后这份会删掉）";
            }

            Text prompt = CreateText("Prompt", canvasGo.transform, 22, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(900f, 32f));

            statusText = CreateText("Status", canvasGo.transform, 16, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(800f, 26f));
            statusText.text = BuildStatusText();

            var interactor = player.AddComponent<TempInteractor>();
            interactor.promptLabel = prompt;
        }

        Text CreateText(string textName, Transform parent, int fontSize, TextAnchor align,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(textName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = LoadFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = new Color(1f, 1f, 1f, 0.85f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        string BuildStatusText()
        {
            LevelDirector director = FindObjectOfType<LevelDirector>();

            if (director == null || director.Context == null) return "独立调试场景";

            string levelId = director.Context.level != null ? director.Context.level.levelId : "?";
            string solution = string.IsNullOrEmpty(director.Context.puzzle.solutionId)
                ? "未判定"
                : director.Context.puzzle.solutionId;

            return $"{levelId}　解法 {solution}　已收集 {director.Context.collected.Count}";
        }

        // ------------------------------------------------------------------ 占位贴图

        /// <summary>1×1 白色方块，pivot 在底部中点（这样 transform.position 就是"脚点"）。</summary>
        static Sprite Pixel()
        {
            if (pixelSprite != null) return pixelSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderPixel",
                filterMode = FilterMode.Point
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0f), 1f);
            pixelSprite.name = "PlaceholderSquare";

            return pixelSprite;
        }

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
                // 忽略
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
