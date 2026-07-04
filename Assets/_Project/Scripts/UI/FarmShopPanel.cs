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
        [SerializeField] private RectTransform seedButton;
        [SerializeField] private RectTransform potatoButton;
        [SerializeField] private RectTransform purchaseButton;
        [SerializeField] private RectTransform closeButton;
        [SerializeField] private Text priceText;
        [SerializeField] private Text statusText;

        private Action purchaseRequested;
        private Action<string> cropPlantRequested;
        private Action visibilityChanged;
        private bool canPurchase;
        private bool canPlantWheat;
        private bool canPlantPotato;

        public bool IsOpen => panel != null && panel.activeSelf;

        public static FarmShopPanel Create(Transform parent)
        {
            GameObject shopTab = CreateImage("Shop Tab", parent, new Color(0.30f, 0.45f, 0.22f, 0.97f));
            RectTransform shopTabRect = shopTab.GetComponent<RectTransform>();
            SetAnchored(shopTabRect, new Vector2(1f, 0f), new Vector2(158f, 58f), new Vector2(-24f, 132f), new Vector2(1f, 0f));
            shopTab.GetComponent<Image>().raycastTarget = false;
            Text shopTabLabel = CreateText("Label", shopTab.transform, "상점", 24, TextAnchor.MiddleCenter, new Color(0.88f, 0.94f, 0.80f));
            SetStretch(shopTabLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 4f, -4f, -4f);

            GameObject panelObject = CreateImage("Shop Panel", parent, Color.white);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetAnchored(panelRect, new Vector2(0.5f, 0.5f), new Vector2(650f, 457f), Vector2.zero);
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = LoadFirstSprite("Image/panel_shop_borderless");
            panelImage.raycastTarget = false;

            Text heading = CreateText("Heading", panelObject.transform, "작물 상점", 28, TextAnchor.MiddleLeft, new Color(0.20f, 0.25f, 0.15f));
            SetAnchored(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 48f), new Vector2(105f, -34f), new Vector2(0.5f, 1f));

            GameObject close = CreateImage("Close", panelObject.transform, new Color(0.32f, 0.42f, 0.22f, 0.95f));
            RectTransform closeRect = close.GetComponent<RectTransform>();
            SetAnchored(closeRect, new Vector2(1f, 1f), new Vector2(38f, 38f), new Vector2(-16f, -16f), new Vector2(1f, 1f));
            Text closeLabel = CreateText("Label", close.transform, "X", 23, TextAnchor.MiddleCenter, Color.white);
            SetStretch(closeLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 4f, -4f, -4f);

            GameObject product = CreateImage("Selected Product", panelObject.transform, Color.clear);
            SetAnchored(product.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(330f, 270f), new Vector2(105f, -5f));

            Text name = CreateText("Name", product.transform, "씨앗과 밭 구매", 27, TextAnchor.MiddleLeft, new Color(0.10f, 0.14f, 0.07f));
            SetAnchored(name.rectTransform, new Vector2(0f, 1f), new Vector2(180f, 48f), new Vector2(22f, -18f), new Vector2(0f, 1f));

            Text description = CreateText("Description", product.transform, "밀이나 감자 가격 버튼을 누른 뒤\n빈 밭을 선택해 심으세요.\n밭은 원하는 위치에 추가할 수 있습니다.", 19, TextAnchor.UpperLeft, new Color(0.12f, 0.17f, 0.09f));
            SetAnchored(description.rectTransform, new Vector2(0f, 0.5f), new Vector2(290f, 80f), new Vector2(22f, 10f), new Vector2(0f, 0.5f));

            Text price = CreateText("Price", product.transform, "상품 버튼에 가격이 표시됩니다.", 19, TextAnchor.MiddleLeft, new Color(0.28f, 0.14f, 0.03f));
            SetAnchored(price.rectTransform, new Vector2(0f, 0f), new Vector2(180f, 48f), new Vector2(22f, 14f), new Vector2(0f, 0f));

            GameObject seed = CreateImage("Plant Wheat", panelObject.transform, Color.clear);
            RectTransform seedRect = seed.GetComponent<RectTransform>();
            SetAnchored(seedRect, new Vector2(0.5f, 0.5f), new Vector2(88f, 34f), new Vector2(-154f, 92f));
            Text seedLabel = CreateText("Label", seed.transform, "10원", 20, TextAnchor.MiddleCenter, new Color(0.05f, 0.09f, 0.03f));
            SetStretch(seedLabel.rectTransform, 0f, 0f, 1f, 1f, 2f, 4f, -2f, 0f);

            Text wheatName = CreateText("Wheat Name", panelObject.transform, "밀", 18, TextAnchor.MiddleCenter, new Color(0.08f, 0.12f, 0.05f));
            SetAnchored(wheatName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70f, 26f), new Vector2(-250f, 60f));

            GameObject potato = CreateImage("Plant Potato", panelObject.transform, Color.clear);
            RectTransform potatoRect = potato.GetComponent<RectTransform>();
            SetAnchored(potatoRect, new Vector2(0.5f, 0.5f), new Vector2(88f, 34f), new Vector2(-154f, -35f));
            Text potatoLabel = CreateText("Label", potato.transform, "20원", 20, TextAnchor.MiddleCenter, new Color(0.05f, 0.09f, 0.03f));
            SetStretch(potatoLabel.rectTransform, 0f, 0f, 1f, 1f, 2f, 4f, -2f, 0f);

            Text potatoName = CreateText("Potato Name", panelObject.transform, "감자", 18, TextAnchor.MiddleCenter, new Color(0.08f, 0.12f, 0.05f));
            SetAnchored(potatoName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70f, 26f), new Vector2(-250f, -67f));

            GameObject purchase = CreateImage("Purchase Plot", panelObject.transform, Color.clear);
            RectTransform purchaseRect = purchase.GetComponent<RectTransform>();
            SetAnchored(purchaseRect, new Vector2(0.5f, 0.5f), new Vector2(88f, 34f), new Vector2(-154f, -162f));
            Text purchaseLabel = CreateText("Label", purchase.transform, "100원", 19, TextAnchor.MiddleCenter, new Color(0.05f, 0.09f, 0.03f));
            SetStretch(purchaseLabel.rectTransform, 0f, 0f, 1f, 1f, 2f, 4f, -2f, 0f);

            Text plotName = CreateText("Plot Name", panelObject.transform, "밭", 18, TextAnchor.MiddleCenter, new Color(0.08f, 0.12f, 0.05f));
            SetAnchored(plotName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70f, 26f), new Vector2(-250f, -194f));

            Text status = CreateText("Status", panelObject.transform, string.Empty, 17, TextAnchor.MiddleCenter, new Color(0.35f, 0.20f, 0.06f));
            SetAnchored(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(335f, 64f), new Vector2(105f, 42f), new Vector2(0.5f, 0f));

            FarmShopPanel shop = parent.gameObject.AddComponent<FarmShopPanel>();
            MakeTextCrisp(
                shopTabLabel,
                heading,
                name,
                description,
                price,
                seedLabel,
                wheatName,
                potatoLabel,
                potatoName,
                purchaseLabel,
                plotName,
                status);

            shop.Configure(panelObject, shopTabRect, seedRect, potatoRect, purchaseRect, closeRect, price, status);
            return shop;
        }

        public void Configure(
            GameObject panelObject,
            RectTransform tabButton,
            RectTransform plantSeedButton,
            RectTransform plantPotatoButton,
            RectTransform buyButton,
            RectTransform closeButtonRect,
            Text priceLabel,
            Text statusLabel)
        {
            panel = panelObject;
            shopTabButton = tabButton;
            seedButton = plantSeedButton;
            potatoButton = plantPotatoButton;
            purchaseButton = buyButton;
            closeButton = closeButtonRect;
            priceText = priceLabel;
            statusText = statusLabel;
            panel.SetActive(false);
        }

        public void Initialize(
            Action onPurchaseRequested,
            Action<string> onCropPlantRequested,
            Action onVisibilityChanged)
        {
            purchaseRequested = onPurchaseRequested;
            cropPlantRequested = onCropPlantRequested;
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

        public void Refresh(int plotPrice, int wheatSeedPrice, int potatoSeedPrice, int money, int availableSlots)
        {
            canPurchase = money >= plotPrice && availableSlots > 0;
            canPlantWheat = money >= wheatSeedPrice;
            canPlantPotato = money >= potatoSeedPrice;
            if (priceText != null)
            {
                priceText.text = $"밀 {wheatSeedPrice:N0}원 · 감자 {potatoSeedPrice:N0}원 · 밭 {plotPrice:N0}원";
            }

            if (statusText == null)
            {
                return;
            }

            statusText.text = availableSlots <= 0
                ? $"보유금 {money:N0}원 · 모든 밭 배치 완료"
                : $"보유금 {money:N0}원 · 밭 추가 {plotPrice:N0}원";
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

                if (Contains(seedButton, screenPosition))
                {
                    if (canPlantWheat)
                    {
                        cropPlantRequested?.Invoke("wheat");
                    }

                    return;
                }

                if (Contains(potatoButton, screenPosition))
                {
                    if (canPlantPotato)
                    {
                        cropPlantRequested?.Invoke("potato");
                    }

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
