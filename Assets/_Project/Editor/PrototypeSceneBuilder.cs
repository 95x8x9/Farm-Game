#if UNITY_EDITOR
using System.Collections.Generic;
using FarmGame.Core;
using FarmGame.Data;
using FarmGame.Farm;
using FarmGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmGame.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenePath = ProjectRoot + "/Scenes/FarmScene.unity";
        private const string WheatPath = ProjectRoot + "/Data/Crops/Wheat.asset";
        private const string KoreanFontPath = "Fonts/NotoSansKR-VF";
        private const string ProgressBarAssetRoot = ProjectRoot + "/Resources/ProgressBar";

        [MenuItem("Farm Game/Prototype/Rebuild Farm Scene")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog("Farm Game", "FarmScene 프로토타입을 생성했습니다.", "확인");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolders();
            EnsureProgressBarImportSettings();
            CropDefinition wheat = CreateOrUpdateWheat();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "FarmScene";

            Camera camera = CreateCamera();
            CreateLighting();
            CreateMapBackdrop();

            GameObject systems = new("Systems");
            FarmGameManager manager = new GameObject("GameManager").AddComponent<FarmGameManager>();
            manager.transform.SetParent(systems.transform);

            List<FarmCellView> cells = CreateFarmCells();

            GameObject uiRoot = new("UI");
            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 600f);
            scaler.matchWidthOrHeight = 0.5f;
            uiRoot.AddComponent<GraphicRaycaster>();
            uiRoot.AddComponent<RuntimeFontInstaller>();

            FarmHud hud = BuildHud(uiRoot.transform);
            WateringMinigame minigame = BuildWateringMinigame(uiRoot.transform);
            FarmShopPanel shop = FarmShopPanel.Create(uiRoot.transform);
            FirstPlotTutorialPopup tutorialPopup = FirstPlotTutorialPopup.Create(uiRoot.transform);
            manager.Configure(wheat, cells.ToArray(), hud, minigame, shop, tutorialPopup);

            FarmInputController input = new GameObject("FarmInputController").AddComponent<FarmInputController>();
            input.transform.SetParent(systems.transform);
            input.Configure(camera, manager);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Farm prototype scene generated: {ScenePath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder(ProjectRoot, "Scenes");
            EnsureFolder(ProjectRoot, "Data");
            EnsureFolder(ProjectRoot, "Resources");
            EnsureFolder(ProjectRoot + "/Data", "Crops");
            EnsureFolder(ProjectRoot + "/Resources", "Fonts");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static CropDefinition CreateOrUpdateWheat()
        {
            CropDefinition wheat = AssetDatabase.LoadAssetAtPath<CropDefinition>(WheatPath);
            if (wheat == null)
            {
                wheat = ScriptableObject.CreateInstance<CropDefinition>();
                AssetDatabase.CreateAsset(wheat, WheatPath);
            }

            wheat.Configure("wheat", "밀", 10, 20, 60, 1);
            EditorUtility.SetDirty(wheat);
            return wheat;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.51f, 0.66f, 0.36f);
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Base;
            return camera;
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new("Global Light 2D");
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static void CreateMapBackdrop()
        {
            // 스프라이트 로드와 배치는 FarmBackdrop이 런타임에 처리한다.
            GameObject map = new("Farm Backdrop");
            map.AddComponent<SpriteRenderer>();
            map.AddComponent<FarmBackdrop>();
        }

        private static List<FarmCellView> CreateFarmCells()
        {
            GameObject container = new("FarmCells");
            List<FarmCellView> views = new();
            const float spacing = 1.55f;

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    GameObject cellObject = new($"FarmCell ({x},{y})");
                    cellObject.transform.SetParent(container.transform);
                    cellObject.transform.position = new Vector3((x - 1) * spacing - 0.65f, 1.45f - y * spacing, 0f);
                    cellObject.transform.localScale = new Vector3(1.28f, 1.28f, 1f);

                    SpriteRenderer soil = cellObject.AddComponent<SpriteRenderer>();
                    soil.sortingOrder = 0;
                    BoxCollider2D collider = cellObject.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(1.06f, 1.12f);

                    SpriteRenderer crop = CreateChildRenderer(cellObject.transform, "Crop", 2);
                    SpriteRenderer accent = CreateChildRenderer(cellObject.transform, "State Accent", 3);

                    FarmCellView view = cellObject.AddComponent<FarmCellView>();
                    view.Configure(x, y, soil, crop, accent);
                    views.Add(view);
                }
            }

            return views;
        }

        private static SpriteRenderer CreateChildRenderer(Transform parent, string name, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static FarmHud BuildHud(Transform parent)
        {
            GameObject topBar = CreateImage("Top Bar", parent, new Color(0.18f, 0.23f, 0.16f, 0.97f));
            SetStretch(topBar.GetComponent<RectTransform>(), 0f, 1f, 1f, 1f, 0f, -76f, 0f, 0f);

            Text title = CreateText("Title", topBar.transform, "작은 밀 농장", 28, TextAnchor.MiddleCenter, new Color(0.93f, 0.95f, 0.85f));
            SetAnchored(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(280f, 54f), Vector2.zero);

            GameObject coinObject = CreateImage("Coin Icon", topBar.transform, Color.white);
            Image coinImage = coinObject.GetComponent<Image>();
            Sprite[] coinSprites = Resources.LoadAll<Sprite>("Image/icon_coin");
            coinImage.sprite = coinSprites.Length > 0 ? coinSprites[0] : null;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;
            SetAnchored(coinObject.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(36f, 36f), new Vector2(28f, 0f), new Vector2(0f, 0.5f));

            Text money = CreateText("Money", topBar.transform, "보유금", 25, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.28f));
            SetAnchored(money.rectTransform, new Vector2(0f, 0.5f), new Vector2(236f, 54f), new Vector2(72f, 0f), new Vector2(0f, 0.5f));

            Text harvest = CreateText("Harvest", topBar.transform, "밀 수확", 25, TextAnchor.MiddleRight, new Color(0.75f, 1f, 0.60f));
            SetAnchored(harvest.rectTransform, new Vector2(1f, 0.5f), new Vector2(280f, 54f), new Vector2(-28f, 0f), new Vector2(1f, 0.5f));

            GameObject messagePanel = CreateImage("Message Panel", parent, new Color(0.19f, 0.25f, 0.14f, 0.92f));
            SetStretch(messagePanel.GetComponent<RectTransform>(), 0f, 0f, 1f, 0f, 0f, 0f, 0f, 118f);

            Text message = CreateText(
                "Message",
                messagePanel.transform,
                "상점에서 밭을 골라 원하는 위치에 배치하세요.",
                22,
                TextAnchor.MiddleCenter,
                Color.white);
            SetStretch(message.rectTransform, 0f, 0f, 1f, 1f, 20f, 34f, -20f, -8f);

            Text legend = CreateText(
                "Legend",
                messagePanel.transform,
                "상점: 자유 배치   빨강: 설치 불가   파란 표시: 물 필요   초록: 성장   노랑: 수확",
                16,
                TextAnchor.MiddleCenter,
                new Color(0.78f, 0.85f, 0.68f));
            SetStretch(legend.rectTransform, 0f, 0f, 1f, 0f, 12f, 4f, -12f, 34f);

            FarmHud hud = parent.gameObject.AddComponent<FarmHud>();
            hud.Configure(money, harvest, message);
            return hud;
        }

        private static WateringMinigame BuildWateringMinigame(Transform parent)
        {
            GameObject panel = CreateImage("Watering Minigame", parent, new Color(0.12f, 0.09f, 0.06f, 0.97f));
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(650f, 250f), Vector2.zero);

            Text heading = CreateText("Heading", panel.transform, "물주기 타이밍", 30, TextAnchor.MiddleCenter, new Color(0.55f, 0.85f, 1f));
            SetAnchored(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(500f, 50f), new Vector2(0f, -42f), new Vector2(0.5f, 1f));

            GameObject track = CreateImage("Track", panel.transform, Color.white);
            Image trackImage = track.GetComponent<Image>();
            trackImage.sprite = LoadProgressBarSprite("progressbar-track.png");
            trackImage.raycastTarget = false;
            SetAnchored(track.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(520f, 54f), new Vector2(0f, 5f));

            GameObject success = CreateImage("Success Zone", track.transform, Color.white);
            Image successImage = success.GetComponent<Image>();
            successImage.sprite = LoadProgressBarSprite("progressbar-fill.png");
            successImage.raycastTarget = false;
            SetAnchored(success.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(146f, 54f), Vector2.zero);

            GameObject frame = CreateImage("Frame", track.transform, Color.white);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.sprite = LoadProgressBarSprite("progressbar-frame.png");
            frameImage.raycastTarget = false;
            SetAnchored(frame.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(548f, 90f), Vector2.zero);

            GameObject marker = CreateImage("Marker", track.transform, Color.white);
            Image markerImage = marker.GetComponent<Image>();
            markerImage.sprite = LoadProgressBarSprite("progressbar-handle.png");
            markerImage.preserveAspect = true;
            markerImage.raycastTarget = false;
            SetAnchored(marker.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(30f, 92f), new Vector2(-222f, 0f));

            Text result = CreateText("Result", panel.transform, "초록 영역에서 클릭 또는 Space", 20, TextAnchor.MiddleCenter, Color.white);
            SetAnchored(result.rectTransform, new Vector2(0.5f, 0f), new Vector2(590f, 64f), new Vector2(0f, 38f), new Vector2(0.5f, 0f));

            WateringMinigame minigame = parent.gameObject.AddComponent<WateringMinigame>();
            minigame.Configure(panel, marker.GetComponent<RectTransform>(), result);
            return minigame;
        }

        private static Sprite LoadProgressBarSprite(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ProgressBarAssetRoot}/{fileName}");
        }

        private static void EnsureProgressBarImportSettings()
        {
            string[] fileNames =
            {
                "progressbar-frame.png",
                "progressbar-fill.png",
                "progressbar-track.png",
                "progressbar-handle.png"
            };

            foreach (string fileName in fileNames)
            {
                string assetPath = $"{ProgressBarAssetRoot}/{fileName}";
                if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            return gameObject;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.Load<Font>(KoreanFontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static void SetAnchored(
            RectTransform rect,
            Vector2 anchor,
            Vector2 size,
            Vector2 position,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetStretch(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
#endif
