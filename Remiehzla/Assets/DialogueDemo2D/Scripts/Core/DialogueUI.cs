using UnityEngine;
using UnityEngine.UI;

namespace DialogueDemo
{
    /// <summary>
    /// 对话框的显示层：只负责文字、淡入淡出与提示，不关心对话流程。
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text contentText;
        [SerializeField] private Text hintText;

        [Header("表现")]
        [SerializeField, Min(0.01f)] private float fadeSpeed = 12f;

        float targetAlpha;

        void Awake()
        {
            if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>();

            UIFontProvider.Apply(speakerText);
            UIFontProvider.Apply(contentText);
            UIFontProvider.Apply(hintText);

            if (panelGroup != null)
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }
        }

        /// <summary>由场景搭建脚本初始化引用。</summary>
        public void Configure(CanvasGroup group, Text speaker, Text content, Text hint)
        {
            panelGroup = group;
            speakerText = speaker;
            contentText = content;
            hintText = hint;
        }

        void Update()
        {
            if (panelGroup == null) return;
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        }

        public void Show()
        {
            targetAlpha = 1f;
            if (panelGroup != null) panelGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            targetAlpha = 0f;
            if (panelGroup != null) panelGroup.blocksRaycasts = false;
        }

        public void SetSpeaker(string speaker)
        {
            if (speakerText == null) return;
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            speakerText.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker) speakerText.text = speaker;
        }

        public void SetText(string text)
        {
            if (contentText != null) contentText.text = text;
        }

        public void SetHint(string hint)
        {
            if (hintText != null) hintText.text = hint;
        }
    }
}
