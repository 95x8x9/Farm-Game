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
        [SerializeField] private RectTransform purchaseButton;
        [SerializeField] private RectTransform closeButton;
        [SerializeField] private Text priceText;
        [SerializeField] private Text statusText;

        private Action purchaseRequested;
        private Action visibilityChanged;
        private bool canPurchase;

        public bool IsOpen => panel != null && panel.activeSelf;

        public static FarmShopPanel Create(Transform parent)
        {
            GameObject shopTab = CreateImage("Shop Tab", parent, new Color(0.22f, 0.48f, 0.18f, 1f));
            RectTransform shopTabRect = shopTab.GetComponent<RectTransform>();
            SetAnchored(shopTabRect, new Vector2(1f, 0f), new Vector2(150f, 58f), new Vector2(-24f, 132f), new Vector2(1f, 0f));
            Text tabLabel = CreateText("Label", shopTab.transform, "상점", 24, TextAnchor.MiddleCenter, Color.white);
            SetStretch(tabLabel.rectTransform, 0f, 0f, 1f, 1f, 8f, 4f, -8f, -4f);

            GameObject panelObject = CreateImage("Shop Panel", parent, new Color(0.06f, 0.09f, 0.06f, 0.97f));
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetAnchored(panelRect, new Vector2(1f, 0.5f), new Vector2(390f, 390f), new Vector2(-24f, 0f), new Vector2(1f, 0.5f));

            Text heading = CreateText("Heading", panelObject.transform, "농장 상점", 30, TextAnchor.MiddleLeft, Color.white);
            SetAnchored(heading.rectTransform, new Vector2(0f, 1f), new Vector2(250f, 56f), new Vector2(28f, -20f), new Vector2(0f, 1f));

            GameObject close = CreateImage("Close", panelObject.transform, new Color(0.35f, 0.38f, 0.35f, 1f));
            RectTransform closeRect = close.GetComponent<RectTransform>();
            SetAnchored(closeRect, new Vector2(1f, 1f), new Vector2(52f, 52f), new Vector2(-20f, -20f), new Vector2(1f, 1f));
            Text closeLabel = CreateText("Label", close.transform, "X", 23, TextAnchor.MiddleCenter, Color.white);
            SetStretch(closeLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 4f, -4f, -4f);

            GameObject product = CreateImage("Plot Product", panelObject.transform, new Color(0.88f, 0.79f, 0.57f, 1f));
            SetAnchored(product.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(334f, 185f), new Vector2(0f, 18f));

            Text name = CreateText("Name", product.transform, "밭 1칸", 27, TextAnchor.MiddleLeft, new Color(0.12f, 0.18f, 0.10f));
            SetAnchored(name.rectTransform, new Vector2(0f, 1f), new Vector2(180f, 48f), new Vector2(22f, -18f), new Vector2(0f, 1f));

            Text description = CreateText("Description", product.transform, "구매 후 농장 안의 원하는 위치를 선택하세요.", 18, TextAnchor.MiddleLeft, new Color(0.20f, 0.25f, 0.17f));
            SetAnchored(description.rectTransform, new Vector2(0f, 0.5f), new Vector2(290f, 55f), new Vector2(22f, 5f), new Vector2(0f, 0.5f));

            Text price = CreateText("Price", product.transform, "가격  100원", 23, TextAnchor.MiddleLeft, new Color(0.35f, 0.20f, 0.06f));
            SetAnchored(price.rectTransform, new Vector2(0f, 0f), new Vector2(180f, 48f), new Vector2(22f, 14f), new Vector2(0f, 0f));

            GameObject purchase = CreateImage("Purchase", product.transform, new Color(0.24f, 0.55f, 0.20f, 1f));
            RectTransform purchaseRect = purchase.GetComponent<RectTransform>();
            SetAnchored(purchaseRect, new Vector2(1f, 0f), new Vector2(112f, 54f), new Vector2(-18f, 14f), new Vector2(1f, 0f));
            Text purchaseLabel = CreateText("Label", purchase.transform, "배치", 22, TextAnchor.MiddleCenter, Color.white);
            SetStretch(purchaseLabel.rectTransform, 0f, 0f, 1f, 1f, 5f, 3f, -5f, -3f);

            Text status = CreateText("Status", panelObject.transform, string.Empty, 18, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.30f));
            SetAnchored(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(340f, 52f), new Vector2(0f, 22f), new Vector2(0.5f, 0f));

            FarmShopPanel shop = parent.gameObject.AddComponent<FarmShopPanel>();
            shop.Configure(panelObject, shopTabRect, purchaseRect, closeRect, price, status);
            return shop;
        }

        public void Configure(
            GameObject panelObject,
            RectTransform tabButton,
            RectTransform buyButton,
            RectTransform closeButtonRect,
            Text priceLabel,
            Text statusLabel)
        {
            panel = panelObject;
            shopTabButton = tabButton;
            purchaseButton = buyButton;
            closeButton = closeButtonRect;
            priceText = priceLabel;
            statusText = statusLabel;
            panel.SetActive(false);
        }

        public void Initialize(Action onPurchaseRequested, Action onVisibilityChanged)
        {
            purchaseRequested = onPurchaseRequested;
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

        public void Refresh(int price, int money, int availableSlots)
        {
            canPurchase = money >= price && availableSlots > 0;
            if (priceText != null)
            {
                priceText.text = $"가격  {price:N0}원";
            }

            if (statusText == null)
            {
                return;
            }

            statusText.text = availableSlots <= 0
                ? "더 이상 배치할 공간이 없습니다."
                : money < price
                    ? $"보유금이 {price - money:N0}원 부족합니다."
                    : $"배치 가능한 위치 {availableSlots}곳";
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

                if (Contains(purchaseButton, screenPosition))
                {
                    if (canPurchase)
                    {
                        purchaseRequested?.Invoke();
                    }

                    return;
                }

                return;
            }

            if (Contains(shopTabButton, screenPosition))
            {
                Open();
            }
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
