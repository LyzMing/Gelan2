using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DialogueDemo.EditorTools
{
    /// <summary>
    /// 2D 场景搭建后的自检：确认精灵、2D 物理组件与 UI 引用齐全。
    /// 批处理：-executeMethod DialogueDemo.EditorTools.DemoSceneValidator2D.ValidateBatch
    /// 成功时日志里会出现 "[DialogueDemo2D][VALIDATION] PASS"。
    /// </summary>
    public static class DemoSceneValidator2D
    {
        [MenuItem("Tools/Dialogue Demo 2D/校验 2D 演示场景", false, 20)]
        public static void ValidateFromMenu()
        {
            Validate();
        }

        public static void ValidateBatch()
        {
            Validate();
        }

        static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSceneBuilder2D.ScenePath) == null)
            {
                Debug.LogError("[DialogueDemo2D][VALIDATION] FAIL：场景资产不存在 " + DemoSceneBuilder2D.ScenePath);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSceneBuilder2D.HallScenePath) == null)
            {
                Debug.LogError("[DialogueDemo2D][VALIDATION] FAIL：大厅场景不存在 " + DemoSceneBuilder2D.HallScenePath);
                return;
            }

            EditorSceneManager.OpenScene(DemoSceneBuilder2D.ScenePath, OpenSceneMode.Single);

            var report = new StringBuilder();
            int failures = 0;

            void Check(string label, bool condition, string detail = "")
            {
                if (condition)
                {
                    report.AppendLine("[OK]   " + label);
                }
                else
                {
                    failures++;
                    report.AppendLine("[FAIL] " + label + (string.IsNullOrEmpty(detail) ? "" : " → " + detail));
                }
            }

            var manager = Object.FindObjectOfType<DialogueManager>();
            Check("场景中有 DialogueManager", manager != null);
            Check("DialogueManager 绑定了 DialogueUI", manager != null && manager.UI != null);

            var ui = Object.FindObjectOfType<DialogueUI>();
            Check("场景中有 DialogueUI", ui != null);
            if (ui != null)
            {
                var uiSo = new SerializedObject(ui);
                Check("DialogueUI.panelGroup", uiSo.FindProperty("panelGroup").objectReferenceValue != null);
                Check("DialogueUI.speakerText", uiSo.FindProperty("speakerText").objectReferenceValue != null);
                Check("DialogueUI.contentText", uiSo.FindProperty("contentText").objectReferenceValue != null);
                Check("DialogueUI.hintText", uiSo.FindProperty("hintText").objectReferenceValue != null);
            }

            var camera = Camera.main;
            Check("场景中有正交相机", camera != null && camera.orthographic);
            Check("工程启用了 URP", GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset);
            Check("相机挂有 URP Camera Data",
                camera != null && camera.GetComponent<UniversalAdditionalCameraData>() != null);

            var light2D = Object.FindObjectOfType<Light2D>();
            Check("场景中有 Global Light 2D",
                light2D != null && light2D.lightType == Light2D.LightType.Global);

            var player = GameObject.FindGameObjectWithTag("Player");
            Check("场景中有 Player（Tag=Player）", player != null);
            if (player != null)
            {
                var body = player.GetComponent<Rigidbody2D>();
                Check("Player 带 Rigidbody2D", body != null);
                Check("Player 不受重力影响", body != null && Mathf.Approximately(body.gravityScale, 0f));
                Check("Player 锁定旋转", body != null && body.constraints.HasFlag(RigidbodyConstraints2D.FreezeRotation));
                Check("Player 带 CircleCollider2D", player.GetComponent<CircleCollider2D>() != null);
                Check("Player 带 PlayerController2D", player.GetComponent<PlayerController2D>() != null);

                var visual = player.transform.Find("Visual");
                var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                Check("Player 有精灵", renderer != null && renderer.sprite != null);
                Check("Player 使用 URP Sprite-Lit 材质",
                    renderer != null && renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.shader.name.Contains("Sprite-Lit"));
            }

            var items = Object.FindObjectsOfType<InteractableItem2D>();
            Check("场景中有 2 个可交互物品", items.Length == 2, "实际 " + items.Length + " 个");

            foreach (var item in items)
            {
                Check(item.name + " 配置了对话", item.dialogue != null && item.dialogue.IsValid);
                Check(item.name + " 配置了视觉精灵",
                    item.visual != null && item.visual.GetComponent<SpriteRenderer>() != null &&
                    item.visual.GetComponent<SpriteRenderer>().sprite != null);
                Check(item.name + " 配置了光晕", item.glow != null && item.glow.sprite != null);

                var collider = item.GetComponent<Collider2D>();
                Check(item.name + " 的接触区是 Trigger", collider != null && collider.isTrigger);
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            Check("场景中有 Canvas", canvas != null);
            Check("Canvas 挂了中文字体引导", canvas != null && canvas.GetComponent<CjkTextBootstrap>() != null);
            Check("Canvas 下有至少 4 个 Text", canvas != null && canvas.GetComponentsInChildren<Text>(true).Length >= 4);

            if (canvas != null)
            {
                var panel = canvas.transform.Find("DialoguePanel");
                Check("Canvas 下有 DialoguePanel", panel != null);
                Check("DialoguePanel 初始透明", panel != null && panel.GetComponent<CanvasGroup>().alpha < 0.01f);
            }

            var ground = GameObject.Find("Room/Ground");
            Check("地面使用 Tiled 精灵", ground != null &&
                ground.GetComponent<SpriteRenderer>().drawMode == SpriteDrawMode.Tiled);

            var walls = GameObject.Find("Room/WallTop");
            Check("房间墙壁带碰撞体", walls != null && walls.GetComponent<BoxCollider2D>() != null);

            var portals = Object.FindObjectsOfType<ScenePortal2D>();
            Check("主场景有传送门", portals.Length >= 1);
            bool mainPortalToHall = false;
            foreach (var portal in portals)
            {
                if (portal.TargetScene == DemoSceneBuilder2D.HallSceneName) mainPortalToHall = true;
            }
            Check("主场景传送门指向大厅", mainPortalToHall);

            bool mainInBuildSettings = false;
            bool hallInBuildSettings = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == DemoSceneBuilder2D.ScenePath) mainInBuildSettings = true;
                if (scene.path == DemoSceneBuilder2D.HallScenePath) hallInBuildSettings = true;
            }
            Check("主场景已在 Build Settings", mainInBuildSettings);
            Check("大厅场景已在 Build Settings", hallInBuildSettings);

            // 审计：确认场景中没有任何 3D 内容
            Check("场景没有 3D 网格渲染器", Object.FindObjectsOfType<MeshRenderer>().Length == 0);
            Check("场景没有 MeshFilter", Object.FindObjectsOfType<MeshFilter>().Length == 0);
            Check("场景没有 3D 碰撞体", Object.FindObjectsOfType<Collider>().Length == 0);
            Check("场景没有 CharacterController", Object.FindObjectsOfType<CharacterController>().Length == 0);
            Check("场景没有 3D Rigidbody", Object.FindObjectsOfType<Rigidbody>().Length == 0);
            Check("场景没有 3D 灯光", Object.FindObjectsOfType<Light>().Length == 0);

            bool allCamerasOrthographic = true;
            var cameras = Object.FindObjectsOfType<Camera>();
            if (cameras.Length == 0) allCamerasOrthographic = false;
            foreach (var cam in cameras)
            {
                if (!cam.orthographic) allCamerasOrthographic = false;
            }
            Check("所有相机均为正交投影", allCamerasOrthographic);

            EditorSceneManager.OpenScene(DemoSceneBuilder2D.HallScenePath, OpenSceneMode.Single);

            var hallPlayer = GameObject.FindGameObjectWithTag("Player");
            Check("大厅场景有 Player", hallPlayer != null);
            Check("大厅场景有正交相机", Camera.main != null && Camera.main.orthographic);
            Check("大厅场景有 Global Light 2D",
                Object.FindObjectOfType<Light2D>() != null);

            var hallPortals = Object.FindObjectsOfType<ScenePortal2D>();
            bool hallPortalToMain = false;
            foreach (var portal in hallPortals)
            {
                if (portal.TargetScene == DemoSceneBuilder2D.MainSceneName) hallPortalToMain = true;
            }
            Check("大厅传送门指回主场景", hallPortalToMain);
            Check("大厅没有 3D 网格渲染器", Object.FindObjectsOfType<MeshRenderer>().Length == 0);
            Check("大厅没有 3D 碰撞体", Object.FindObjectsOfType<Collider>().Length == 0);

            Debug.Log("[DialogueDemo2D][VALIDATION]\n" + report);
            if (failures == 0)
            {
                Debug.Log("[DialogueDemo2D][VALIDATION] PASS");
            }
            else
            {
                Debug.LogError("[DialogueDemo2D][VALIDATION] FAIL，共 " + failures + " 处问题");
            }
        }
    }
}
