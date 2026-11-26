using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Remiehzla.Flow;
using Remiehzla.Explore;

namespace Remiehzla.Dialogue
{
    /// <summary>
    /// 字幕播放器：屏幕下方逐字显示，一句播完停留一会儿自动进下一句，不需要玩家按键。
    ///
    /// 特点：
    ///   · 自己建 Canvas（场景里只要有一个物体挂它，或者第一次调用时自动创建）
    ///   · 播放期间 <see cref="IsAnyPlaying"/> 为 true，所有可交互物会被自动锁住
    ///   · 可以按需锁住玩家移动（物品交互触发时锁，区域触发时不锁）
    ///   · 对话带奖励时，触发瞬间就给玩家，并弹一个提示
    /// </summary>
    [DisallowMultipleComponent]
    public class SubtitlePlayer : MonoBehaviour, IDialogueService
    {
        static SubtitlePlayer instance;
        static bool quitting;

        /// <summary>场景里没有就自动建一个，用起来不用先摆物体。</summary>
        public static SubtitlePlayer Instance
        {
            get
            {
                if (instance != null) return instance;
                if (quitting) return null;

                instance = FindObjectOfType<SubtitlePlayer>();

                if (instance == null)
                {
                    var go = new GameObject("SubtitlePlayer");
                    instance = go.AddComponent<SubtitlePlayer>();
                }

                return instance;
            }
        }

        /// <summary>是否正在播字幕（交互锁用；不会触发自动创建）。</summary>
        public static bool IsAnyPlaying => instance != null && instance.IsPlaying;

        [Header("文本")]
        [Tooltip("默认打字速度（字/秒）")]
        public float charsPerSecond = 26f;

        [Tooltip("一句播完后停留多久再进下一句（秒）")]
        public float holdAfterLine = 1.1f;

        [Tooltip("整段播完后再等多久才恢复（给最后一句留点缓冲）")]
        public float endDelay = 0.35f;

        [Header("外观")]
        public int fontSize = 28;
        public Color textColor = Color.white;

        [Tooltip("字幕区域高度")]
        public float barHeight = 150f;

        [Tooltip("离屏幕底边的距离。0 = 贴着底边")]
        public float bottomMargin = 0f;

        [Tooltip("字幕背景条颜色。alpha 设 0 就是不要背景")]
        public Color barColor = new Color(0f, 0f, 0f, 0.66f);

        [Header("奖励提示")]
        public float toastSeconds = 2.2f;

        public bool IsPlaying { get; private set; }

        Canvas canvas;
        GameObject bar;
        Text subtitleText;
        Text toastText;
        Coroutine routine;
        Coroutine toastRoutine;
        Action currentComplete;
        bool lockedByUs;

        TempPlayerController playerController;
        PlayerAnimator playerAnimator;

        // ------------------------------------------------------------------ 生命周期

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            BuildUI();
            ShowBar(false);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        // ------------------------------------------------------------------ 对外

        public bool Play(DialogueAsset dialogue, Action onComplete = null, bool lockMovement = false)
        {
            if (dialogue == null || !dialogue.IsValid)
            {
                Debug.LogWarning("[Subtitle] 对话数据为空或没有台词", this);
                return false;
            }

            if (IsPlaying)
            {
                // 正在播的时候不打断，也不排队
                return false;
            }

            currentComplete = onComplete;
            lockedByUs = lockMovement;

            if (lockMovement) SetPlayerControl(false);

            // 只要触发就发奖励
            GrantReward(dialogue);

            routine = StartCoroutine(Run(dialogue));

            return true;
        }

        public void Skip()
        {
            if (!IsPlaying) return;

            if (routine != null) StopCoroutine(routine);

            Finish();
        }

        // ------------------------------------------------------------------ 播放

        IEnumerator Run(DialogueAsset dialogue)
        {
            IsPlaying = true;
            ShowBar(true);

            DialogueLine[] lines = dialogue.lines;

            for (int i = 0; i < lines.Length; i++)
            {
                DialogueLine line = lines[i];
                string full = line.text ?? string.Empty;

                float speed = line.charsPerSecond > 0f ? line.charsPerSecond : charsPerSecond;
                speed = Mathf.Max(1f, speed);

                subtitleText.text = "";

                int visible = 0;
                float acc = 0f;

                while (visible < full.Length)
                {
                    acc += Time.unscaledDeltaTime * speed;

                    int target = Mathf.Min(full.Length, Mathf.FloorToInt(acc));

                    if (target != visible)
                    {
                        visible = target;
                        subtitleText.text = full.Substring(0, visible);
                    }

                    yield return null;
                }

                float hold = line.holdAfter > 0f ? line.holdAfter : holdAfterLine;

                float waited = 0f;

                while (waited < hold)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 最后一句多停一下，别一闪就没
            float tail = 0f;

            while (tail < endDelay)
            {
                tail += Time.unscaledDeltaTime;
                yield return null;
            }

            Finish();
        }

        void Finish()
        {
            routine = null;
            IsPlaying = false;

            ShowBar(false);

            if (lockedByUs) SetPlayerControl(true);

            lockedByUs = false;

            Action callback = currentComplete;
            currentComplete = null;

            callback?.Invoke();
        }

        // ------------------------------------------------------------------ 奖励

        void GrantReward(DialogueAsset dialogue)
        {
            if (!dialogue.HasReward) return;

            if (ProgressService.HasCollected(dialogue.rewardItemId)) return;

            ProgressService.Collect(dialogue.rewardItemId);
            GameEvents.RaiseItemCollected(dialogue.rewardItemId);

            string label = string.IsNullOrEmpty(dialogue.rewardLabel) ? dialogue.rewardItemId : dialogue.rewardLabel;

            ShowToast($"获得：{label}");

            Debug.Log($"[Subtitle] 获得物品：{dialogue.rewardItemId}");
        }

        void ShowToast(string message)
        {
            if (toastText == null) return;

            if (toastRoutine != null) StopCoroutine(toastRoutine);

            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;

            var group = toastText.GetComponent<CanvasGroup>();

            if (group == null) group = toastText.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 1f;

            // 立刻切到画面提示层级，别被字幕条挡住
            toastText.transform.SetAsLastSibling();

            float hold = Mathf.Max(0.3f, toastSeconds - 0.6f);
            float waited = 0f;

            while (waited < hold)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            float fade = 0.6f;
            float done = 0f;

            while (done < fade)
            {
                done += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(1f, 0f, done / fade);
                yield return null;
            }

            group.alpha = 0f;
            toastRoutine = null;
        }

        // ------------------------------------------------------------------ 玩家控制

        void SetPlayerControl(bool enabled)
        {
            if (playerController == null) playerController = FindObjectOfType<TempPlayerController>();
            if (playerAnimator == null) playerAnimator = FindObjectOfType<PlayerAnimator>();

            if (playerController != null) playerController.SetControlEnabled(enabled);
            if (playerAnimator != null) playerAnimator.SetControlEnabled(enabled);
        }

        // ------------------------------------------------------------------ UI

        void BuildUI()
        {
            var canvasGo = new GameObject("SubtitleCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 底部字幕区域：只要文字，不画背景板
            // 底部字幕条（带背景板，策划要的可读性优先）
            var barGo = new GameObject("SubtitleBar", typeof(Image));
            barGo.transform.SetParent(canvasGo.transform, false);

            var barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, barHeight);
            barRect.anchoredPosition = new Vector2(0f, bottomMargin);

            var barImage = barGo.GetComponent<Image>();
            barImage.color = barColor;
            barImage.raycastTarget = false;

            bar = barGo;

            subtitleText = CreateText("SubtitleText", barGo.transform, fontSize, textColor);
            subtitleText.alignment = TextAnchor.MiddleCenter;

            var textRect = subtitleText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(80f, 20f);
            textRect.offsetMax = new Vector2(-80f, -20f);

            // 奖励提示（顶部居中）
            toastText = CreateText("Toast", canvasGo.transform, fontSize + 2, new Color(1f, 0.92f, 0.55f));
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.fontStyle = FontStyle.Bold;

            var toastRect = toastText.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.5f, 1f);
            toastRect.anchorMax = new Vector2(0.5f, 1f);
            toastRect.pivot = new Vector2(0.5f, 1f);
            toastRect.anchoredPosition = new Vector2(0f, -70f);
            toastRect.sizeDelta = new Vector2(900f, 60f);

            var toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
        }

        Text CreateText(string name, Transform parent, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = LoadFont();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;

            return text;
        }

        void ShowBar(bool visible)
        {
            if (bar != null) bar.SetActive(visible);
        }

        static Font LoadFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei", "微软雅黑", "SimHei", "黑体",
                    "Noto Sans CJK SC", "PingFang SC", "Arial Unicode MS"
                }, 32);

                if (font != null) return font;
            }
            catch (Exception)
            {
                // 拿不到系统字体就走下面的兜底
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
