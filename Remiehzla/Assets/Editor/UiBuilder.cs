using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Remiehzla.UI;

namespace Remiehzla.EditorTools
{
    /// <summary>
    /// 把开始 / 结束界面生成成**场景里的真实物体**，之后随便调。
    ///
    /// 生成出来的都是普通 Canvas / Image / Text / Button，
    /// 脚本只是帮你摆第一版，组件引用会自动填好，也可以手动改。
    /// </summary>
    public static class UiBuilder
    {
        const string MenuRoot = "工具/界面/";

        static Font EditorFont
        {
            get
            {
                Font font = null;

                try
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch
                {
                    // 老版本 Unity 用 Arial.ttf
                }

                if (font == null)
                {
                    try
                    {
                        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                    catch
                    {
                        font = null;
                    }
                }

                return font;
            }
        }

        // ---------------------------------------------------------------- 开始界面

        [MenuItem(MenuRoot + "生成开始界面", false, 10)]
        public static void BuildStartMenu()
        {
            if (WarnIfExists("StartMenuUI")) return;

            GameObject root = CreateCanvasRoot("StartMenuUI", 500);

            var menu = root.AddComponent<StartMenu>();
            root.AddComponent<CjkFontBinder>();

            GameObject panel = CreateFullScreen(root.transform, "Panel", new Color(0.07f, 0.08f, 0.11f, 1f));

            Text title = CreateText(panel.transform, "Title", "拾忆拼图", 72,
                new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1500f, 140f));

            Text subtitle = CreateText(panel.transform, "Subtitle", "把记忆拼回来", 28,
                new Color(0.72f, 0.74f, 0.80f, 1f), TextAnchor.MiddleCenter);
            Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1500f, 60f));

            Button start = CreateButton(panel.transform, "StartButton", "开始游戏", 32,
                new Vector2(0f, -110f), new Vector2(420f, 96f),
                new Color(0.94f, 0.76f, 0.36f, 1f), new Color(0.12f, 0.12f, 0.14f, 1f));

            Text hint = CreateText(panel.transform, "Hint", "方向键移动　E 交互　K / J 旋转翻转", 20,
                new Color(0.50f, 0.52f, 0.58f, 1f), TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1400f, 40f), new Vector2(0.5f, 0f));

            // 自动填引用
            menu.panel = panel;
            menu.titleText = title;
            menu.subtitleText = subtitle;
            menu.hintText = hint;
            menu.startButton = start;

            Finish(root, "生成开始界面");
        }

        // ---------------------------------------------------------------- 结束界面

        [MenuItem(MenuRoot + "生成结束界面", false, 11)]
        public static void BuildEndScreen()
        {
            if (WarnIfExists("EndScreenUI")) return;

            GameObject root = CreateCanvasRoot("EndScreenUI", 600);

            var screen = root.AddComponent<EndScreen>();
            root.AddComponent<CjkFontBinder>();

            GameObject panel = CreateFullScreen(root.transform, "Panel", new Color(0.05f, 0.06f, 0.08f, 1f));

            Text title = CreateText(panel.transform, "Title", "结束", 64,
                new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1500f, 130f));

            Text message = CreateText(panel.transform, "Message", "（这里放结局文字）", 26,
                new Color(0.74f, 0.76f, 0.82f, 1f), TextAnchor.UpperCenter);
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1300f, 200f));

            Button back = CreateButton(panel.transform, "BackButton", "回到开始", 30,
                new Vector2(0f, -160f), new Vector2(400f, 90f),
                new Color(0.94f, 0.76f, 0.36f, 1f), new Color(0.12f, 0.12f, 0.14f, 1f));

            Text footer = CreateText(panel.transform, "Footer", "感谢游玩", 18,
                new Color(0.45f, 0.47f, 0.53f, 1f), TextAnchor.MiddleCenter);
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 40f), new Vector2(0.5f, 0f));

            screen.panel = panel;
            screen.titleText = title;
            screen.messageText = message;
            screen.backButton = back;
            screen.footerText = footer;

            // 结束界面平时隐藏，Show() 才显示
            panel.SetActive(false);

            Finish(root, "生成结束界面");
        }

        // ---------------------------------------------------------------- 公共

        static bool WarnIfExists(string rootName)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != rootName) continue;

                EditorUtility.DisplayDialog("已经有一个了",
                    $"当前场景里已经有 {rootName}。\n先删掉它再生成，或者直接改现有的那个。", "好");

                Selection.activeGameObject = go;
                return true;
            }

            return false;
        }

        static GameObject CreateCanvasRoot(string name, int sortingOrder)
        {
            var root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            return root;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "生成 EventSystem");

            Debug.Log("[界面] 场景里没有 EventSystem，已自动创建一个（按钮需要它才能点）");
        }

        static GameObject CreateFullScreen(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = color;

            return go;
        }

        static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = EditorFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;

            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, int fontSize,
            Vector2 position, Vector2 size, Color background, Color labelColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = background;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(go.transform, "Label", label, fontSize, labelColor, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>按锚点摆放（anchor 指锚点位置，默认屏幕中心）。</summary>
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Finish(GameObject root, string undoName)
        {
            // 整棵树建完之后再登记 Undo，这样撤销能一次把这套 UI 全撤掉
            Undo.RegisterCreatedObjectUndo(root, undoName);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            Debug.Log($"[界面] {undoName}：已生成 {root.name}。" +
                      "现在可以在 Scene 视图里直接拖动位置、在 Inspector 里改颜色字号，" +
                      "也可以把 Image 换成美术给的贴图。记得保存场景（Ctrl+S）。");
        }
    }
}
