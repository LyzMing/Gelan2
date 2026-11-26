using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DialogueDemo.EditorTools
{
    /// <summary>
    /// 场景切换冒烟测试：进入 Play Mode，把角色送入传送门，
    /// 验证「接触 → 淡出 → 异步加载 → 大厅 → 返回书房」整条链路。
    /// 批处理：-executeMethod DialogueDemo.EditorTools.SceneTransitionRuntimeTest2D.RunBatch
    /// </summary>
    public static class SceneTransitionRuntimeTest2D
    {
        const double TimeoutSeconds = 40.0;
        const double LoadTimeoutSeconds = 10.0;

        static double startedAt;
        static double phaseStartedAt;
        static int phase;
        static bool finished;

        static GameObject player;
        static Rigidbody2D playerBody;

        [MenuItem("Tools/Dialogue Demo 2D/运行场景切换测试", false, 31)]
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
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            EditorSceneManager.OpenScene(DemoSceneBuilder2D.ScenePath, OpenSceneMode.Single);

            startedAt = EditorApplication.timeSinceStartup;
            phase = 0;
            finished = false;

            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
            Debug.Log("[DialogueDemo2D][TRANSITION] 进入 Play Mode，开始场景切换测试…");
        }

        static void Tick()
        {
            if (finished) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - startedAt > TimeoutSeconds)
            {
                Fail("测试超时");
                return;
            }

            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            switch (phase)
            {
                case 0:
                    if (now - startedAt < 1.0) return;
                    if (SceneTransitionManager.Instance == null)
                    {
                        Fail("没有自动创建 SceneTransitionManager");
                        return;
                    }
                    if (SceneTransitionManager.Instance.IsTransitioning) return;

                    var portal = GameObject.Find("Portal_ToHall");
                    if (portal == null)
                    {
                        Fail("主场景里找不到 Portal_ToHall");
                        return;
                    }

                    if (!ResolvePlayer())
                    {
                        Fail("主场景里找不到玩家");
                        return;
                    }

                    Teleport(portal.transform.position + new Vector3(0f, 0.4f, 0f));
                    Debug.Log("[DialogueDemo2D][TRANSITION] 已把角色送入大厅传送门");
                    NextPhase(1);
                    break;

                case 1:
                    if (SceneManager.GetActiveScene().name == DemoSceneBuilder2D.HallSceneName)
                    {
                        Debug.Log("[DialogueDemo2D][TRANSITION] 场景已切换到大堂");
                        NextPhase(2);
                    }
                    else if (now - phaseStartedAt > LoadTimeoutSeconds)
                    {
                        Fail("10 秒内没有切换到大厅场景");
                    }
                    break;

                case 2:
                    if (SceneTransitionManager.Instance != null &&
                        SceneTransitionManager.Instance.IsTransitioning) return;

                    var backPortal = GameObject.Find("Portal_ToMain");
                    if (backPortal == null)
                    {
                        Fail("大厅里找不到 Portal_ToMain");
                        return;
                    }

                    if (!ResolvePlayer())
                    {
                        Fail("大厅里找不到玩家");
                        return;
                    }

                    Teleport(backPortal.transform.position + new Vector3(0f, 0.4f, 0f));
                    Debug.Log("[DialogueDemo2D][TRANSITION] 已把角色送入返回传送门");
                    NextPhase(3);
                    break;

                case 3:
                    if (SceneManager.GetActiveScene().name == DemoSceneBuilder2D.MainSceneName)
                    {
                        Debug.Log("[DialogueDemo2D][TRANSITION] 已返回书房");
                        NextPhase(4);
                    }
                    else if (now - phaseStartedAt > LoadTimeoutSeconds)
                    {
                        Fail("10 秒内没有返回主场景");
                    }
                    break;

                case 4:
                    if (SceneTransitionManager.Instance != null &&
                        SceneTransitionManager.Instance.IsTransitioning) return;

                    Pass();
                    break;
            }
        }

        static bool ResolvePlayer()
        {
            player = GameObject.FindGameObjectWithTag("Player");
            playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            return playerBody != null;
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
            Debug.Log("[DialogueDemo2D][TRANSITION] PASS");
            Finish(0);
        }

        static void Fail(string reason)
        {
            Debug.LogError("[DialogueDemo2D][TRANSITION] FAIL：" + reason);
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
            Debug.Log("[DialogueDemo2D][TRANSITION] 测试结束，exitCode=" + exitCode);

            // 批处理模式才退出编辑器；在编辑器菜单里运行只停止 Play Mode
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
