using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Remiehzla.Explore;
using Remiehzla.Flow;
using Remiehzla.Puzzle;

namespace Remiehzla.EditorTools
{
    /// <summary>
    /// 探索场景的检查工具。跟拼图的"校验关卡定义"配套用。
    /// 菜单：工具 → 校验探索场景
    ///
    /// 会检查：可交互物 id 是否为空/重复、有没有门、门有没有配目标场景、
    /// 以及（如果能找到关卡资产）LevelDefinition 里写的收集品和场景里的拾取物是否对得上。
    /// </summary>
    public static class ExploreSceneValidator
    {
        [MenuItem("工具/校验探索场景")]
        public static void ValidateOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("探索场景校验", "当前没有打开的场景。", "好");
                return;
            }

            var interactables = new List<InteractableBase>();

            foreach (GameObject root in scene.GetRootGameObjects())
                interactables.AddRange(root.GetComponentsInChildren<InteractableBase>(true));

            var errors = new List<string>();
            var warnings = new List<string>();

            var itemIds = new Dictionary<string, PickupInteractable>();
            var pickupIds = new List<string>();
            int doorCount = 0;
            int doorWithoutTarget = 0;

            LevelDirector director = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                director = root.GetComponentInChildren<LevelDirector>(true);
                if (director != null) break;
            }

            foreach (InteractableBase interactable in interactables)
            {
                if (interactable == null) continue;

                if (interactable is PickupInteractable pickup)
                {
                    if (string.IsNullOrWhiteSpace(pickup.itemId))
                    {
                        errors.Add($"{Path(pickup)} 的 itemId 为空");
                    }
                    else if (itemIds.ContainsKey(pickup.itemId))
                    {
                        errors.Add($"itemId \"{pickup.itemId}\" 重复：{Path(itemIds[pickup.itemId])} 和 {Path(pickup)}");
                    }
                    else
                    {
                        itemIds.Add(pickup.itemId, pickup);
                        pickupIds.Add(pickup.itemId);
                    }
                }
                else if (interactable is DoorInteractable door)
                {
                    doorCount++;

                    if (string.IsNullOrWhiteSpace(door.targetSceneName))
                    {
                        doorWithoutTarget++;
                        warnings.Add($"{Path(door)} 没有填 targetSceneName（转场系统不知道去哪）");
                    }
                }
                else if (interactable is TriggerInteractable trigger && string.IsNullOrWhiteSpace(trigger.interactableId))
                {
                    warnings.Add($"{Path(trigger)} 的 interactableId 为空，事件订阅方收不到");
                }
            }

            if (doorCount == 0) warnings.Add("场景里没有任何 DoorInteractable，玩家无法离开这一关");

            if (doorCount > 0 && director == null)
                warnings.Add("场景里有门但没有 LevelDirector，门点了不会过关");

            // ---- 和关卡资产对账 ----
            LevelDefinition level = null;

            if (director != null) level = director.level;
            if (level == null) level = Selection.activeObject as LevelDefinition;

            if (level != null && level.collectibles != null)
            {
                foreach (string id in level.collectibles)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    if (!itemIds.ContainsKey(id))
                        errors.Add($"关卡资产 {level.levelId} 里写了收集品 \"{id}\"，但场景里找不到对应的 PickupInteractable");
                }

                foreach (string id in pickupIds)
                {
                    if (System.Array.IndexOf(level.collectibles, id) < 0)
                        warnings.Add($"场景里的收集品 \"{id}\" 没有写进关卡资产 {level.levelId} 的 collectibles");
                }
            }
            else
            {
                warnings.Add("没有找到关卡资产（先选中一个 LevelDefinition，或让场景里有 LevelDirector），跳过收集品对账");
            }

            // ---- 输出 ----
            foreach (string error in errors) Debug.LogError($"[探索校验] {scene.name}：{error}");
            foreach (string warning in warnings) Debug.LogWarning($"[探索校验] {scene.name}：{warning}");

            if (errors.Count == 0 && warnings.Count == 0)
                Debug.Log($"[探索校验] {scene.name} 通过 ✔（{interactables.Count} 个可交互物）");

            EditorUtility.DisplayDialog("探索场景校验",
                $"{scene.name}\n\n可交互物：{interactables.Count}　门：{doorCount}\n" +
                $"错误：{errors.Count}　警告：{warnings.Count}\n\n详见 Console。", "好");
        }

        [MenuItem("工具/清除进度存档")]
        public static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("清除进度存档",
                    "会删掉 persistentDataPath 下的 progress.json（收集品、通关记录全部清空）。确定？", "清除", "取消"))
                return;

            ProgressService.ResetAll();

            Debug.Log("[存档] 已清空进度存档");
        }

        static string Path(Component component)
        {
            if (component == null) return "(空)";

            var path = component.name;
            Transform parent = component.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
