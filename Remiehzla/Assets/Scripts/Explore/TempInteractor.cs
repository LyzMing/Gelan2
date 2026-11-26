using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Remiehzla.Explore
{
    /// <summary>
    /// ⚠️ 临时用的交互检测，只为验证流程。正式的检测（朝向、最近、按键提示 UI）由对方的系统做。
    /// 它做的事：每 0.3 秒扫一遍场景里的可交互物，挑最近的一个显示提示，按 E 交互。
    ///
    /// 对方的系统接进来后，把挂了这个组件的物体删掉即可。
    /// </summary>
    public class TempInteractor : MonoBehaviour
    {
        [Header("交互")]
        public float radius = 1.4f;
        public KeyCode interactKey = KeyCode.E;

        [Tooltip("显示提示文字的 UI。留空会自动生成一个（推荐）")]
        public Text promptLabel;

        [Tooltip("没有目标时提示文字的默认内容")]
        public string idleText = "";

        [Header("自动生成的提示 UI（Prompt Label 留空时生效）")]
        public bool autoCreatePrompt = true;

        [Tooltip("字号")]
        public int promptFontSize = 26;

        [Tooltip("离屏幕底边的距离（像素）。默认放在字幕上方")]
        public float promptBottomMargin = 240f;

        readonly List<IInteractable> candidates = new List<IInteractable>();

        IInteractable current;
        float scanTimer;

        void Awake()
        {
            if (promptLabel != null)
            {
                // 常见坑：Text 有了但字体是空的，文字永远不显示
                if (promptLabel.font == null)
                {
                    Debug.LogWarning("[TempInteractor] Prompt Label 的字体是空的，提示不会显示。" +
                                     "给它指定一个字体，或者清空 Prompt Label 让脚本自动生成", this);
                }

                return;
            }

            if (autoCreatePrompt) BuildPromptUI();
        }

        /// <summary>没配 Prompt Label 时，自己在屏幕下方生成一行提示文字。</summary>
        void BuildPromptUI()
        {
            var canvasGo = new GameObject("InteractPromptCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textGo = new GameObject("Prompt", typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);

            var text = textGo.GetComponent<Text>();
            text.font = LoadFont();
            text.fontSize = promptFontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = "";

            // 没有背景板，靠描边保证在亮背景上看得清
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, promptBottomMargin);
            rect.sizeDelta = new Vector2(900f, 60f);

            promptLabel = text;
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
            catch
            {
                // 拿不到系统字体就走下面的兜底
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void Update()
        {
            scanTimer -= Time.deltaTime;

            if (scanTimer <= 0f)
            {
                scanTimer = 0.3f;
                Rescan();
            }

            current = FindNearest();

            if (promptLabel != null)
                promptLabel.text = current != null ? $"[{interactKey}] {current.Prompt}" : idleText;

            if (current != null && Input.GetKeyDown(interactKey))
                current.Interact(gameObject);
        }

        void Rescan()
        {
            candidates.Clear();

            var all = FindObjectsOfType<MonoBehaviour>();

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is IInteractable interactable && interactable.CanInteract)
                    candidates.Add(interactable);
            }
        }

        IInteractable FindNearest()
        {
            IInteractable best = null;
            float bestDistance = radius * radius;

            for (int i = 0; i < candidates.Count; i++)
            {
                IInteractable candidate = candidates[i];

                if (candidate == null) continue;
                if (!candidate.CanInteract) continue;

                Transform target = candidate.HighlightTarget;
                if (target == null) continue;

                float distance = (target.position - transform.position).sqrMagnitude;

                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }
    }
}
