using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueDemo
{
    /// <summary>
    /// UI 文本的中文字体来源：运行时从操作系统里挑选一个可用的中文字体。
    /// 这样不必把字体文件放进工程，也避免随字体分发带来的授权问题。
    /// </summary>
    public static class UIFontProvider
    {
        // 覆盖 Windows / macOS / Linux 常见的中文字体名
        static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "SimHei", "黑体",
            "SimSun", "宋体", "Noto Sans CJK SC", "Source Han Sans SC",
            "PingFang SC", "Hiragino Sans GB", "WenQuanYi Micro Hei", "Arial Unicode MS"
        };

        const int FontTextureSize = 64;

        static Font cachedFont;
        static bool searched;

        /// <summary>返回一个支持中文的动态字体；系统里找不到时返回 null。</summary>
        public static Font GetFont()
        {
            if (cachedFont != null) return cachedFont;
            if (searched) return null;
            searched = true;

            try
            {
                string[] installed = Font.GetOSInstalledFontNames() ?? Array.Empty<string>();
                var installedSet = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);

                foreach (string fontName in PreferredFonts)
                {
                    if (!installedSet.Contains(fontName)) continue;
                    cachedFont = Font.CreateDynamicFontFromOSFont(fontName, FontTextureSize);
                    if (cachedFont != null) break;
                }

                if (cachedFont == null)
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont(PreferredFonts, FontTextureSize);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DialogueDemo] 读取系统字体失败，将回退到默认字体：" + e.Message);
            }

            if (cachedFont == null)
            {
                Debug.LogWarning("[DialogueDemo] 没有找到可用的中文字体，UI 中文可能显示为方块。");
            }
            return cachedFont;
        }

        public static void Apply(Text text)
        {
            if (text == null) return;
            var font = GetFont();
            if (font != null) text.font = font;
        }

        public static void ApplyToChildren(GameObject root)
        {
            if (root == null) return;
            foreach (var text in root.GetComponentsInChildren<Text>(true)) Apply(text);
        }
    }
}
