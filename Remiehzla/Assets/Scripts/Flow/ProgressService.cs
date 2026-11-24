using System.IO;
using UnityEngine;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 进度记录入口。所有"拿到了什么、通关了哪一关"都走这里，别到处直接改 ProgressData。
    ///
    /// ⚠️ 现在**默认不落盘**：每次 Play 都是干净的，拾取物会重新出现。
    ///    仍然需要在内存里记（否则同一件东西能反复捡、隐藏结局没法统计），
    ///    只是不写进磁盘。以后要做真存档，把 <see cref="PersistToDisk"/> 设成 true 即可。
    ///
    /// 开启落盘后文件位置：Application.persistentDataPath/progress.json
    /// 写入先落临时文件再替换，避免中途退出把存档写坏。
    /// </summary>
    public static class ProgressService
    {
        /// <summary>
        /// 是否把进度写到磁盘。默认 false（每次 Play 都从零开始）。
        /// 以后要做真存档，把这里直接改成 `= true` —— 比在运行时赋值可靠
        /// （运行时赋值要赶在第一次访问 Data 之前，容易踩时序）。
        /// </summary>
        public static bool PersistToDisk = false;

        static ProgressData data;
        static bool loaded;

        static string FilePath => Path.Combine(Application.persistentDataPath, "progress.json");

        public static ProgressData Data
        {
            get
            {
                EnsureLoaded();
                return data;
            }
        }

        /// <summary>不落盘时，每次进 Play 都清空内存里的进度。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForNewSession()
        {
            data = null;
            loaded = false;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;

            loaded = true;

            // 不落盘：直接用一份空的内存数据，不碰磁盘
            if (!PersistToDisk)
            {
                data = new ProgressData();
                return;
            }

            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    data = JsonUtility.FromJson<ProgressData>(json);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProgressService] 读档失败，从空存档开始：{e.Message}");
            }

            if (data == null) data = new ProgressData();
            if (data.clearedLevels == null) data.clearedLevels = new System.Collections.Generic.List<string>();
            if (data.collected == null) data.collected = new System.Collections.Generic.List<string>();
            if (data.hiddenSolutions == null) data.hiddenSolutions = new System.Collections.Generic.List<string>();
        }

        // ------------------------------------------------------------------ 查询

        public static bool HasCollected(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && Data.collected.Contains(itemId);
        }

        public static bool HasCleared(string levelId)
        {
            return !string.IsNullOrEmpty(levelId) && Data.clearedLevels.Contains(levelId);
        }

        public static bool HasHiddenSolution(string solutionId)
        {
            return !string.IsNullOrEmpty(solutionId) && Data.hiddenSolutions.Contains(solutionId);
        }

        // ------------------------------------------------------------------ 写入

        /// <summary>记录收集品。返回 true 表示这次是新拿到的。</summary>
        public static bool Collect(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (Data.collected.Contains(itemId)) return false;

            Data.collected.Add(itemId);
            Save();

            return true;
        }

        public static void MarkCleared(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return;
            if (Data.clearedLevels.Contains(levelId)) return;

            Data.clearedLevels.Add(levelId);
            Save();
        }

        public static void MarkHiddenSolution(string solutionId)
        {
            if (string.IsNullOrEmpty(solutionId)) return;
            if (Data.hiddenSolutions.Contains(solutionId)) return;

            Data.hiddenSolutions.Add(solutionId);
            Save();
        }

        public static void SetCurrentLevel(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || Data.currentLevelId == levelId) return;

            Data.currentLevelId = levelId;
            Save();
        }

        // ------------------------------------------------------------------ 存 / 读 / 清

        public static void Save()
        {
            // 不落盘：数据只留在内存里
            if (!PersistToDisk) return;

            EnsureLoaded();

            try
            {
                string json = JsonUtility.ToJson(data, true);
                string path = FilePath;
                string temp = path + ".tmp";

                File.WriteAllText(temp, json);

                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProgressService] 存档失败：{e.Message}");
            }
        }

        /// <summary>清空存档（编辑器菜单和调试用）。</summary>
        public static void ResetAll()
        {
            data = new ProgressData();
            loaded = true;

            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProgressService] 删除存档失败：{e.Message}");
            }
        }
    }
}
