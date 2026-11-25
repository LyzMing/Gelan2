using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueDemo.EditorTools
{
    /// <summary>
    /// 2D 运行时冒烟测试：进入 Play Mode，把角色移动到物品旁，验证
    /// 「接触 → 对话展开 → 打字机 → 逐句推进 → 结束隐藏 → oneShot 不重复」。
    /// 批处理：-executeMethod DialogueDemo.EditorTools.DialogueRuntimeTest2D.RunBatch
    /// 成功时日志里会出现 "[DialogueDemo2D][RUNTIME] PASS"。
    /// </summary>
    public static class DialogueRuntimeTest2D
    {
        const double TimeoutSeconds = 45.0;
        const float TriggerDistance = 1.0f;

        static double startedAt;
        static double phaseStartedAt;
        static int phase;
        static bool finished;

        static DialogueManager manager;
        static InteractableItem2D book;
        static GameObject player;
        static Rigidbody2D playerBody;
        static CanvasGroup panel;
        static Text content;

        static int advanceCount;

        [MenuItem("Tools/Dialogue Demo 2D/运行 2D 冒烟测试", false, 30)]
        public static void RunFromMenu()
        {
            Prepare();
        }

        public static void RunBatch()
        {
            Prepare();
        }

        static void Prepare()
        {
            // 关闭域重载，让静态状态和 update 回调在进入 Play Mode 后仍然存在
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            EditorSceneManager.OpenScene(DemoSceneBuilder2D.ScenePath, OpenSceneMode.Single);

            startedAt = EditorApplication.timeSinceStartup;
            phase = 0;
            finished = false;
            advanceCount = 0;

            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
            Debug.Log("[DialogueDemo2D][RUNTIME] 进入 Play Mode，开始冒烟测试…");
        }

        static void Tick()
        {
            if (finished) return;

            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - startedAt;
            if (elapsed > TimeoutSeconds)
            {
                Fail("测试超时（" + elapsed.ToString("F1") + " 秒）");
                return;
            }

            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            switch (phase)
            {
                case 0:
                    if (elapsed < 1.0) return;
                    ResolveSceneObjects();
                    if (manager == null || book == null || player == null || playerBody == null ||
                        panel == null || content == null)
                    {
                        Fail("运行时缺少必要对象");
                        return;
                    }

                    Debug.Log("[DialogueDemo2D][RUNTIME] 场景对象就绪，把角色移动到「" + book.itemName + "」旁边");
                    Teleport(book.transform.position + new Vector3(0f, -TriggerDistance, 0f));
                    NextPhase(1);
                    break;

                case 1:
                    if (manager.IsDialogueActive)
                    {
                        Debug.Log("[DialogueDemo2D][RUNTIME] 接触成功触发对话");
                        NextPhase(2);
                    }
                    else if (now - phaseStartedAt > 3.0)
                    {
                        Fail("角色接触物品后 3 秒内没有开始对话");
                    }
                    break;

                case 2:
                    if (now - phaseStartedAt < 0.8) return;
                    if (panel.alpha < 0.6f)
                    {
                        Fail("对话框没有淡入，alpha=" + panel.alpha.ToString("F2"));
                        return;
                    }

                    string shown = content.text ?? "";
                    if (string.IsNullOrEmpty(shown))
                    {
                        Fail("打字机没有输出任何文字");
                        return;
                    }

                    string firstLine = book.dialogue.lines[0].text;
                    Debug.Log("[DialogueDemo2D][RUNTIME] 打字机已输出 " + shown.Length + " 个字：「" + shown + "」");

                    if (shown != firstLine) manager.Advance();
                    else Debug.Log("[DialogueDemo2D][RUNTIME] 第一句已自然打完");

                    NextPhase(3);
                    break;

                case 3:
                    if (now - phaseStartedAt < 0.3) return;
                    string expectedFirstLine = book.dialogue.lines[0].text;
                    if (content.text != expectedFirstLine)
                    {
                        Fail("跳过打字后没有显示完整的第一句");
                        return;
                    }

                    Debug.Log("[DialogueDemo2D][RUNTIME] 第一句完整显示，开始逐句推进");
                    NextPhase(4);
                    break;

                case 4:
                    if (now - phaseStartedAt < 0.25) return;
                    advanceCount++;
                    if (advanceCount > 20)
                    {
                        Fail("推进超过 20 次对话仍未结束");
                        return;
                    }

                    manager.Advance();
                    NextPhase(5);
                    break;

                case 5:
                    if (now - phaseStartedAt < 0.3) return;
                    if (manager.IsDialogueActive)
                    {
                        NextPhase(4);
                        return;
                    }

                    Debug.Log("[DialogueDemo2D][RUNTIME] 对话结束，共推进 " + advanceCount + " 次");
                    NextPhase(6);
                    break;

                case 6:
                    if (now - phaseStartedAt < 0.6) return;
                    if (panel.alpha > 0.05f)
                    {
                        Fail("对话结束后对话框没有隐藏，alpha=" + panel.alpha.ToString("F2"));
                        return;
                    }

                    Debug.Log("[DialogueDemo2D][RUNTIME] 对话框已隐藏");
                    Teleport(book.transform.position + new Vector3(5f, -5f, 0f));
                    NextPhase(7);
                    break;

                case 7:
                    if (now - phaseStartedAt < 1.0) return;
                    Teleport(book.transform.position + new Vector3(0f, -TriggerDistance, 0f));
                    NextPhase(8);
                    break;

                case 8:
                    if (now - phaseStartedAt < 1.0) return;
                    if (manager.IsDialogueActive)
                    {
                        Fail("oneShot 物品在离开后重新靠近时又被触发");
                        return;
                    }

                    if (!book.HasBeenTriggered)
                    {
                        Fail("物品没有记录「已触发」状态");
                        return;
                    }

                    Pass();
                    break;
            }
        }

        static void ResolveSceneObjects()
        {
            manager = UnityEngine.Object.FindObjectOfType<DialogueManager>();
            player = GameObject.FindGameObjectWithTag("Player");
            playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;

            var bookObject = GameObject.Find("Item_Book");
            book = bookObject != null ? bookObject.GetComponent<InteractableItem2D>() : null;

            var panelObject = GameObject.Find("Canvas/DialoguePanel");
            panel = panelObject != null ? panelObject.GetComponent<CanvasGroup>() : null;

            var contentObject = GameObject.Find("Canvas/DialoguePanel/Content");
            content = contentObject != null ? contentObject.GetComponent<Text>() : null;
        }

        static void Teleport(Vector3 position)
        {
            playerBody.position = position;
            Physics2D.SyncTransforms();
        }

        static void NextPhase(int next)
        {
            phase = next;
            phaseStartedAt = EditorApplication.timeSinceStartup;
        }

        static void Pass()
        {
            Debug.Log("[DialogueDemo2D][RUNTIME] PASS");
            Finish(0);
        }

        static void Fail(string reason)
        {
            Debug.LogError("[DialogueDemo2D][RUNTIME] FAIL：" + reason);
            Finish(1);
        }

        static void Finish(int exitCode)
        {
            if (finished) return;
            finished = true;

            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.Log("[DialogueDemo2D][RUNTIME] 测试结束，exitCode=" + exitCode);

            // 批处理模式才退出编辑器；在编辑器菜单里运行只停止 Play Mode
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
