using UnityEngine;
using UnityEngine.UI;
using Remiehzla.Flow;

namespace Remiehzla.UI
{
    /// <summary>
    /// 结束界面。用 <see cref="Show"/> 从任何地方弹出来。
    ///
    /// 两种用法：
    ///   A) **手摆 UI（推荐）**：菜单 工具 → 界面 → 生成结束界面，
    ///      生成物默认是隐藏的，Show() 时显示；位置颜色随便调。
    ///   B) 什么都不做：Show() 时自动生成一套临时界面。
    /// </summary>
    [DisallowMultipleComponent]
    public class EndScreen : MonoBehaviour
    {
        public static EndScreen Instance { get; private set; }

        [Header("界面引用（用 工具 → 界面 → 生成结束界面 会自动填好）")]
        [Tooltip("整块面板。Show() 时显示它")]
        public GameObject panel;

        public Text titleText;
        public Text messageText;
        public Button backButton;
        public Text footerText;

        [Header("文案")]
        public string title = "结束";
        [TextArea(2, 4)]
        public string message = "";
        public string buttonLabel = "回到开始";
        public string footer = "感谢游玩";

        [Header("临时界面（仅在没摆 UI 时使用）")]
        public Color background = new Color(0.05f, 0.06f, 0.08f, 1f);
        public Color titleColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        public Color messageColor = new Color(0.74f, 0.76f, 0.82f, 1f);

        Canvas runtimeCanvas;

        /// <summary>显示结束界面（重复调用只会更新文字）。</summary>
        public static void Show(string title, string message = "")
        {
            if (Instance == null)
            {
                var go = new GameObject("EndScreen");
                Instance = go.AddComponent<EndScreen>();
            }

            Instance.title = title;
            Instance.message = message;
            Instance.Present();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Present()
        {
            if (panel == null) BuildRuntimeFallback();

            if (panel != null) panel.SetActive(true);

            Bind();
            ApplyTexts();
        }

        void Bind()
        {
            if (backButton == null) return;

            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        void ApplyTexts()
        {
            // 标题和正文是运行时传进来的（两套结局内容不同），必须写
            if (titleText != null) titleText.text = title;
            if (messageText != null) messageText.text = message;

            // 按钮文字 / 页脚属于静态外观，已经摆好 UI 时尊重场景里的写法
            if (runtimeCanvas == null) return;

            if (footerText != null) footerText.text = footer;

            if (backButton != null)
            {
                Text label = backButton.GetComponentInChildren<Text>(true);

                if (label != null) label.text = buttonLabel;
            }
        }

        void OnBackClicked()
        {
            // 回到开始界面：清掉本次的收集记录，重新加载第一个场景
            ProgressService.ResetAll();
            GameSession.Reset();

            Debug.Log("[EndScreen] 重置进度，回到开始界面");

            if (runtimeCanvas != null) runtimeCanvas.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);

            SceneFlow.Load(string.IsNullOrEmpty(GameSession.PuzzleSceneName)
                ? "Puzzle"
                : GameSession.PuzzleSceneName);
        }

        // ------------------------------------------------------------------ 没摆 UI 时的兜底

        void BuildRuntimeFallback()
        {
            var existing = transform.Find("EndScreenCanvas");

            if (existing != null)
            {
                panel = existing.gameObject;
                return;
            }

            Debug.LogWarning("[EndScreen] 没有指定界面引用，运行时临时生成一个。" +
                             "想要能自己调，用菜单 工具 → 界面 → 生成结束界面", this);

            runtimeCanvas = UiFactory.CreateCanvas("EndScreenCanvas", 600, transform);

            panel = UiFactory.CreateFullScreen(runtimeCanvas.transform, "Backdrop", background).gameObject;

            titleText = UiFactory.CreateText(panel.transform, "Title", 64, titleColor, TextAnchor.MiddleCenter);
            UiFactory.Anchor(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f), new Vector2(1500f, 120f));

            messageText = UiFactory.CreateText(panel.transform, "Message", 26, messageColor, TextAnchor.UpperCenter);
            UiFactory.Anchor(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(1300f, 200f));

            backButton = UiFactory.CreateButton(panel.transform, "BackButton", buttonLabel, 30,
                new Vector2(0f, -150f), new Vector2(400f, 90f),
                new Color(0.94f, 0.76f, 0.36f, 1f), new Color(0.12f, 0.12f, 0.14f, 1f));

            footerText = UiFactory.CreateText(panel.transform, "Footer", 18,
                new Color(0.45f, 0.47f, 0.53f, 1f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(footerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(1200f, 40f));
        }
    }
}
