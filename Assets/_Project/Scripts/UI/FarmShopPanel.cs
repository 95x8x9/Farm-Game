using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public sealed class FarmShopPanel : MonoBehaviour
    {
        private const string KoreanFontPath = "Fonts/NotoSansKR-VF";

        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform shopTabButton;
        [SerializeField] private RectTransform wheatRow;
        [SerializeField] private RectTransform plotRow;
        [SerializeField] private RectTransform removeRow;
        [SerializeField] private RectTransform closeButton;
        [SerializeField] private Text wheatDescText;
        [SerializeField] private Text plotDescText;

        private Action purchaseRequested;
        private Action<string> cropPlantRequested;
        private Action plotRemoveRequested;
        private Action visibilityChanged;

        public bool IsOpen => panel != null && panel.activeSelf;

        public static FarmShopPanel Create(Transform parent)
        {
            GameObject shopTab = CreateImage("Shop Tab", parent, Color.white);
            RectTransform shopTabRect = shopTab.GetComponent<RectTransform>();
            SetAnchored(shopTabRect, new Vector2(1f, 0f), new Vector2(158f, 58f), new Vector2(-24f, 132f), new Vector2(1f, 0f));
            Image shopTabImage = shopTab.GetComponent<Image>();
            shopTabImage.sprite = LoadFirstSprite("Image/btn_cart");
            shopTabImage.preserveAspect = true;
            shopTabImage.raycastTarget = false;

            GameObject panelObject = CreateImage("Shop Panel", parent, Color.white);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetAnchored(panelRect, new Vector2(0.5f, 0.5f), new Vector2(650f, 457f), Vector2.zero);
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = LoadFirstSprite("Image/panel_shop");
            panelImage.raycastTarget = false;

            GameObject close = CreateImage("Close", panelObject.transform, Color.white);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            SetAnchored(closeRect, new Vector2(1f, 1f), new Vector2(42f, 44f), new Vector2(-14f, -14f), new Vector2(1f, 1f));
            Image closeImage = close.GetComponent<Image>();
            closeImage.sprite = LoadFirstSprite("Image/btn_close");
            closeImage.preserveAspect = true;

            // 이미지에 그려진 3개 행(아이콘·코인·초록 버튼)에 맞춘 클릭 영역과 텍스트.
            RectTransform wheatRowRect = CreateRowHitArea(panelObject.transform, "Wheat Row", 136f);
            RectTransform plotRowRect = CreateRowHitArea(panelObject.transform, "Plot Row", 1f);
            RectTransform removeRowRect = CreateRowHitArea(panelObject.transform, "Remove Row", -133f);

            Color titleColor = new(0.10f, 0.14f, 0.07f);
            Color descColor = new(0.24f, 0.20f, 0.12f);
            Color buttonTextColor = new(0.07f, 0.12f, 0.04f);

            Text wheatTitle = CreateText("Wheat Title", panelObject.transform, "[밀 씨앗]", 24, TextAnchor.MiddleLeft, titleColor);
            SetAnchored(wheatTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(362f, 40f), new Vector2(101f, 162f));
            Text wheatDesc = CreateText("Wheat Desc", panelObject.transform, "가격 -원 / 판매 시 -원 / 성장 -분", 17, TextAnchor.MiddleLeft, descColor);
            SetAnchored(wheatDesc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(362f, 30f), new Vector2(101f, 112f));
            Text wheatButtonLabel = CreateText("Wheat Button Label", panelObject.transform, "구매", 19, TextAnchor.MiddleCenter, buttonTextColor);
            SetAnchored(wheatButtonLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(86f, 30f), new Vector2(-150f, 107f));

            Text plotTitle = CreateText("Plot Title", panelObject.transform, "[밭]", 24, TextAnchor.MiddleLeft, titleColor);
            SetAnchored(plotTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(362f, 40f), new Vector2(101f, 27f));
            Text plotDesc = CreateText("Plot Desc", panelObject.transform, "가격 -원 / 삭제 시 0원 / 설치 즉시", 17, TextAnchor.MiddleLeft, descColor);
            SetAnchored(plotDesc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(362f, 30f), new Vector2(101f, -23f));
            Text plotButtonLabel = CreateText("Plot Button Label", panelObject.transform, "설치", 19, TextAnchor.MiddleCenter, buttonTextColor);
            SetAnchored(plotButtonLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(86f, 30f), new Vector2(-150f, -28f));

            Text removeTitle = CreateText("Remove Title", panelObject.transform, "[밭 삭제]", 24, TextAnchor.MiddleLeft, titleColor);
            SetAnchored(removeTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(362f, 40f), new Vector2(101f, -107f));
            Text removeDesc = CreateText("Remove Desc", panelObject.transform, "가격 0원 / 빈 밭 클릭 / 삭제 즉시", 17, TextAnchor.MiddleLeft, descColor);
            SetAnchored(removeDesc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(362f, 30f), new Vector2(101f, -157f));
            Text removeButtonLabel = CreateText("Remove Button Label", panelObject.transform, "삭제", 19, TextAnchor.MiddleCenter, buttonTextColor);
            SetAnchored(removeButtonLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(86f, 30f), new Vector2(-150f, -162f));

            FarmShopPanel shop = parent.gameObject.AddComponent<FarmShopPanel>();
            MakeTextCrisp(
                wheatTitle,
                wheatDesc,
                wheatButtonLabel,
                plotTitle,
                plotDesc,
                plotButtonLabel,
                removeTitle,
                removeDesc,
                removeButtonLabel);

            shop.Configure(panelObject, shopTabRect, wheatRowRect, plotRowRect, removeRowRect, closeRect, wheatDesc, plotDesc);
            return shop;
        }

        public void Configure(
            GameObject panelObject,
            RectTransform tabButton,
            RectTransform wheatRowRect,
            RectTransform plotRowRect,
            RectTransform removeRowRect,
            RectTransform closeButtonRect,
            Text wheatDesc,
            Text plotDesc)
        {
            panel = panelObject;
            shopTabButton = tabButton;
            wheatRow = wheatRowRect;
            plotRow = plotRowRect;
            removeRow = removeRowRect;
            closeButton = closeButtonRect;
            wheatDescText = wheatDesc;
            plotDescText = plotDesc;
            panel.SetActive(false);
        }

        public void Initialize(
            Action onPurchaseRequested,
            Action<string> onCropPlantRequested,
            Action onPlotRemoveRequested,
            Action onVisibilityChanged)
        {
            purchaseRequested = onPurchaseRequested;
            cropPlantRequested = onCropPlantRequested;
            plotRemoveRequested = onPlotRemoveRequested;
            visibilityChanged = onVisibilityChanged;
        }

        public void Open()
        {
            if (panel == null)
            {
                return;
            }

            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            visibilityChanged?.Invoke();
        }

        public void Close()
        {
            if (panel == null || !panel.activeSelf)
            {
                return;
            }

            panel.SetActive(false);
            visibilityChanged?.Invoke();
        }

        public void Refresh(int plotPrice, int seedPrice, int sellPrice, int growthSeconds, int money, int availableSlots)
        {
            if (wheatDescText != null)
            {
                string growth = growthSeconds >= 60 ? $"{growthSeconds / 60}분" : $"{growthSeconds}초";
                wheatDescText.text = $"가격 {seedPrice:N0}원 / 판매 시 {sellPrice:N0}원 / 성장 {growth}";
            }

            if (plotDescText != null)
            {
                plotDescText.text = availableSlots > 0
                    ? $"가격 {plotPrice:N0}원 / 삭제 시 0원 / 설치 즉시"
                    : "모든 밭을 배치했습니다";
            }
        }

        private void Update()
        {
            if (!TryGetPointerPress(out Vector2 screenPosition))
            {
                return;
            }

            if (IsOpen)
            {
                if (Contains(closeButton, screenPosition))
                {
                    Close();
                    return;
                }

                if (Contains(wheatRow, screenPosition))
                {
                    cropPlantRequested?.Invoke("wheat");
                    return;
                }

                if (Contains(plotRow, screenPosition))
                {
                    purchaseRequested?.Invoke();
                    return;
                }

                if (Contains(removeRow, screenPosition))
                {
                    plotRemoveRequested?.Invoke();
                }

                return;
            }

            if (Contains(shopTabButton, screenPosition))
            {
                Open();
            }
        }

        private static RectTransform CreateRowHitArea(Transform parent, string name, float centerY)
        {
            GameObject row = CreateImage(name, parent, Color.clear);
            RectTransform rect = row.GetComponent<RectTransform>();
            SetAnchored(rect, new Vector2(0.5f, 0.5f), new Vector2(588f, 124f), new Vector2(0f, centerY));
            return rect;
        }

        private static bool Contains(RectTransform rect, Vector2 screenPosition)
        {
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition);
        }

        private static bool TryGetPointerPress(out Vector2 position)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        private static Sprite LoadFirstSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites.Length > 0)
            {
                return sprites[0];
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            return texture == null
                ? null
                : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void MakeTextCrisp(params Text[] labels)
        {
            foreach (Text label in labels)
            {
                if (label == null)
                {
                    continue;
                }

                label.fontStyle = FontStyle.Bold;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;

                // 글리프를 2배 해상도로 굽고 절반 크기로 표시해 선명도를 높인다.
                label.fontSize *= 2;
                label.rectTransform.sizeDelta *= 2f;
                label.rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);
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
