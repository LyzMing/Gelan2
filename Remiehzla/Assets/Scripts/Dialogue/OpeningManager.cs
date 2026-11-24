using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class OpeningManager : MonoBehaviour
{
    [Header("对话UI")]
    public GameObject dialoguePanel;
    public TMP_Text nameText;
    public TMP_Text dialogueText;
    public Button nextButton;

    [Header("对话内容")]
    public string[] names;
    [TextArea(2, 5)]
    public string[] sentences;

    [Header("打字机设置")]
    public float typingSpeed = 0.05f;

    [Header("自动播放设置")]
    public bool autoPlay = true;
    public float autoPlayDelay = 2f;

    private int index = 0;
    private Coroutine typingCoroutine;
    private Coroutine autoPlayCoroutine;
    private bool isTyping = false;
    private bool isFinished = false;

    void Start()
    {
        dialoguePanel.SetActive(true);

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextDialogue);
        }

        // 自动开始对话
        StartDialogue();
    }

    // 开始对话
    public void StartDialogue()
    {
        index = 0;
        isFinished = false;

        if (sentences == null || sentences.Length == 0)
        {
            EndDialogue();
            return;
        }

        ShowDialogue();
    }

    // 显示当前句子
    void ShowDialogue()
    {
        if (index >= sentences.Length)
        {
            EndDialogue();
            return;
        }

        // 停止之前的协程
        StopDialogueCoroutines();

        // 显示说话人
        if (nameText != null)
        {
            if (names != null && index < names.Length)
                nameText.text = names[index];
            else
                nameText.text = "";
        }

        // 开始打字
        typingCoroutine = StartCoroutine(TypeText());
    }

    // 逐字显示
    IEnumerator TypeText()
    {
        isTyping = true;
        dialogueText.text = "";

        string sentence = sentences[index];

        foreach (char letter in sentence)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;

        // 自动播放下一句
        if (autoPlay)
        {
            autoPlayCoroutine =
                StartCoroutine(AutoPlayNext());
        }
    }

    // 自动播放等待
    IEnumerator AutoPlayNext()
    {
        yield return new WaitForSeconds(autoPlayDelay);

        autoPlayCoroutine = null;

        if (!isFinished)
        {
            NextDialogue();
        }
    }

    // 下一句（保留 public，供 Button 调用）
    public void NextDialogue()
    {
        if (isFinished)
            return;

        // 如果正在打字，先显示完整句子
        if (isTyping)
        {
            if (typingCoroutine != null)
                StopCoroutine(typingCoroutine);

            dialogueText.text = sentences[index];
            isTyping = false;
            typingCoroutine = null;

            // 完整显示后重新开始等待
            if (autoPlay && autoPlayCoroutine == null)
            {
                autoPlayCoroutine =
                    StartCoroutine(AutoPlayNext());
            }

            return;
        }

        // 停止自动等待
        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
            autoPlayCoroutine = null;
        }

        // 进入下一句
        index++;

        if (index < sentences.Length)
        {
            ShowDialogue();
        }
        else
        {
            EndDialogue();
        }
    }

    // 停止所有对话协程
    void StopDialogueCoroutines()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
            autoPlayCoroutine = null;
        }

        isTyping = false;
    }

    // 对话结束
    void EndDialogue()
    {
        StopDialogueCoroutines();

        isFinished = true;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        Debug.Log("对话播放结束！");
    }
}
