using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public sealed class FarmHud : MonoBehaviour
    {
        [SerializeField] private Text moneyText;
        [SerializeField] private Text harvestText;
        [SerializeField] private Text messageText;

        private bool skinApplied;

        private void Awake()
        {
            ApplyBeigeSkin();
        }

        public void Configure(Text money, Text harvest, Text message)
        {
            moneyText = money;
            harvestText = harvest;
            messageText = message;
            ApplyBeigeSkin();
        }

        public void Refresh(int money, int harvested)
        {
            moneyText.text = $"보유금  {money:N0}원";
            harvestText.text = $"밀 수확  {harvested}회";
        }

        public void SetMessage(string message)
        {
            messageText.text = message;
        }

        // 씬에 저장된 옛 스타일(진녹색 단색)을 런타임에 베이지 픽셀 패널로 교체한다.
        private void ApplyBeigeSkin()
        {
            if (skinApplied || moneyText == null || harvestText == null || messageText == null)
            {
                return;
            }

            skinApplied = true;
            Sprite panelSprite = LoadFirstSprite("Image/panel_beige");

            RectTransform topBar = moneyText.transform.parent as RectTransform;
            StylePanel(topBar, panelSprite, new Vector2(6f, -76f), new Vector2(-6f, -4f));

            RectTransform messagePanel = messageText.transform.parent as RectTransform;
            StylePanel(messagePanel, panelSprite, new Vector2(6f, 4f), new Vector2(-6f, 118f));

            StyleText(moneyText, new Color(0.42f, 0.27f, 0.01f));
            StyleText(harvestText, new Color(0.09f, 0.27f, 0.04f));
            StyleText(messageText, new Color(0.07f, 0.05f, 0.02f));
            StyleChildText(topBar, "Title", new Color(0.12f, 0.09f, 0.04f));
            StyleChildText(messagePanel, "Legend", new Color(0.28f, 0.22f, 0.12f));
        }

        private static void StylePanel(RectTransform rect, Sprite sprite, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            if (rect.TryGetComponent(out Image image))
            {
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
                else
                {
                    image.color = new Color(0.97f, 0.93f, 0.85f, 0.96f);
                }
            }

            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void StyleText(Text label, Color color)
        {
            if (label == null)
            {
                return;
            }

            label.color = color;
            label.fontStyle = FontStyle.Bold;
        }

        private static void StyleChildText(Transform parent, string childName, Color color)
        {
            if (parent == null)
            {
                return;
            }

            Transform child = parent.Find(childName);
            if (child != null && child.TryGetComponent(out Text label))
            {
                StyleText(label, color);
            }
        }

        private static Sprite LoadFirstSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            return sprites.Length > 0 ? sprites[0] : Resources.Load<Sprite>(resourcePath);
        }
    }
}
