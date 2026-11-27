using UnityEngine;
using UnityEngine.SceneManagement;
using DialogueDemo;          // 队友的转场系统（唯一依赖他代码的地方）

namespace Remiehzla.Flow
{
    /// <summary>
    /// 场景切换的统一入口。所有"换个场景"都走这里，别直接调 SceneManager。
    ///
    /// 内部用队友的 <see cref="SceneTransitionManager"/>：淡出 → 异步加载 → 淡入。
    /// 它不可用、或者拒绝了请求时退回硬切，保证流程不会卡死。
    ///
    /// 他那个管理器自带 RuntimeInitializeOnLoadMethod 自动创建并 DontDestroyOnLoad，
    /// 所以这里不用管初始化——第一次真正切场景时它的"开场淡入"早就跑完了。
    /// </summary>
    public static class SceneFlow
    {
        /// <summary>
        /// 是否正在转场（淡出 / 加载 / 淡入 都算）。
        /// 自动触发的东西（区域对话、结局演出、交互提示）要等它变 false 才该生效，
        /// 否则会播在遮罩后面。
        /// </summary>
        public static bool IsTransitioning
        {
            get
            {
                SceneTransitionManager transition = SceneTransitionManager.Instance;

                return transition != null && transition.IsTransitioning;
            }
        }

        /// <summary>带淡入淡出地加载场景。</summary>
        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneFlow] 目标场景名为空，无法切换");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneFlow] 场景「{sceneName}」没有加入 Build Settings（File → Build Settings），无法加载");
                return;
            }

            SceneTransitionManager transition = SceneTransitionManager.Instance;

            // 转场系统不在（被删了/改名了）：直接硬切，别让流程断掉
            if (transition == null)
            {
                Debug.LogWarning($"[SceneFlow] 找不到 SceneTransitionManager，直接加载 {sceneName}");
                SceneManager.LoadScene(sceneName);
                return;
            }

            if (transition.IsTransitioning)
            {
                Debug.LogWarning("[SceneFlow] 正在转场，忽略本次切换请求");
                return;
            }

            if (!transition.LoadScene(sceneName))
            {
                Debug.LogWarning($"[SceneFlow] 转场系统拒绝了加载请求，直接硬切 {sceneName}");
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
