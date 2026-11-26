using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DialogueDemo
{
    /// <summary>
    /// 2D 场景切换管理器：全屏淡出 → 异步加载目标场景 → 淡入。
    /// 开启 Play 时会自动创建并跨场景保留，不需要手动摆放在场景里。
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("时间")]
        [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float fadeInDuration = 0.4f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.1f;

        [Header("外观")]
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private string loadingText = "加载中…";

        public bool IsTransitioning { get; private set; }
        public event Action<string> TransitionStarted;
        public event Action<string> TransitionCompleted;

        CanvasGroup overlayGroup;
        Image fadeImage;
        Text loadingLabel;
        Text progressLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            EnsureInstance();
        }

        public static SceneTransitionManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var managerObject = new GameObject("SceneTransitionManager");
            DontDestroyOnLoad(managerObject);
            return managerObject.AddComponent<SceneTransitionManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();

            overlayGroup.alpha = 1f;
            overlayGroup.blocksRaycasts = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            StartCoroutine(InitialFadeIn());
        }

        /// <summary>淡出并加载指定场景。场景必须已加入 Build Settings。</summary>
        public bool LoadScene(string sceneName)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("[DialogueDemo2D] 正在切换场景，忽略本次请求。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[DialogueDemo2D] 目标场景名为空。");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[DialogueDemo2D] 场景未加入 Build Settings：" + sceneName);
                return false;
            }

            StartCoroutine(TransitionRoutine(sceneName));
            return true;
        }

        IEnumerator InitialFadeIn()
        {
            IsTransitioning = true;
            yield return null;
            yield return FadeOverlay(1f, 0f, fadeInDuration);
            overlayGroup.blocksRaycasts = false;
            IsTransitioning = false;
        }

        IEnumerator TransitionRoutine(string sceneName)
        {
            IsTransitioning = true;
            overlayGroup.blocksRaycasts = true;
            loadingLabel.text = loadingText;
            progressLabel.text = string.Empty;
            TransitionStarted?.Invoke(sceneName);

            yield return FadeOverlay(0f, 1f, fadeOutDuration);
            if (blackHoldDuration > 0f) yield return new WaitForSecondsRealtime(blackHoldDuration);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError("[DialogueDemo2D] 无法加载场景：" + sceneName);
                loadingLabel.text = "加载失败";
                progressLabel.text = string.Empty;
                yield return new WaitForSecondsRealtime(0.8f);
                yield return FadeOverlay(1f, 0f, fadeInDuration);
                overlayGroup.blocksRaycasts = false;
                IsTransitioning = false;
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                progressLabel.text = Mathf.RoundToInt(operation.progress / 0.9f * 100f) + "%";
                yield return null;
            }

            progressLabel.text = "100%";
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            loadingLabel.text = string.Empty;
            progressLabel.text = string.Empty;
            yield return null;

            yield return FadeOverlay(1f, 0f, fadeInDuration);
            overlayGroup.blocksRaycasts = false;
            IsTransitioning = false;
            TransitionCompleted?.Invoke(sceneName);
        }

        IEnumerator FadeOverlay(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                overlayGroup.alpha = to;
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
                overlayGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            overlayGroup.alpha = to;
        }

        void BuildOverlay()
        {
            var canvasObject = new GameObject(
                "SceneTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var overlay = new GameObject(
                "Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            overlay.transform.SetParent(canvasObject.transform, false);

            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            fadeImage = overlay.GetComponent<Image>();
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = true;

            overlayGroup = overlay.GetComponent<CanvasGroup>();
            overlayGroup.alpha = 1f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = true;

            loadingLabel = CreateText(
                "Loading", overlay.transform, loadingText, 30, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 16f), new Vector2(0f, 76f));

            progressLabel = CreateText(
                "Progress", overlay.transform, string.Empty, 22, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -40f), new Vector2(0f, -8f));
        }

        static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UIFontProvider.Apply(text);
            text.text = content;
            text.fontSize = fontSize;
            text.color = new Color(0.92f, 0.95f, 1f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
