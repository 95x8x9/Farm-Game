using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public sealed class FirstPlotTutorialPopup : MonoBehaviour
    {
        private const string KoreanFontPath = "Fonts/NotoSansKR-VF";

        [SerializeField] private GameObject overlay;
        [SerializeField] private RectTransform purchaseButton;

        private Action<bool> dismissed;

        public bool IsVisible => overlay != null && overlay.activeSelf;

        public static FirstPlotTutorialPopup Create(Transform parent)
        {
            GameObject overlayObject = CreateImage("First Plot Tutorial", parent, new Color(0.02f, 0.04f, 0.02f, 0.72f));
            SetStretch(overlayObject.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);

            GameObject panel = CreateImage("Popup Panel", overlayObject.transform, Color.white);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(610f, 250f), Vector2.zero);
            Image panelImage = panel.GetComponent<Image>();
            Sprite panelSprite = LoadFirstSprite("Image/panel_beige");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }
            else
            {
                panelImage.color = new Color(0.96f, 0.91f, 0.73f, 1f);
            }

            Text message = CreateText(
                "Message",
                panel.transform,
                "상점에서 밭을 구매하고 원하는 위치에 설치해주세요!",
                25,
                TextAnchor.MiddleCenter,
                new Color(0.13f, 0.10f, 0.05f));
            message.fontStyle = FontStyle.Bold;
            message.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetAnchored(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(560f, 60f), new Vector2(0f, -30f), new Vector2(0.5f, 1f));

            GameObject purchaseButtonObject = CreateImage("Purchase Button", panel.transform, Color.white);
            RectTransform purchaseButtonRect = purchaseButtonObject.GetComponent<RectTransform>();
            SetAnchored(purchaseButtonRect, new Vector2(0.5f, 0f), new Vector2(360f, 72f), new Vector2(0f, 54f), new Vector2(0.5f, 0f));
            Image purchaseImage = purchaseButtonObject.GetComponent<Image>();
            Sprite buttonSprite = LoadFirstSprite("Image/btn_pixel_green");
            if (buttonSprite != null)
            {
                purchaseImage.sprite = buttonSprite;
                purchaseImage.type = Image.Type.Sliced;
            }
            else
            {
                purchaseImage.color = new Color(0.24f, 0.55f, 0.20f, 1f);
            }

            Text buttonLabel = CreateText(
                "Label",
                purchaseButtonObject.transform,
                "상점 열기",
                24,
                TextAnchor.MiddleCenter,
                new Color(0.07f, 0.12f, 0.04f));
            buttonLabel.fontStyle = FontStyle.Bold;
            SetStretch(buttonLabel.rectTransform, 0f, 0f, 1f, 1f, 12f, 6f, -12f, -6f);

            FirstPlotTutorialPopup popup = overlayObject.AddComponent<FirstPlotTutorialPopup>();
            popup.Configure(overlayObject, purchaseButtonRect);
            return popup;
        }

        public void Configure(GameObject overlayObject, RectTransform purchaseButtonRect)
        {
            overlay = overlayObject;
            purchaseButton = purchaseButtonRect;
        }

        public void Show(Action<bool> onDismissed)
        {
            dismissed = onDismissed;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            dismissed = null;
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsVisible || !TryGetPointerPress(out Vector2 screenPosition))
            {
                return;
            }

            bool purchaseRequested = purchaseButton != null
                && RectTransformUtility.RectangleContainsScreenPoint(purchaseButton, screenPosition);
            Action<bool> callback = dismissed;
            Hide();
            callback?.Invoke(purchaseRequested);
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
            return sprites.Length > 0 ? sprites[0] : Resources.Load<Sprite>(resourcePath);
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
