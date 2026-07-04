using UnityEngine;

namespace FarmGame.Farm
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FarmBackdrop : MonoBehaviour
    {
        // 목업 전체 화면(768px)이 세로 10유닛이므로 HUD를 제외한 밴드(516px)는 6.72유닛.
        private const float WorldHeight = 6.72f;
        private const float WorldCenterY = 0.30f;

        private void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            Sprite background = LoadFirstSprite("Image/bg_farm_pixel");
            if (background == null)
            {
                return;
            }

            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = background;
            renderer.color = Color.white;
            renderer.sortingOrder = -8;
            float scale = WorldHeight / background.bounds.size.y;
            transform.position = new Vector3(0f, WorldCenterY, 1f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public static void Ensure()
        {
            DisableLegacyObject("Small Farm Map");
            DisableLegacyObject("Farm Path");

            if (FindFirstObjectByType<FarmBackdrop>() != null)
            {
                return;
            }

            GameObject backdrop = new("Farm Backdrop");
            backdrop.AddComponent<SpriteRenderer>();
            backdrop.AddComponent<FarmBackdrop>();
        }

        private static void DisableLegacyObject(string name)
        {
            GameObject legacy = GameObject.Find(name);
            if (legacy != null)
            {
                legacy.SetActive(false);
            }
        }

        private static Sprite LoadFirstSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            return sprites.Length > 0 ? sprites[0] : Resources.Load<Sprite>(resourcePath);
        }
    }
}
