using UnityEngine;
using UnityEngine.UI;

namespace Remiehzla.UI
{
    /// <summary>
    /// 给界面里没指定中文的 Text 换上系统字体。
    ///
    /// 场景里手摆的 UI 默认用 Unity 内置字体（没有中文字形，会显示方块），
    /// 运行时由这个组件换成系统中文字体。**已经指定了别的字体的 Text 不会被动**，
    /// 所以以后导入了正式中文字体，直接给 Text 指定，本组件会自动跳过。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class CjkFontBinder : MonoBehaviour
    {
        [Tooltip("关掉就不自动换字体")]
        public bool applyOnAwake = true;

        static Font builtinFont;
        static bool builtinSearched;

        void Awake()
        {
            if (applyOnAwake) Apply();
        }

        public void Apply()
        {
            Font font = UiFactory.Font;

            if (font == null) return;

            Text[] texts = GetComponentsInChildren<Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];

                if (text == null) continue;

                // 已经指定了自定义字体 → 尊重它，别乱换
                if (text.font != null && text.font != BuiltinFont()) continue;

                text.font = font;
            }
        }

        static Font BuiltinFont()
        {
            if (builtinSearched) return builtinFont;

            builtinSearched = true;

            try
            {
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                builtinFont = null;
            }

            return builtinFont;
        }
    }
}
