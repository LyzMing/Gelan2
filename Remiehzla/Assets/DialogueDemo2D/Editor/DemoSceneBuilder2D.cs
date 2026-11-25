using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DialogueDemo.EditorTools
{
    /// <summary>
    /// 一键生成 2D 俯视角对话演示场景：
    /// 羽化的精灵图、房间、玩家、两个可交互物品、UI 与管理器全部由脚本搭建。
    /// 菜单：Tools → Dialogue Demo 2D → 生成 2D 演示场景
    /// 批处理：-executeMethod DialogueDemo.EditorTools.DemoSceneBuilder2D.Build
    /// </summary>
    public static class DemoSceneBuilder2D
    {
        public const string RootFolder = "Assets/DialogueDemo2D";
        public const string ScenePath = RootFolder + "/Scenes/DialogueDemo2D.unity";

        const string DialogueFolder = RootFolder + "/Dialogues";
        const string TextureFolder = RootFolder + "/Art";
        const string SceneFolder = RootFolder + "/Scenes";
        const string MaterialFolder = RootFolder + "/Materials";

        const float PixelsPerUnit = 128f;

        static Material cachedSpriteLit;
        static Material cachedSpriteUnlit;

        [MenuItem("Tools/Dialogue Demo 2D/生成 2D 演示场景", false, 10)]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "2D 对话演示",
                "场景已生成：\n" + ScenePath + "\n\n按 Play 试玩：WASD 移动，走近物品即开始对话。",
                "好");
        }

        public static void Build()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(DialogueFolder);
            EnsureFolder(TextureFolder);
            EnsureFolder(MaterialFolder);

            GenerateArt();

            DialogueData bookDialogue = CreateDialogue("BookDialogue2D", "尘封的书", true, new[]
            {
                new DialogueLine("尘封的书", "指尖碰到封面的瞬间，书页自己翻开了。"),
                new DialogueLine("尘封的书", "很久以前，这里住着一位抄写员。他把每个路过者的梦，都一字一句记了下来。"),
                new DialogueLine("旅人", "那我的梦……也会被写进去吗？", 28f),
                new DialogueLine("尘封的书", "翻到最后一页吧。空白的那一行，正等着你的名字。", 24f),
            });

            DialogueData chestDialogue = CreateDialogue("ChestDialogue2D", "风化的木箱", true, new[]
            {
                new DialogueLine("风化的木箱", "咔哒。锁扣在你掌心下松开，像一声很轻的叹息。"),
                new DialogueLine("风化的木箱", "里面没有金币，只有一张折好的纸条——「把没说完的话，说完。」"),
                new DialogueLine("旅人", "……这可不像宝藏。", 30f),
                new DialogueLine("风化的木箱", "木箱微微震动，仿佛在笑。", 20f),
            });

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildCamera();
            BuildRoom();
            GameObject player = BuildPlayer();
            BuildItems(bookDialogue, chestDialogue);
            BuildUI(out DialogueUI dialogueUI);

            var managerObject = new GameObject("DialogueSystem");
            var manager = managerObject.AddComponent<DialogueManager>();
            manager.Configure(dialogueUI);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log("[DialogueDemo2D] 2D 演示场景已生成：" + ScenePath);
        }

        // ------------------------------------------------------------------
        // 场景
        // ------------------------------------------------------------------

        static void BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;

            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Base;
        }

        static void BuildLighting()
        {
            var lightObject = new GameObject("Global Light 2D");
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.05f;
        }

        static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            Sprite square = LoadSprite("Square");

            CreateSprite("Background", LoadSprite("Background"), -100, Color.white,
                Vector3.zero, new Vector3(13f, 8f, 1f), room, GetSpriteUnlitMaterial());

            var ground = CreateSprite("Ground", LoadSprite("GroundTile"), -90, Color.white,
                Vector3.zero, Vector3.one, room);
            var groundRenderer = ground.GetComponent<SpriteRenderer>();
            groundRenderer.drawMode = SpriteDrawMode.Tiled;
            groundRenderer.size = new Vector2(20f, 11f);

            // 地毯
            CreateSprite("RugBorder", square, -87, new Color(0.2f, 0.12f, 0.14f, 0.9f),
                new Vector3(0f, -1.4f, 0f), new Vector3(7.4f, 3.4f, 1f), room);
            CreateSprite("Rug", square, -86, new Color(0.36f, 0.17f, 0.2f, 0.85f),
                new Vector3(0f, -1.4f, 0f), new Vector3(7f, 3f, 1f), room);

            // 墙
            CreateWall(room, "WallTop", new Vector2(0f, 5.7f), new Vector2(20.8f, 0.6f));
            CreateWall(room, "WallBottom", new Vector2(0f, -5.7f), new Vector2(20.8f, 0.6f));
            CreateWall(room, "WallLeft", new Vector2(-10.2f, 0f), new Vector2(0.6f, 12f));
            CreateWall(room, "WallRight", new Vector2(10.2f, 0f), new Vector2(0.6f, 12f));

            // 书架
            CreateSprite("Shelf", square, -42, new Color(0.22f, 0.17f, 0.24f),
                new Vector3(-3.2f, 3.8f, 0f), new Vector3(2.8f, 1.8f, 1f), room);
            CreateSprite("ShelfLine1", square, -41, new Color(0.34f, 0.27f, 0.36f),
                new Vector3(-3.2f, 3.45f, 0f), new Vector3(2.4f, 0.1f, 1f), room);
            CreateSprite("ShelfLine2", square, -41, new Color(0.34f, 0.27f, 0.36f),
                new Vector3(-3.2f, 3.95f, 0f), new Vector3(2.4f, 0.1f, 1f), room);

            Color[] bookColors =
            {
                new Color(0.72f, 0.3f, 0.3f), new Color(0.3f, 0.55f, 0.7f),
                new Color(0.75f, 0.62f, 0.28f), new Color(0.4f, 0.6f, 0.4f),
                new Color(0.55f, 0.4f, 0.65f), new Color(0.65f, 0.5f, 0.35f),
            };
            for (int i = 0; i < bookColors.Length; i++)
            {
                float x = -4.25f + i * 0.42f;
                float height = 0.5f + (i % 3) * 0.08f;
                CreateSprite("ShelfBook" + i, square, -40, bookColors[i],
                    new Vector3(x, 3.21f, 0f), new Vector3(0.22f, height, 1f), room);
            }

            // 木箱堆
            CreateSprite("CrateA", square, -42, new Color(0.42f, 0.3f, 0.18f),
                new Vector3(4.7f, 3.9f, 0f), new Vector3(1.5f, 1.2f, 1f), room);
            CreateSprite("CrateB", square, -41, new Color(0.5f, 0.36f, 0.22f),
                new Vector3(5.6f, 3.35f, 0f), new Vector3(0.9f, 0.8f, 1f), room);

            // 两处底座
            CreateSprite("PedestalBook", square, -30, new Color(0.24f, 0.26f, 0.32f),
                new Vector3(-3.2f, 0.55f, 0f), new Vector3(1.7f, 0.5f, 1f), room);
            CreateSprite("PedestalChest", square, -30, new Color(0.24f, 0.26f, 0.32f),
                new Vector3(3.2f, 0.35f, 0f), new Vector3(1.7f, 0.5f, 1f), room);
        }

        static void CreateWall(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var wall = CreateSprite(name, LoadSprite("Square"), -60, new Color(0.16f, 0.19f, 0.26f),
                center, new Vector3(size.x, size.y, 1f), parent);
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }

        static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, -3.4f, 0f);

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.drag = 0f;

            var collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.3f;
            collider.sharedMaterial = GetFrictionlessMaterial();

            var shadow = CreateSprite("Shadow", LoadSprite("Glow"), 19, new Color(0f, 0f, 0f, 0.35f),
                new Vector3(0f, -0.42f, 0f), new Vector3(1.1f, 0.55f, 1f), player.transform, GetSpriteUnlitMaterial());
            shadow.transform.localPosition = new Vector3(0f, -0.42f, 0f);

            var visual = CreateSprite("Visual", LoadSprite("Player"), 20, Color.white,
                Vector3.zero, Vector3.one, player.transform);
            visual.transform.localPosition = Vector3.zero;

            var controller = player.AddComponent<PlayerController2D>();
            controller.Configure(visual.transform);
            return player;
        }

        static void BuildItems(DialogueData bookDialogue, DialogueData chestDialogue)
        {
            var items = new GameObject("Items").transform;

            CreateItem(items, "Item_Book", "尘封的书", new Vector2(-3.2f, 1.35f),
                LoadSprite("Book"), bookDialogue);

            CreateItem(items, "Item_Chest", "风化的木箱", new Vector2(3.2f, 1.05f),
                LoadSprite("Chest"), chestDialogue);
        }

        static void CreateItem(Transform parent, string name, string displayName, Vector2 position,
            Sprite sprite, DialogueData dialogue)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, position.y, 0f);

            var trigger = root.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.95f;

            var glow = CreateSprite("Glow", LoadSprite("Glow"), 5, new Color(0.4f, 0.8f, 1f, 0.22f),
                Vector3.zero, new Vector3(2.5f, 2.5f, 1f), root.transform, GetSpriteUnlitMaterial());
            glow.transform.localPosition = new Vector3(0f, -0.05f, 0f);

            var visual = CreateSprite("Visual", sprite, 10, Color.white,
                Vector3.zero, Vector3.one, root.transform);
            visual.transform.localPosition = Vector3.zero;

            var item = root.AddComponent<InteractableItem2D>();
            item.itemName = displayName;
            item.dialogue = dialogue;
            item.oneShot = dialogue != null && dialogue.oneShot;
            item.visual = visual.transform;
            item.glow = glow.GetComponent<SpriteRenderer>();
        }

        static GameObject CreateSprite(string name, Sprite sprite, int sortingOrder, Color color,
            Vector3 position, Vector3 scale, Transform parent, Material material = null)
        {
            var gameObject = new GameObject(name);
            if (parent != null) gameObject.transform.SetParent(parent, false);

            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = material != null ? material : GetSpriteLitMaterial();
            return gameObject;
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------

        static void BuildUI(out DialogueUI dialogueUI)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<CjkTextBootstrap>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            CreateText("ControlsTip", canvasObject.transform, "WASD 移动　·　走近物品触发对话", 26,
                new Color(0.8f, 0.85f, 0.95f, 0.85f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -96f), new Vector2(900f, -50f));

            var panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(1040f, 260f);

            var group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var background = panel.GetComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.07f, 0.94f);
            background.raycastTarget = false;

            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            var accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.offsetMin = new Vector2(0f, -4f);
            accentRect.offsetMax = Vector2.zero;
            var accentImage = accent.GetComponent<Image>();
            accentImage.color = new Color(0.35f, 0.75f, 1f, 0.9f);
            accentImage.raycastTarget = false;

            Text speaker = CreateText("Speaker", panel.transform, "", 32,
                new Color(1f, 0.82f, 0.45f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -50f), new Vector2(600f, -12f));
            speaker.fontStyle = FontStyle.Bold;

            Text content = CreateText("Content", panel.transform, "", 30,
                new Color(0.93f, 0.95f, 0.98f), TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(40f, 56f), new Vector2(-40f, -70f));
            content.lineSpacing = 1.15f;

            Text hint = CreateText("Hint", panel.transform, "", 20,
                new Color(0.55f, 0.62f, 0.75f), TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-520f, 12f), new Vector2(-32f, 52f));

            dialogueUI = panel.AddComponent<DialogueUI>();
            dialogueUI.Configure(group, speaker, content, hint);
        }

        static Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // ------------------------------------------------------------------
        // 程序化美术
        // ------------------------------------------------------------------

        static void GenerateArt()
        {
            PaintTexture("Player", 128, 128, PaintPlayer, false);
            PaintTexture("Book", 96, 112, PaintBook, false);
            PaintTexture("Chest", 112, 96, PaintChest, false);
            PaintTexture("Glow", 128, 128, PaintGlow, false);
            PaintTexture("Square", 128, 128, (x, y) => Color.white, false);
            PaintTexture("GroundTile", 128, 128, PaintGroundTile, true);
            PaintTexture("Background", 256, 256, PaintBackground, false);
        }

        static void PaintTexture(string name, int width, int height, Func<float, float, Color> painter, bool repeat)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = painter(x + 0.5f, y + 0.5f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            string assetPath = TextureFolder + "/" + name + ".png";
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(assetPath, repeat);
        }

        static void ConfigureSpriteImporter(string assetPath, bool repeat)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer))
            {
                Debug.LogWarning("[DialogueDemo2D] 无法取得贴图导入器：" + assetPath);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string name)
        {
            string path = TextureFolder + "/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new UnityException("加载精灵失败：" + path);
            }
            return sprite;
        }

        static Material GetSpriteLitMaterial()
        {
            if (cachedSpriteLit == null)
            {
                cachedSpriteLit = GetOrCreateMaterial(
                    "SpriteLit", "Universal Render Pipeline/2D/Sprite-Lit-Default");
            }
            return cachedSpriteLit;
        }

        static Material GetSpriteUnlitMaterial()
        {
            if (cachedSpriteUnlit == null)
            {
                cachedSpriteUnlit = GetOrCreateMaterial("SpriteUnlit", "Sprites/Default");
            }
            return cachedSpriteUnlit;
        }

        static Material GetOrCreateMaterial(string name, string shaderName)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new UnityException("找不到着色器：" + shaderName);

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static float RoundedRectSdf(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - (halfWidth - radius);
            float qy = Mathf.Abs(py) - (halfHeight - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                       Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        static float CircleSdf(float dx, float dy, float radius)
        {
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        static float Coverage(float sdf)
        {
            return Mathf.Clamp01(0.5f - sdf);
        }

        static Color Over(Color below, Color above)
        {
            float alpha = above.a + below.a * (1f - above.a);
            if (alpha <= 0.0001f) return Color.clear;

            float r = (above.r * above.a + below.r * below.a * (1f - above.a)) / alpha;
            float g = (above.g * above.a + below.g * below.a * (1f - above.a)) / alpha;
            float b = (above.b * above.a + below.b * below.a * (1f - above.a)) / alpha;
            return new Color(r, g, b, alpha);
        }

        static Color PaintPlayer(float x, float y)
        {
            float px = x - 64f;
            float py = y - 64f;
            Color color = Color.clear;

            float body = RoundedRectSdf(px, py, 44f, 44f, 16f);
            color = Over(color, new Color(0.07f, 0.11f, 0.16f, Coverage(body)));
            if (body < -2f) color = Over(color, new Color(0.22f, 0.78f, 0.88f, 1f));

            float shine = RoundedRectSdf(px + 16f, py - 18f, 13f, 7f, 6f);
            if (shine < 0.5f) color = Over(color, new Color(1f, 1f, 1f, 0.1f * Coverage(shine)));

            float visor = RoundedRectSdf(px, py - 16f, 23f, 11f, 8f);
            if (visor < 0.5f) color = Over(color, new Color(0.04f, 0.13f, 0.18f, Coverage(visor)));

            float eye = CircleSdf(px - 8f, py - 18f, 3.2f);
            if (eye < 0.5f) color = Over(color, new Color(0.75f, 1f, 1f, Coverage(eye)));

            return color;
        }

        static Color PaintBook(float x, float y)
        {
            float px = x - 48f;
            float py = y - 56f;
            Color color = Color.clear;

            float cover = RoundedRectSdf(px, py, 40f, 48f, 7f);
            color = Over(color, new Color(0.05f, 0.08f, 0.14f, Coverage(cover)));
            if (cover < -2f) color = Over(color, new Color(0.16f, 0.38f, 0.7f, 1f));

            // 书页
            float pages = RoundedRectSdf(px - 35f, py, 4f, 41f, 2f);
            if (pages < 0.5f) color = Over(color, new Color(0.88f, 0.86f, 0.78f, Coverage(pages)));

            // 书脊
            float spine = RoundedRectSdf(px + 31f, py, 6f, 43f, 3f);
            if (spine < 0.5f) color = Over(color, new Color(0.08f, 0.2f, 0.42f, Coverage(spine)));

            float bandTop = RoundedRectSdf(px + 31f, py - 16f, 6f, 2f, 1f);
            float bandBottom = RoundedRectSdf(px + 31f, py + 16f, 6f, 2f, 1f);
            if (bandTop < 0.5f) color = Over(color, new Color(0.95f, 0.78f, 0.35f, Coverage(bandTop)));
            if (bandBottom < 0.5f) color = Over(color, new Color(0.95f, 0.78f, 0.35f, Coverage(bandBottom)));

            // 封面标题
            float title = RoundedRectSdf(px + 2f, py - 10f, 15f, 2.5f, 1f);
            if (title < 0.5f) color = Over(color, new Color(0.95f, 0.82f, 0.45f, Coverage(title)));

            float subtitle = RoundedRectSdf(px + 2f, py + 4f, 9f, 1.6f, 0.8f);
            if (subtitle < 0.5f) color = Over(color, new Color(0.95f, 0.82f, 0.45f, 0.75f * Coverage(subtitle)));

            return color;
        }

        static Color PaintChest(float x, float y)
        {
            float px = x - 56f;
            float py = y - 48f;
            Color color = Color.clear;

            float body = RoundedRectSdf(px, py, 48f, 32f, 7f);
            color = Over(color, new Color(0.08f, 0.05f, 0.03f, Coverage(body)));
            if (body < -2f) color = Over(color, new Color(0.72f, 0.46f, 0.2f, 1f));

            float lid = RoundedRectSdf(px, py - 22f, 48f, 10f, 6f);
            if (lid < 0.5f) color = Over(color, new Color(0.52f, 0.32f, 0.13f, Coverage(lid)));

            float trim = RoundedRectSdf(px, py - 10f, 44f, 2f, 1f);
            if (trim < 0.5f) color = Over(color, new Color(0.94f, 0.78f, 0.36f, Coverage(trim)));

            float strapLeft = RoundedRectSdf(px + 28f, py, 5f, 30f, 2f);
            float strapRight = RoundedRectSdf(px - 28f, py, 5f, 30f, 2f);
            if (strapLeft < 0.5f) color = Over(color, new Color(0.42f, 0.25f, 0.1f, Coverage(strapLeft)));
            if (strapRight < 0.5f) color = Over(color, new Color(0.42f, 0.25f, 0.1f, Coverage(strapRight)));

            float lockPlate = RoundedRectSdf(px, py + 2f, 9f, 11f, 3f);
            if (lockPlate < 0.5f) color = Over(color, new Color(0.95f, 0.8f, 0.4f, Coverage(lockPlate)));

            float keyhole = CircleSdf(px, py, 3.4f);
            if (keyhole < 0.5f) color = Over(color, new Color(0.2f, 0.13f, 0.06f, Coverage(keyhole)));

            return color;
        }

        static Color PaintGlow(float x, float y)
        {
            float dx = x - 64f;
            float dy = y - 64f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / 62f;
            float alpha = Mathf.Clamp01(1f - distance);
            alpha *= alpha;
            return new Color(1f, 1f, 1f, alpha);
        }

        static Color PaintGroundTile(float x, float y)
        {
            var color = new Color(0.105f, 0.125f, 0.17f);

            if (x % 64f < 2f || y % 64f < 2f)
            {
                color = new Color(0.135f, 0.16f, 0.215f);
            }

            if ((x * 7f + y * 13f) % 97f < 1f)
            {
                color = new Color(0.15f, 0.18f, 0.24f);
            }

            return color;
        }

        static Color PaintBackground(float x, float y)
        {
            float t = Mathf.Clamp01(y / 255f);
            var color = Color.Lerp(new Color(0.03f, 0.04f, 0.065f), new Color(0.09f, 0.11f, 0.16f), t);

            float dx = (x - 128f) / 128f;
            float dy = (y - 150f) / 160f;
            float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            color += new Color(0.05f, 0.07f, 0.1f) * (glow * glow);
            color.a = 1f;
            return color;
        }

        // ------------------------------------------------------------------
        // 资产
        // ------------------------------------------------------------------

        static DialogueData CreateDialogue(string fileName, string title, bool oneShot, DialogueLine[] lines)
        {
            string path = DialogueFolder + "/" + fileName + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.title = title;
            data.oneShot = oneShot;
            data.lines = new List<DialogueLine>(lines);
            EditorUtility.SetDirty(data);
            return data;
        }

        static PhysicsMaterial2D GetFrictionlessMaterial()
        {
            string path = MaterialFolder + "/PlayerSlide.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null)
            {
                material = new PhysicsMaterial2D("PlayerSlide") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static void AddSceneToBuildSettings()
        {
            // 只在缺失时追加，避免把工程接入别人的项目时清空已有 Build Settings
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
            {
                if (scene.path == ScenePath) return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
