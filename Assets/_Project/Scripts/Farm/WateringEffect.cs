using System.Collections;
using UnityEngine;

namespace FarmGame.Farm
{
    public sealed class WateringEffect : MonoBehaviour
    {
        private const float CanSortingOrder = 12f;
        private const float DropSortingOrder = 11f;
        private const float WetOverlaySortingOrder = 4f;

        private static Sprite cachedCanSprite;
        private static Sprite cachedDropSprite;
        private static Sprite cachedSquareSprite;

        private bool succeeded;

        public static void Play(Vector3 cellPosition, bool wateringSucceeded)
        {
            GameObject root = new("Watering Effect");
            root.transform.position = cellPosition;
            WateringEffect effect = root.AddComponent<WateringEffect>();
            effect.succeeded = wateringSucceeded;
        }

        private void Start()
        {
            EnsureSprites();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            int dropCount = succeeded ? 10 : 4;
            float pourDuration = succeeded ? 0.9f : 0.4f;

            SpriteRenderer wetOverlay = CreateRenderer("Wet Soil", cachedSquareSprite, (int)WetOverlaySortingOrder);
            wetOverlay.color = new Color(0.20f, 0.45f, 0.85f, 0f);
            wetOverlay.transform.localPosition = Vector3.zero;
            wetOverlay.transform.localScale = new Vector3(1.30f, 1.38f, 1f);

            SpriteRenderer can = CreateRenderer("Watering Can", cachedCanSprite, (int)CanSortingOrder);
            can.transform.localScale = cachedCanSprite == cachedSquareSprite
                ? new Vector3(0.9f, 0.65f, 1f)
                : Vector3.one;
            // 원본 이미지는 주둥이가 오른쪽을 향하므로 밭(왼쪽)을 향하도록 좌우반전한다.
            can.flipX = true;

            // 물뿌리개 등장: 오른쪽 위에서 미끄러져 들어오며 주둥이가 밭 쪽으로 기울어진다.
            Vector3 canStart = new(1.35f, 1.35f, -0.5f);
            Vector3 canPour = new(0.62f, 1.00f, -0.5f);
            yield return Animate(0.22f, t =>
            {
                can.transform.localPosition = Vector3.Lerp(canStart, canPour, t);
                can.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 28f, t));
                can.color = new Color(1f, 1f, 1f, t);
            });

            // 주둥이 끝(물뿌리개의 왼쪽 아래)에서 물방울을 떨어뜨린다.
            Vector3 spout = canPour + new Vector3(-0.52f, -0.18f, 0f);
            float interval = pourDuration / dropCount;
            for (int i = 0; i < dropCount; i++)
            {
                StartCoroutine(RunDrop(spout));
                float overlayAlpha = Mathf.Lerp(0f, 0.38f, (i + 1f) / dropCount);
                wetOverlay.color = new Color(0.20f, 0.45f, 0.85f, overlayAlpha);
                yield return new WaitForSeconds(interval);
            }

            // 물뿌리개 퇴장.
            yield return Animate(0.25f, t =>
            {
                can.transform.localPosition = Vector3.Lerp(canPour, canStart, t);
                can.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(28f, 0f, t));
                can.color = new Color(1f, 1f, 1f, 1f - t);
            });

            // 젖은 흙이 서서히 마른다.
            Color wetColor = wetOverlay.color;
            yield return Animate(0.9f, t => wetOverlay.color = new Color(wetColor.r, wetColor.g, wetColor.b, wetColor.a * (1f - t)));

            Destroy(gameObject);
        }

        private IEnumerator RunDrop(Vector3 spawnLocal)
        {
            SpriteRenderer drop = CreateRenderer("Water Drop", cachedDropSprite, (int)DropSortingOrder);
            float dropScale = cachedDropSprite == cachedSquareSprite ? 0.10f : 0.42f;
            drop.transform.localScale = new Vector3(dropScale, dropScale, 1f);
            drop.transform.localPosition = spawnLocal;
            drop.color = new Color(1f, 1f, 1f, 0.95f);

            Vector2 velocity = new(Random.Range(-1.4f, -0.6f), Random.Range(-0.4f, 0f));
            float landY = Random.Range(-0.30f, 0.20f);
            Vector3 position = spawnLocal;
            while (position.y > landY)
            {
                velocity.y -= 9f * Time.deltaTime;
                position += (Vector3)(velocity * Time.deltaTime);
                position.x = Mathf.Max(position.x, -0.55f);
                drop.transform.localPosition = position;
                yield return null;
            }

            // 착지: 납작해지며 사라지는 작은 스플래시.
            yield return Animate(0.16f, t =>
            {
                if (drop == null)
                {
                    return;
                }

                drop.transform.localScale = new Vector3(dropScale * (1f + t * 0.8f), dropScale * (1f - t * 0.7f), 1f);
                drop.color = new Color(1f, 1f, 1f, 0.95f * (1f - t));
            });

            if (drop != null)
            {
                Destroy(drop.gameObject);
            }
        }

        private static IEnumerator Animate(float duration, System.Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                apply(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void EnsureSprites()
        {
            cachedSquareSprite ??= CreateSquareSprite();
            cachedCanSprite ??= LoadFirstSprite("Image/watering_can") ?? cachedSquareSprite;
            cachedDropSprite ??= LoadFirstSprite("Image/icon_water") ?? cachedSquareSprite;
        }

        private static Sprite LoadFirstSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            return sprites.Length > 0 ? sprites[0] : Resources.Load<Sprite>(resourcePath);
        }

        private static Sprite CreateSquareSprite()
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Square",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
