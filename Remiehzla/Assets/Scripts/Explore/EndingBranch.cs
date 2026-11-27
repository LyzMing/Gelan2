using UnityEngine;
using Remiehzla.Flow;
using Remiehzla.Dialogue;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 结局分支：进场景时检查玩家收集到的物品，决定播哪一套演出。
    ///
    /// 推荐做法是**一个场景 + 两套演出**（共用美术、只切内容），
    /// 把两套内容各自打成父子树，分别拖进 Complete Group / Missing Group。
    ///
    /// 流程：
    ///   1. 进场景 → 检查 Required Items 是否全部收集
    ///   2. 只启用对应那一组物体
    ///   3. 自动播对应的字幕（播放期间锁玩家移动）
    ///   4. 播完 → 弹结束界面（可关）
    ///
    /// 如果两个结局其实是完全不同的地点，那就拆成两个场景，各自放一个普通的
    /// ExploreSceneController + DialogueZone 即可，不需要这个组件。
    /// </summary>
    [DisallowMultipleComponent]
    public class EndingBranch : MonoBehaviour
    {
        [Header("判定条件")]
        [Tooltip("这一串物品全部拿到 = 完整结局；缺任何一个 = 缺失结局")]
        public string[] requiredItems = new string[0];

        [Header("两套演出（只启用其中一套）")]
        [Tooltip("物品都拿齐时启用的物体，可以拖一整棵子树的根")]
        public GameObject completeGroup;

        [Tooltip("缺物品时启用的物体")]
        public GameObject missingGroup;

        [Header("字幕")]
        public DialogueAsset completeDialogue;
        public DialogueAsset missingDialogue;

        [Header("结束界面")]
        public bool showEndScreen = true;
        public string completeEndTitle = "记忆完整";
        public string missingEndTitle = "还差一点";
        [TextArea(2, 4)]
        public string completeEndMessage = "";
        [TextArea(2, 4)]
        public string missingEndMessage = "";

        [Header("时机")]
        [Tooltip("转场结束之后等多少秒再开始播演出（两套场景内容会立刻切换）")]
        public float startDelay = 0.5f;

        /// <summary>本次判定结果：true = 物品齐了。</summary>
        public bool IsComplete { get; private set; }

        void Start()
        {
            IsComplete = CheckComplete();

            // 内容立刻切好，让淡入的时候看到的就是对应那套演出
            ApplyGroups();

            StartCoroutine(PlayAfterLoad());
        }

        System.Collections.IEnumerator PlayAfterLoad()
        {
            // 等转场（淡出/加载/淡入）走完，别把演出播在遮罩后面
            while (SceneFlow.IsTransitioning) yield return null;

            float waited = 0f;

            while (waited < startDelay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            PlayDialogue();
        }

        bool CheckComplete()
        {
            if (requiredItems == null || requiredItems.Length == 0)
            {
                Debug.LogWarning("[EndingBranch] 没有配置 Required Items，按「完整结局」处理", this);
                return true;
            }

            for (int i = 0; i < requiredItems.Length; i++)
            {
                string id = requiredItems[i];

                if (string.IsNullOrEmpty(id)) continue;

                if (!ProgressService.HasCollected(id))
                {
                    Debug.Log($"[EndingBranch] 还缺：{ProgressService.DescribeMissing(requiredItems)} → 走缺失结局");

                    return false;
                }
            }

            Debug.Log("[EndingBranch] 物品已集齐 → 走完整结局");

            return true;
        }

        void ApplyGroups()
        {
            if (completeGroup != null) completeGroup.SetActive(IsComplete);
            if (missingGroup != null) missingGroup.SetActive(!IsComplete);
        }

        void PlayDialogue()
        {
            DialogueAsset dialogue = IsComplete ? completeDialogue : missingDialogue;

            if (dialogue == null)
            {
                // 没有字幕也要能收尾
                Invoke(nameof(ShowEnding), 0.6f);
                return;
            }

            SubtitlePlayer player = SubtitlePlayer.Instance;

            if (player == null)
            {
                Invoke(nameof(ShowEnding), 0.6f);
                return;
            }

            bool started = player.Play(dialogue, ShowEnding, lockMovement: true);

            if (!started) Invoke(nameof(ShowEnding), 0.6f);
        }

        void ShowEnding()
        {
            if (!showEndScreen) return;

            UI.EndScreen.Show(
                IsComplete ? completeEndTitle : missingEndTitle,
                IsComplete ? completeEndMessage : missingEndMessage);
        }
    }
}
