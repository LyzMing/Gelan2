using UnityEngine;
using Remiehzla.Flow;

namespace Remiehzla.Explore
{
    /// <summary>
    /// 按拼图阶段的解法决定自己出现/隐藏 —— 第 4 关那种"隐藏门只在复制妻子并翻转后出现"就靠它。
    ///
    /// 挂在任何物件上都会在 Start 时读一次 LevelDirector.Context.puzzle.solutionId。
    /// 场景里没有 LevelDirector（比如单独的调试场景）时保持原状不动。
    /// </summary>
    public class ExploreVariant : MonoBehaviour
    {
        [Header("显示条件")]
        [Tooltip("只在这些 solutionId 下显示。留空 = 不限制")]
        public string[] showForSolutions;

        [Tooltip("在这些 solutionId 下隐藏（优先级高于 showForSolutions）")]
        public string[] hideForSolutions;

        [Tooltip("隐藏时直接销毁而不是仅禁用")]
        public bool destroyWhenHidden;

        void Start()
        {
            Apply();
        }

        public void Apply(string solutionIdOverride = null)
        {
            string solutionId = solutionIdOverride;

            if (solutionId == null)
            {
                LevelDirector director = FindObjectOfType<LevelDirector>();

                if (director != null && director.Context != null)
                    solutionId = director.Context.puzzle.solutionId;
            }

            // 没有上下文就别乱动，保持场景里的摆放
            if (string.IsNullOrEmpty(solutionId)) return;

            bool visible = true;

            if (hideForSolutions != null && Contains(hideForSolutions, solutionId))
                visible = false;

            if (visible && showForSolutions != null && showForSolutions.Length > 0)
                visible = Contains(showForSolutions, solutionId);

            if (visible)
            {
                gameObject.SetActive(true);
                return;
            }

            if (destroyWhenHidden) Destroy(gameObject);
            else gameObject.SetActive(false);
        }

        static bool Contains(string[] ids, string value)
        {
            if (ids == null) return false;

            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == value) return true;
            }

            return false;
        }
    }
}
