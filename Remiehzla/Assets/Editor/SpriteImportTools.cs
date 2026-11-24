using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Remiehzla.EditorTools
{
    /// <summary>
    /// 美术素材批量工具。菜单：工具 → 美术 → ...
    ///
    /// 为什么需要它：
    ///   美术导出的道具图都是**固定画布尺寸、物体周围一圈透明**，
    ///   Unity 默认 pivot 在画布中心 —— 那不是物体的"脚点"，
    ///   而 <see cref="Remiehzla.Explore.YSort"/> 是靠 transform.position（脚点）算遮挡的，
    ///   pivot 不对 → 遮挡会错。
    ///
    /// 用法：在 Project 窗口里选中一批贴图（或直接选文件夹），然后点菜单。
    /// 注意：会重新导入资源，不能 Ctrl+Z 撤销。
    /// </summary>
    public static class SpriteImportTools
    {
        /// <summary>像素画统一用的 PPU。255px 背景 → 10.2 世界单位高，36px 角色 → 1.44。</summary>
        const float PixelsPerUnit = 25f;

        /// <summary>低于这个 alpha 的像素视为全透明（忽略抗锯齿边缘）。</summary>
        const byte AlphaThreshold = 8;

        const string MenuRoot = "工具/美术/";

        // ---------------------------------------------------------------- 1. 导入设置

        [MenuItem(MenuRoot + "1. 设为像素画 Sprite 导入设置", false, 100)]
        public static void ApplyPixelArtSettings()
        {
            List<string> paths = CollectTexturePaths();

            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("像素画导入设置",
                    "先在 Project 窗口里选中贴图（或整个文件夹）。", "好");
                return;
            }

            if (!EditorUtility.DisplayDialog("像素画导入设置",
                    $"将对 {paths.Count} 张贴图应用：\n\n" +
                    $"· Texture Type = Sprite (2D and UI)\n" +
                    $"· Pixels Per Unit = {PixelsPerUnit}\n" +
                    "· Filter Mode = Point (no filter)\n" +
                    "· Compression = None\n" +
                    "· Mipmap 关闭\n\n" +
                    "（PPU 想改的话改 SpriteImportTools.cs 顶部的常量）",
                    "应用", "取消"))
                return;

            int changed = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("像素画导入设置", paths[i], i / (float)paths.Count);

                    var importer = AssetImporter.GetAtPath(paths[i]) as TextureImporter;

                    if (importer == null)
                    {
                        failed++;
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;

                    if (importer.spriteImportMode == SpriteImportMode.None)
                        importer.spriteImportMode = SpriteImportMode.Single;

                    importer.spritePixelsPerUnit = PixelsPerUnit;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;

                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[美术工具] 像素画导入设置：成功 {changed} 张，失败 {failed} 张");

            EditorUtility.DisplayDialog("像素画导入设置",
                $"完成：{changed} 张\n失败：{failed} 张\n\n接着可以跑「2. 把 Pivot 设到内容底部中点」。", "好");
        }

        // ---------------------------------------------------------------- 2. Pivot

        [MenuItem(MenuRoot + "2. 把 Pivot 设到内容底部中点", false, 101)]
        public static void SetPivotToContentBottomCenter()
        {
            List<string> paths = CollectTexturePaths();

            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("设置 Pivot",
                    "先在 Project 窗口里选中贴图（或整个文件夹）。", "好");
                return;
            }

            if (!EditorUtility.DisplayDialog("设置 Pivot",
                    $"将对 {paths.Count} 张贴图把 pivot 设为「透明内容包围盒的底部中点」。\n\n" +
                    "· 只处理 Sprite / Single 模式的贴图（切好的精灵表会跳过）\n" +
                    "· 只改 pivot，不动贴图像素\n" +
                    "· 会重新导入，不能 Ctrl+Z 撤销",
                    "开始", "取消"))
                return;

            int changed = 0;
            int skippedSheet = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("设置 Pivot", paths[i], i / (float)paths.Count);

                    var importer = AssetImporter.GetAtPath(paths[i]) as TextureImporter;

                    if (importer == null || importer.textureType != TextureImporterType.Sprite)
                    {
                        failed++;
                        continue;
                    }

                    // 精灵表（Multiple）的 pivot 是每个切片单独设的，这里不处理
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        skippedSheet++;
                        continue;
                    }

                    if (!TryGetContentBounds(paths[i], out RectInt bounds, out Vector2Int size))
                    {
                        failed++;
                        continue;
                    }

                    // pivot / alignment 要通过 TextureImporterSettings 设，
                    // 而且只有 alignment = Custom 时自定义 pivot 才生效
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);

                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(
                        (bounds.x + bounds.width * 0.5f) / size.x,
                        bounds.y / (float)size.y);

                    importer.SetTextureSettings(settings);

                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[美术工具] Pivot：成功 {changed} 张，跳过精灵表 {skippedSheet} 张，失败 {failed} 张");

            string message = $"完成：{changed} 张\n";

            if (skippedSheet > 0) message += $"跳过（精灵表/多切片）：{skippedSheet} 张\n";
            if (failed > 0) message += $"失败（不是 Sprite 或读不到图）：{failed} 张\n—— 先跑「1. 设为像素画 Sprite 导入设置」\n";

            EditorUtility.DisplayDialog("设置 Pivot", message, "好");
        }

        // ---------------------------------------------------------------- 3. 留白检查

        [MenuItem(MenuRoot + "3. 检查透明留白", false, 102)]
        public static void ReportPadding()
        {
            List<string> paths = CollectTexturePaths();

            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("检查透明留白",
                    "先在 Project 窗口里选中贴图（或整个文件夹）。", "好");
                return;
            }

            var padded = new List<string>();
            int checkedCount = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                if (!TryGetContentBounds(paths[i], out RectInt bounds, out Vector2Int size)) continue;

                checkedCount++;

                float cover = bounds.width * (float)bounds.height / (size.x * (float)size.y);

                // 内容只占画布四分之一以下 = 留白很严重，建议让美术裁紧再导
                if (cover < 0.25f)
                {
                    padded.Add($"{paths[i]}　内容 {bounds.width}×{bounds.height} / 画布 {size.x}×{size.y}　占比 {cover:P0}");
                }
            }

            if (padded.Count == 0)
            {
                Debug.Log($"[美术工具] 检查了 {checkedCount} 张，没有明显留白。");
                EditorUtility.DisplayDialog("检查透明留白", $"检查了 {checkedCount} 张，都很紧。", "好");
                return;
            }

            Debug.Log($"[美术工具] 检查了 {checkedCount} 张，其中 {padded.Count} 张留白较多（建议让美术裁到内容边界再导）：");

            foreach (string line in padded) Debug.LogWarning("  " + line);

            EditorUtility.DisplayDialog("检查透明留白",
                $"检查了 {checkedCount} 张，其中 {padded.Count} 张留白较多。\n\n" +
                "这些图用「2. 把 Pivot 设到内容底部中点」也能修好遮挡，\n" +
                "但如果美术能裁到内容边界再导出，后面所有环节都更省事。\n\n详见 Console。", "好");
        }

        // ---------------------------------------------------------------- 内部

        /// <summary>把当前选择展开成贴图路径列表（支持选中文件夹）。</summary>
        static List<string> CollectTexturePaths()
        {
            var paths = new List<string>();
            var folders = new List<string>();

            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    folders.Add(path);
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null && !paths.Contains(path))
                    paths.Add(path);
            }

            foreach (string folder in folders)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!paths.Contains(path)) paths.Add(path);
                }
            }

            paths.Sort();

            return paths;
        }

        /// <summary>
        /// 读贴图里"非透明内容"的包围盒。
        /// 直接读磁盘上的 PNG，绕开 Unity「贴图没开 Read/Write 就读不到像素」的限制。
        /// </summary>
        static bool TryGetContentBounds(string assetPath, out RectInt bounds, out Vector2Int size)
        {
            bounds = default;
            size = default;

            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath)) return false;

            Texture2D texture = null;

            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!texture.LoadImage(bytes)) return false;

                size = new Vector2Int(texture.width, texture.height);
                Color32[] pixels = texture.GetPixels32();      // 第 0 行 = 最下面一行

                int minX = size.x, minY = size.y, maxX = -1, maxY = -1;

                for (int y = 0; y < size.y; y++)
                {
                    int row = y * size.x;

                    for (int x = 0; x < size.x; x++)
                    {
                        if (pixels[row + x].a <= AlphaThreshold) continue;

                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX < 0) return false;      // 全透明

                bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[美术工具] 读取 {assetPath} 失败：{e.Message}");
                return false;
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }
    }
}
