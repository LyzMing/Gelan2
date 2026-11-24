using System;
using System.Collections.Generic;

namespace Remiehzla.Flow
{
    /// <summary>
    /// 全局进度存档。没有背包，只有一堆 flag。
    /// 由 <see cref="ProgressService"/> 读写，别直接改这个对象。
    /// </summary>
    [Serializable]
    public class ProgressData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>当前所在关卡 id。</summary>
        public string currentLevelId = "L01_HOME";

        /// <summary>已通关的关卡 id。</summary>
        public List<string> clearedLevels = new List<string>();

        /// <summary>已收集的物品 id（"L01_PHOTO" 这种）。</summary>
        public List<string> collected = new List<string>();

        /// <summary>达成过的隐藏解 id（"L04_TWINS" 这种）。</summary>
        public List<string> hiddenSolutions = new List<string>();
    }
}
