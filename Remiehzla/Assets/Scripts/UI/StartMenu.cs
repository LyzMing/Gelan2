using UnityEngine;
using UnityEngine.UI;
using Remiehzla.Flow;

namespace Remiehzla.UI
{
    /// <summary>
    /// 开始界面。挂在第一个加载的场景（通常是 Puzzle.unity）。
    ///
    /// 两种用法：
    ///   A) **手摆 UI（推荐）**：先用菜单 工具 → 界面 → 生成开始界面 把界面造出来，
    ///      再按自己的喜好调位置/颜色/字号/贴图。本组件的引用会自动填好。
    ///   B) 什么都不做：运行时自动生成一套临时界面（调整不了，只保证能跑）。
    ///
    /// 机制：有它在、且本次运行还没开始过 → 拦住 LevelDirector，拼图不会开始；
    /// 点「开始游戏」→ 广播 GameSession.Started → 拼图（含开场对话）才真正开始。
    /// </summary>
    [DisallowMultipleComponent]
    public class StartMenu : MonoBehaviour
    {
        [Header("界面引用（用 工具 → 界面 → 生成开始界面 会自动填好）")]
        [Tooltip("整块面板。点开始后隐藏它")]
        public GameObject panel;

        public Text titleText;
        public Text subtitleText;
        public Text hintText;
        public Button startButton;

        [Header("文案（只用于运行时临时界面）")]
        [Tooltip("已经摆好 UI 时不会覆盖场景里的文字，想改文字直接在场景里改")]
        public string gameTitle = "拾忆拼图";
        public string subtitle = "把记忆拼回来";
        public string startLabel = "开始游戏";
        public string hint = "方向键移动　E 交互　K / J 旋转翻转";

        [Header("行为")]
        [Tooltip("点开始后要加载的场景名。留空 = 直接进入当前场景")]
        public string startSceneName = "";

        [Header("临时界面（仅在没摆 UI 时使用）")]
        public Color background = new Color(0.07f, 0.08f, 0.11f, 1f);
        public Color titleColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        public Color subtitleColor = new Color(0.72f, 0.74f, 0.80f, 1f);
        public Color buttonColor = new Color(0.94f, 0.76f, 0.36f, 1f);
        public Color buttonTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);

        void Start()
        {
            if (GameSession.HasStarted)
            {
                if (panel != null) panel.SetActive(false);

                gameObject.SetActive(false);
                return;
            }

            // 只有"运行时临时生成"的那套才用这里的文案；
            // 已经摆好 UI 的话，尊重场景里的文字，不覆盖
            if (panel == null)
            {
                BuildRuntimeFallback();
                ApplyTexts();
            }

            Bind();
        }

        void Bind()
        {
            if (startButton == null) return;

            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }

        void ApplyTexts()
        {
            if (titleText != null) titleText.text = gameTitle;
            if (subtitleText != null) subtitleText.text = subtitle;
            if (hintText != null) hintText.text = hint;

            if (startButton != null)
            {
                Text label = startButton.GetComponentInChildren<Text>(true);

                if (label != null) label.text = startLabel;
            }
        }

        void OnStartClicked()
        {
            if (panel != null) panel.SetActive(false);

            Debug.Log("[StartMenu] 开始游戏");

            if (!string.IsNullOrEmpty(startSceneName))
            {
                SceneFlow.Load(startSceneName);
                return;
            }

            // 广播出去，LevelDirector 收到后才开始拼图
            GameSession.MarkStarted();
        }

        // ------------------------------------------------------------------ 没摆 UI 时的兜底

        void BuildRuntimeFallback()
        {
            Debug.LogWarning("[StartMenu] 没有指定界面引用，运行时临时生成一个。" +
                             "想要能自己调，用菜单 工具 → 界面 → 生成开始界面", this);

            Canvas canvas = UiFactory.CreateCanvas("StartMenuCanvas", 500, transform);

            panel = UiFactory.CreateFullScreen(canvas.transform, "Backdrop", background).gameObject;

            titleText = UiFactory.CreateText(panel.transform, "Title", 72, titleColor, TextAnchor.MiddleCenter);
            UiFactory.Anchor(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f), new Vector2(1500f, 130f));

            subtitleText = UiFactory.CreateText(panel.transform, "Subtitle", 28, subtitleColor, TextAnchor.MiddleCenter);
            UiFactory.Anchor(subtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(1500f, 60f));

            startButton = UiFactory.CreateButton(panel.transform, "StartButton", startLabel, 32,
                new Vector2(0f, -100f), new Vector2(420f, 96f), buttonColor, buttonTextColor);

            hintText = UiFactory.CreateText(panel.transform, "Hint", 20,
                new Color(0.50f, 0.52f, 0.58f, 1f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 70f), new Vector2(1200f, 40f));
        }
    }
}
