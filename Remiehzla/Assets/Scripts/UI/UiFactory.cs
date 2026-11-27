using System;
using UnityEngine;
using UnityEngine.UI;

namespace Remiehzla.UI
{
    /// <summary>
    /// 建 UI 的小工具：字体、文字、按钮。只给运行时自建界面的脚本用。
    /// </summary>
    public static class UiFactory
    {
        static Font cachedFont;

        public static Font Font
        {
            get
            {
                if (cachedFont != null) return cachedFont;

                try
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Microsoft YaHei", "微软雅黑", "SimHei", "黑体",
                        "Noto Sans CJK SC", "PingFang SC", "Arial Unicode MS"
                    }, 32);
                }
                catch (Exception)
                {
                    cachedFont = null;
                }

                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                return cachedFont;
            }
        }

        /// <summary>建一个铺满屏幕的 Overlay Canvas。</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static Text CreateText(Transform parent, string name, int fontSize, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;

            return text;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static Button CreateButton(Transform parent, string name, string label, int fontSize,
            Vector2 position, Vector2 size, Color background, Color labelColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            var image = go.GetComponent<Image>();
            image.color = background;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(go.transform, "Label", fontSize, labelColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.text = label;

            return button;
        }

        /// <summary>整块铺满的纯色遮罩（挡点击）。</summary>
        public static Image CreateFullScreen(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            Stretch(rect);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            return image;
        }
    }
}
