using System;
using UnityEngine;

namespace DialogueDemo
{
    /// <summary>
    /// 对话的唯一入口：推进台词、打字机效果、输入与状态广播。
    /// 场景里只应存在一个实例。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("视图")]
        [SerializeField] private DialogueUI ui;

        [Header("输入")]
        [SerializeField] private KeyCode advanceKey = KeyCode.Space;
        [SerializeField] private KeyCode advanceKeyAlt = KeyCode.E;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private bool advanceWithMouseClick = true;

        [Header("打字机")]
        [SerializeField, Range(5f, 120f)] private float defaultCharsPerSecond = 32f;
        [Tooltip("每次推进后的输入锁定时长，避免一次按键连跳两句")]
        [SerializeField, Min(0f)] private float inputCooldown = 0.15f;

        public bool IsDialogueActive { get; private set; }
        public DialogueData CurrentDialogue { get; private set; }
        public DialogueUI UI => ui;

        public event Action<DialogueData> DialogueStarted;
        public event Action<DialogueData> DialogueFinished;
        public event Action<int, DialogueLine> LineStarted;

        int lineIndex = -1;
        float revealProgress;
        int visibleChars;
        float charsPerSecond;
        bool lineComplete;
        float inputLockedUntil;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DialogueDemo] 场景中存在多个 DialogueManager，已销毁多余的一个。", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (ui == null) ui = FindObjectOfType<DialogueUI>(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>由场景搭建脚本初始化视图引用。</summary>
        public void Configure(DialogueUI dialogueUI)
        {
            ui = dialogueUI;
        }

        /// <summary>开始一段对话。已在对话中或数据无效时返回 false。</summary>
        public bool StartDialogue(DialogueData data)
        {
            if (data == null || !data.IsValid)
            {
                Debug.LogWarning("[DialogueDemo] 对话数据为空或没有台词。", this);
                return false;
            }

            if (IsDialogueActive)
            {
                Debug.Log("[DialogueDemo] 已有对话在进行，忽略本次触发。", this);
                return false;
            }

            CurrentDialogue = data;
            IsDialogueActive = true;
            lineIndex = -1;

            if (ui != null) ui.Show();
            DialogueStarted?.Invoke(data);
            ShowNextLine();
            return true;
        }

        /// <summary>推进：正在打字则立刻显示整句，否则进入下一句。</summary>
        public void Advance()
        {
            if (!IsDialogueActive) return;
            if (Time.unscaledTime < inputLockedUntil) return;
            inputLockedUntil = Time.unscaledTime + inputCooldown;

            if (!lineComplete)
            {
                RevealWholeLine();
                return;
            }

            ShowNextLine();
        }

        /// <summary>立即结束当前对话（例如跳过或触发者中途离开）。</summary>
        public void EndDialogue()
        {
            if (!IsDialogueActive) return;

            DialogueData finished = CurrentDialogue;
            IsDialogueActive = false;
            CurrentDialogue = null;
            lineIndex = -1;

            if (ui != null) ui.Hide();
            DialogueFinished?.Invoke(finished);
        }

        void ShowNextLine()
        {
            if (CurrentDialogue == null) return;

            lineIndex++;
            if (lineIndex >= CurrentDialogue.lines.Count)
            {
                EndDialogue();
                return;
            }

            DialogueLine line = CurrentDialogue.lines[lineIndex];
            revealProgress = 0f;
            visibleChars = 0;
            lineComplete = false;
            charsPerSecond = line.charsPerSecond > 0f ? line.charsPerSecond : defaultCharsPerSecond;
            inputLockedUntil = Time.unscaledTime + inputCooldown;

            if (ui != null)
            {
                ui.SetSpeaker(line.speaker);
                ui.SetText("");
                ui.SetHint(InputHint(false));
            }

            LineStarted?.Invoke(lineIndex, line);
        }

        void Update()
        {
            if (!IsDialogueActive || CurrentDialogue == null) return;
            if (lineIndex < 0 || lineIndex >= CurrentDialogue.lines.Count) return;

            DialogueLine line = CurrentDialogue.lines[lineIndex];
            string fullText = line.text ?? string.Empty;

            if (!lineComplete)
            {
                revealProgress += Mathf.Max(0.01f, charsPerSecond) * Time.unscaledDeltaTime;
                int targetCount = Mathf.Min(Mathf.FloorToInt(revealProgress), fullText.Length);
                if (targetCount != visibleChars)
                {
                    visibleChars = targetCount;
                    if (ui != null) ui.SetText(fullText.Substring(0, visibleChars));
                }

                if (visibleChars >= fullText.Length)
                {
                    lineComplete = true;
                    if (ui != null) ui.SetHint(InputHint(true));
                }
            }

            if (Time.unscaledTime < inputLockedUntil) return;

            if (Input.GetKeyDown(cancelKey))
            {
                EndDialogue();
                return;
            }

            bool advancePressed =
                Input.GetKeyDown(advanceKey) ||
                Input.GetKeyDown(advanceKeyAlt) ||
                (advanceWithMouseClick && Input.GetMouseButtonDown(0));

            if (advancePressed) Advance();
        }

        void RevealWholeLine()
        {
            DialogueLine line = CurrentDialogue.lines[lineIndex];
            string fullText = line.text ?? string.Empty;

            visibleChars = fullText.Length;
            revealProgress = visibleChars;
            lineComplete = true;

            if (ui != null)
            {
                ui.SetText(fullText);
                ui.SetHint(InputHint(true));
            }
        }

        string InputHint(bool revealed)
        {
            return revealed ? "空格 / E 继续　·　Esc 结束" : "空格 / E 跳过打字";
        }
    }
}
