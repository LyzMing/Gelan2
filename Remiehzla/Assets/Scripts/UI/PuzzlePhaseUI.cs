using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Remiehzla.Puzzle;

namespace Remiehzla.UI
{
    /// <summary>
    /// 拼图阶段的覆盖层 UI：标题、步数、操作提示、确定/重打乱按钮、判定提示。
    ///
    /// 独立于 <see cref="PuzzleGrid"/>（棋盘只管画和逻辑），
    /// 用自己的一张 Canvas（sortingOrder 更高）盖在棋盘上。
    /// 美术要换皮肤时改这里的字段即可；以后想换成预制体，只要保留对外这三个入口：
    ///   event Confirmed / event Rejected / OnConfirm()
    /// </summary>
    [DisallowMultipleComponent]
    public class PuzzlePhaseUI : MonoBehaviour
    {
        [Header("绑定")]
        [Tooltip("留空会自动在场景里找")]
        public PuzzleGrid grid;

        [Header("文案")]
        [Tooltip("{0} = 关卡显示名")]
        public string titleFormat = "{0}";

        [Tooltip("是否显示关卡名。功能保留，暂时隐藏")]
        public bool showTitle;

        public string stepFormat = "步数：{0}";
        public string confirmLabel = "确定";
        public string shuffleLabel = "重新打乱";
        public string wrongHint = "还差一点，再试试";
        public string solvedHint = "拼好了";

        [Header("外观")]
        public Color textColor = new Color(1f, 1f, 1f, 0.92f);
        public Color hintColor = new Color(1f, 1f, 1f, 0.62f);
        public Color buttonColor = new Color(1f, 0.78f, 0.25f, 1f);
        public Color buttonTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        public Color wrongToastColor = new Color(1f, 0.45f, 0.45f, 1f);
        public Color solvedToastColor = new Color(0.55f, 0.95f, 0.60f, 1f);

        [Header("尺寸")]
        public Vector2 referenceResolution = new Vector2(1280f, 720f);
        public int titleFontSize = 26;
        public int bodyFontSize = 18;
        public int buttonFontSize = 22;
        public int toastFontSize = 24;

        [Header("提示停留时间")]
        public float toastHold = 1.4f;

        /// <summary>点“确定”且判定通过。</summary>
        public event Action<PuzzleResult> Confirmed;

        /// <summary>点“确定”但没拼对。</summary>
        public event Action<PuzzleResult> Rejected;

        Canvas canvas;
        Text titleText;
        Text stepText;
        Text hintText;
        Text toastText;
        Button confirmButton;
        Button shuffleButton;
        float toastTime;
        int lastMoveCount = -1;

        // ------------------------------------------------------------------ 生命周期

        void Awake()
        {
            if (grid == null) grid = FindObjectOfType<PuzzleGrid>();

            Build();
        }

        void Start()
        {
            RefreshStaticTexts();
        }

        void Update()
        {
            // 步数只在变化时重建字符串，避免每帧产生垃圾
            if (grid != null && stepText != null && grid.MoveCount != lastMoveCount)
            {
                lastMoveCount = grid.MoveCount;
                stepText.text = string.Format(stepFormat, lastMoveCount);
            }

            if (toastTime <= 0f) return;

            toastTime -= Time.deltaTime;

            if (toastText == null) return;

            float alpha = Mathf.Clamp01(toastTime / Mathf.Max(0.01f, toastHold));
            toastText.color = new Color(toastText.color.r, toastText.color.g, toastText.color.b, alpha);

            if (toastTime <= 0f) toastText.text = "";
        }

        /// <summary>关卡切换后刷新标题/提示（目前一关一个场景，Start 里调一次就够）。</summary>
        public void RefreshStaticTexts()
        {
            if (grid == null) return;

            if (titleText != null)
            {
                string name = grid.Level != null && !string.IsNullOrEmpty(grid.Level.displayName)
                    ? grid.Level.displayName
                    : "";
                titleText.text = string.Format(titleFormat, name);
            }

            if (hintText != null) hintText.text = BuildHint(grid.ActiveOps);

            // 只有开了 allowShuffleInGame 才显示重打乱按钮
            if (shuffleButton != null) shuffleButton.gameObject.SetActive(grid.CanShuffle);
        }

        // ------------------------------------------------------------------ 对外入口

        /// <summary>“确定”按钮。判定通过 → Confirmed；否则抖一下 + 提示。</summary>
        public void OnConfirm()
        {
            if (grid == null) return;

            // 开场对话还没播完：不让判定
            if (!grid.InputEnabled) return;

            PuzzleResult result;

            if (grid.Confirm(out result))
            {
                ShowToast(solvedHint, solvedToastColor);
                Confirmed?.Invoke(result);
                return;
            }

            grid.PlayWrongShake();
            ShowToast(wrongHint, wrongToastColor);
            Rejected?.Invoke(result);
        }

        /// <summary>“重新打乱”按钮（仅调试用）。</summary>
        public void OnShuffle()
        {
            if (grid == null) return;
            if (!grid.InputEnabled) return;

            grid.Shuffle();
            RefreshStaticTexts();
        }

        /// <summary>开关按钮可点状态（开场对话期间关掉）。</summary>
        public void SetButtonsEnabled(bool enabled)
        {
            if (confirmButton != null) confirmButton.interactable = enabled;
            if (shuffleButton != null) shuffleButton.interactable = enabled;
        }

        public void ShowToast(string message, Color color)
        {
            if (toastText == null || string.IsNullOrEmpty(message)) return;

            toastText.text = message;
            toastText.color = color;

            toastTime = toastHold;
        }

        // ------------------------------------------------------------------ 搭 UI

        void Build()
        {
            var canvasGo = new GameObject("PuzzlePhaseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;          // 盖在棋盘上面

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // ---- 标题（顶部居中，默认隐藏）----
            titleText = CreateText("Title", canvasGo.transform, titleFontSize, TextAnchor.MiddleCenter, textColor);
            Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(900f, 44f));
            titleText.gameObject.SetActive(showTitle);

            // ---- 步数（右上）----
            stepText = CreateText("Step", canvasGo.transform, bodyFontSize, TextAnchor.MiddleRight, hintColor);
            Place(stepText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(260f, 32f));

            // ---- 操作提示（顶部居中）----
            hintText = CreateText("Hint", canvasGo.transform, bodyFontSize, TextAnchor.MiddleCenter, hintColor);
            Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1100f, 32f));

            // ---- 确定按钮（右下）----
            confirmButton = CreateButton("ConfirmButton", confirmLabel, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 30f), new Vector2(220f, 64f), buttonColor);
            confirmButton.onClick.AddListener(OnConfirm);

            // ---- 重打乱按钮（左下，仅调试显示）----
            shuffleButton = CreateButton("ShuffleButton", shuffleLabel, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(32f, 32f), new Vector2(170f, 52f), new Color(0.35f, 0.37f, 0.43f, 0.9f));

            // ---- 判定提示（屏幕中部偏下）----
            toastText = CreateText("Toast", canvasGo.transform, toastFontSize, TextAnchor.MiddleCenter, wrongToastColor);
            Place(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(900f, 48f));
        }

        Text CreateText(string name, Transform parent, int fontSize, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = LoadFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        Button CreateButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            image.color = background;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            Text buttonLabel = CreateText("Label", go.transform, buttonFontSize, TextAnchor.MiddleCenter, buttonTextColor);
            StretchFull(buttonLabel.rectTransform);
            buttonLabel.text = label;

            return button;
        }

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        string BuildHint(PuzzleOps ops)
        {
            var builder = new StringBuilder();

            if ((ops & PuzzleOps.Select) != 0) builder.Append("左键点选　");
            if ((ops & PuzzleOps.SwapAdjacent) != 0) builder.Append("↑↓←→ 移动　");
            if ((ops & PuzzleOps.Rotate) != 0) builder.Append("H K 旋转　");
            if ((ops & PuzzleOps.Flip) != 0) builder.Append("J 翻转　");
            if ((ops & PuzzleOps.Drag) != 0) builder.Append("按住拖动　");
            if ((ops & PuzzleOps.Copy) != 0) builder.Append("C 复制　");
            if ((ops & PuzzleOps.Undo) != 0) builder.Append("Z 撤回　");

            builder.Append("滚轮缩放　中键平移");

            return builder.ToString();
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
