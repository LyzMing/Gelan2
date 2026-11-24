using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Remiehzla.Puzzle;

namespace Remiehzla.EditorTools
{
    /// <summary>
    /// 一键校验项目里所有 LevelDefinition，错误和警告打到 Console 并弹汇总。
    /// 菜单：工具 → 校验关卡定义
    /// </summary>
    public static class LevelConfigValidator
    {
        [MenuItem("工具/校验关卡定义")]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDefinition");

            if (guids.Length == 0)
            {
                Debug.LogWarning("[关卡校验] 项目里还没有任何 LevelDefinition。");
                EditorUtility.DisplayDialog("关卡校验", "项目里还没有任何 LevelDefinition。", "好");
                return;
            }

            int errorCount = 0;
            int warningCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);

                if (level == null) continue;

                var errors = new List<string>();
                var warnings = new List<string>();

                ValidateLevel(level, errors, warnings);

                for (int e = 0; e < errors.Count; e++)
                {
                    Debug.LogError($"[关卡校验] {path}：{errors[e]}", level);
                    errorCount++;
                }

                for (int w = 0; w < warnings.Count; w++)
                {
                    Debug.LogWarning($"[关卡校验] {path}：{warnings[w]}", level);
                    warningCount++;
                }
            }

            EditorUtility.DisplayDialog(
                "关卡校验",
                $"检查了 {guids.Length} 个关卡：{errorCount} 个错误、{warningCount} 个警告。\n详见 Console。",
                "好");
        }

        /// <summary>单关卡校验（也供其他工具/测试调用）。</summary>
        public static void ValidateLevel(LevelDefinition level, List<string> errors, List<string> warnings)
        {
            if (level == null)
            {
                errors.Add("LevelDefinition 为空");
                return;
            }

            if (string.IsNullOrWhiteSpace(level.levelId))
                errors.Add("levelId 为空（存档键会失效）");
            else if (level.levelId != level.levelId.Trim())
                warnings.Add($"levelId \"{level.levelId}\" 前后有空格");

            if (string.IsNullOrWhiteSpace(level.displayName))
                warnings.Add("displayName 为空");

            if (level.collectibles != null)
            {
                var seen = new HashSet<string>();

                for (int i = 0; i < level.collectibles.Length; i++)
                {
                    string id = level.collectibles[i];

                    if (string.IsNullOrWhiteSpace(id)) errors.Add($"collectibles[{i}] 是空字符串");
                    else if (!seen.Add(id)) errors.Add($"collectibles 里 \"{id}\" 重复");
                }
            }

            if (level.puzzle == null)
            {
                warnings.Add("没有拼图配置（如果是纯剧情关可以忽略）");
                return;
            }

            PuzzleRules.Validate(level.puzzle, errors, warnings);

            // 探索场景名：拼图关必须有
            if (string.IsNullOrWhiteSpace(level.exploreSceneName))
                warnings.Add("exploreSceneName 为空，进入探索阶段会找不到场景");
        }

        /// <summary>在 LevelDefinition 的 Inspector 右键菜单里单独校验当前关卡。</summary>
        [MenuItem("CONTEXT/LevelDefinition/校验本关卡")]
        static void ValidateSingle(MenuCommand command)
        {
            var level = command.context as LevelDefinition;
            if (level == null) return;

            var errors = new List<string>();
            var warnings = new List<string>();

            ValidateLevel(level, errors, warnings);

            for (int i = 0; i < errors.Count; i++) Debug.LogError($"[关卡校验] {level.levelId}：{errors[i]}", level);
            for (int i = 0; i < warnings.Count; i++) Debug.LogWarning($"[关卡校验] {level.levelId}：{warnings[i]}", level);

            if (errors.Count == 0 && warnings.Count == 0)
                Debug.Log($"[关卡校验] {level.levelId} 通过 ✔", level);

            EditorUtility.DisplayDialog("关卡校验",
                $"{level.levelId}：{errors.Count} 个错误、{warnings.Count} 个警告。", "好");
        }
    }
}
