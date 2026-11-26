using UnityEngine;
using Remiehzla.Dialogue;

namespace Remiehzla.Puzzle
{
    /// <summary>
    /// 一个关卡的全部数据。策划只填这个资产，代码里不写死任何一关。
    ///
    /// 创建：右键 Create → Remiehzla → Level Definition
    /// 摆布局：菜单 工具 → 拼图布局编辑器
    /// 校验：菜单 工具 → 校验关卡定义
    /// </summary>
    [CreateAssetMenu(menuName = "Remiehzla/Level Definition", fileName = "L00_NewLevel")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("存档键 + 收集品 id 前缀，全项目唯一，例如 L01_HOME")]
        public string levelId = "L01_HOME";

        [Tooltip("显示名，例如 家")]
        public string displayName = "新关卡";

        [Header("拼图阶段")]
        [Tooltip("留空 = 纯剧情关（最终演出关），直接跳过拼图")]
        public PuzzleConfig puzzle;

        [Tooltip("进入拼图环节时自动播放的字幕。播完之前不能操作拼图。留空 = 没有开场对话")]
        public DialogueAsset puzzleDialogue;

        [Header("探索阶段")]
        [Tooltip("拼图通过后加载的探索场景名（必须加到 Build Settings）。留空 = 在本场景里切 ExploreRoot")]
        public string exploreSceneName = "";

        [Header("流程")]
        [Tooltip("下一关的关卡资产。探索场景里走到门时，加载它的拼图场景；留空 = 没有下一关")]
        public LevelDefinition nextLevel;

        [Header("收集品")]
        [Tooltip("本关可拾取物的 id，带关卡前缀，例如 L01_PHOTO")]
        public string[] collectibles = new string[0];

        // 对话字段等对方确认 DialogueAsset 类型后再加（M2）
    }

    /// <summary>
    /// 拼图阶段的配置，内联在 LevelDefinition 里，不单独出资产。
    /// </summary>
    [System.Serializable]
    public class PuzzleConfig
    {
        [Header("图片")]
        [Tooltip("正方形整图，边长建议取 格数 的整数倍")]
        public Texture2D image;

        [Range(2, 8)]
        [Tooltip("几乘几。正方形，格数逐关递增（2 / 3 / 4 …）")]
        public int gridSize = 3;

        [Header("初始状态")]
        [Tooltip("策划摆好的打乱状态；为空时运行时退回随机打乱")]
        public PuzzleLayout initial;

        [Header("正解（至少一个）")]
        [Tooltip("第一个非隐藏解视为常规通关；隐藏解用 isHidden 标记")]
        public PuzzleSolution[] solutions = new PuzzleSolution[0];

        [Header("允许的操作")]
        [Tooltip("逐关解锁：第 1 关只要 Select | SwapAdjacent")]
        public PuzzleOps allowedOps = PuzzleOps.Select | PuzzleOps.SwapAdjacent;

        [Header("步数（暂不做限制，只记录）")]
        [Tooltip("旋转/翻转是否计入步数")]
        public bool countRotateAsStep;

        [Tooltip("0 = 不限")]
        public int moveLimit;

        [Header("调试")]
        [Tooltip("游戏内是否允许按 R 重打乱。正式关卡建议关掉")]
        public bool allowShuffleInGame;
    }
}
