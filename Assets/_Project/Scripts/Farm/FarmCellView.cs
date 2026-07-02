using FarmGame.Data;
using UnityEngine;

namespace FarmGame.Farm
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class FarmCellView : MonoBehaviour
    {
        private static Sprite squareSprite;
        private static Sprite cachedSoilSprite;
        private static Sprite cachedSeedSprite;
        private static Sprite cachedSproutSprite;
        private static Sprite cachedWheatSprite;

        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private SpriteRenderer soilRenderer;
        [SerializeField] private SpriteRenderer cropRenderer;
        [SerializeField] private SpriteRenderer accentRenderer;
        [SerializeField] private Sprite soilSprite;
        [SerializeField] private Sprite seedSprite;
        [SerializeField] private Sprite sproutSprite;
        [SerializeField] private Sprite wheatSprite;

        public int X => x;
        public int Y => y;

        private void Awake()
        {
            EnsureVisuals();
        }

        public void Configure(
            int cellX,
            int cellY,
            SpriteRenderer soil,
            SpriteRenderer crop,
            SpriteRenderer accent)
        {
            x = cellX;
            y = cellY;
            soilRenderer = soil;
            cropRenderer = crop;
            accentRenderer = accent;
            EnsureVisuals();
        }

        public void Refresh(FarmCellState state, CropDefinition crop, long nowUtc)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureVisuals();
            soilRenderer.enabled = true;
            BoxCollider2D plotCollider = GetComponent<BoxCollider2D>();
            if (plotCollider != null)
            {
                plotCollider.enabled = true;
            }

            FarmCellStatus status = state.GetStatus(crop, nowUtc);
            Sprite stageSprite = GetStageSprite(status, state, nowUtc);
            bool usesTileImage = stageSprite != null;

            soilRenderer.sprite = stageSprite ?? squareSprite;
            soilRenderer.color = usesTileImage ? Color.white : status switch
            {
                FarmCellStatus.Locked => new Color(0.26f, 0.29f, 0.30f, 0.72f),
                FarmCellStatus.Empty => new Color(0.48f, 0.29f, 0.15f),
                FarmCellStatus.NeedsWater => new Color(0.60f, 0.38f, 0.18f),
                FarmCellStatus.Growing => new Color(0.38f, 0.24f, 0.12f),
                FarmCellStatus.Ready => new Color(0.45f, 0.28f, 0.12f),
                _ => Color.magenta
            };

            cropRenderer.enabled = !usesTileImage
                && (status is FarmCellStatus.NeedsWater or FarmCellStatus.Growing or FarmCellStatus.Ready);
            accentRenderer.enabled = status is FarmCellStatus.Locked or FarmCellStatus.NeedsWater;

            if (status == FarmCellStatus.Locked)
            {
                accentRenderer.color = new Color(0.12f, 0.14f, 0.15f);
                accentRenderer.transform.localScale = new Vector3(0.38f, 0.12f, 1f);
                accentRenderer.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            }
            else if (status == FarmCellStatus.NeedsWater)
            {
                accentRenderer.color = new Color(0.20f, 0.70f, 1f);
                accentRenderer.transform.localScale = new Vector3(0.15f, 0.28f, 1f);
                accentRenderer.transform.localPosition = new Vector3(0.38f, 0.36f, -0.2f);
            }

            if (!cropRenderer.enabled)
            {
                return;
            }

            float progress = GetGrowthProgress(state, nowUtc);

            cropRenderer.color = status == FarmCellStatus.Ready
                ? new Color(1f, 0.78f, 0.12f)
                : Color.Lerp(new Color(0.18f, 0.58f, 0.18f), new Color(0.65f, 0.82f, 0.18f), progress);
            float height = Mathf.Lerp(0.25f, 0.82f, progress);
            cropRenderer.transform.localScale = new Vector3(0.22f, height, 1f);
            cropRenderer.transform.localPosition = new Vector3(0f, -0.40f + height * 0.5f, -0.1f);
        }

        public void Hide()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureVisuals();
            soilRenderer.enabled = false;
            cropRenderer.enabled = false;
            accentRenderer.enabled = false;
            BoxCollider2D plotCollider = GetComponent<BoxCollider2D>();
            if (plotCollider != null)
            {
                plotCollider.enabled = false;
            }
        }

        public void SetWorldPosition(float worldX, float worldY)
        {
            transform.position = new Vector3(worldX, worldY, 0f);
        }

        public void ShowPlacementPreview(Vector2 worldPosition, bool canPlace)
        {
            SetWorldPosition(worldPosition.x, worldPosition.y);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureVisuals();
            soilRenderer.enabled = true;
            BoxCollider2D plotCollider = GetComponent<BoxCollider2D>();
            if (plotCollider != null)
            {
                plotCollider.enabled = true;
            }

            soilRenderer.sprite = soilSprite ?? squareSprite;
            soilRenderer.color = canPlace
                ? new Color(0.32f, 0.82f, 0.30f, 0.78f)
                : new Color(0.90f, 0.24f, 0.20f, 0.78f);
            cropRenderer.enabled = false;
            accentRenderer.enabled = true;
            accentRenderer.color = canPlace
                ? new Color(0.82f, 1f, 0.68f, 0.95f)
                : new Color(1f, 0.72f, 0.66f, 0.95f);
            accentRenderer.transform.localScale = new Vector3(0.58f, 0.58f, 1f);
            accentRenderer.transform.localPosition = new Vector3(0f, 0f, -0.2f);
        }

        private static float GetGrowthProgress(FarmCellState state, long nowUtc)
        {
            long duration = state.readyAtUtc - state.growthStartedAtUtc;
            if (duration <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(nowUtc - state.growthStartedAtUtc) / duration);
        }

        private Sprite GetStageSprite(FarmCellStatus status, FarmCellState state, long nowUtc)
        {
            if (status is FarmCellStatus.Locked or FarmCellStatus.Empty)
            {
                return soilSprite;
            }

            if (status == FarmCellStatus.Ready)
            {
                return wheatSprite;
            }

            return GetGrowthProgress(state, nowUtc) < 0.25f ? seedSprite : sproutSprite;
        }

        private void EnsureVisuals()
        {
            squareSprite ??= CreateSquareSprite();
            cachedSoilSprite ??= LoadFirstSprite("Image/tile_soil");
            cachedSeedSprite ??= LoadFirstSprite("Image/tile_soil_seed");
            cachedSproutSprite ??= LoadFirstSprite("Image/tile_soil_sprout");
            cachedWheatSprite ??= LoadFirstSprite("Image/tile_soil_wheat");

            soilSprite ??= cachedSoilSprite;
            seedSprite ??= cachedSeedSprite;
            sproutSprite ??= cachedSproutSprite;
            wheatSprite ??= cachedWheatSprite;

            soilRenderer ??= GetComponent<SpriteRenderer>();
            soilRenderer.sprite ??= soilSprite ?? squareSprite;
            BoxCollider2D plotCollider = GetComponent<BoxCollider2D>();
            if (soilSprite != null && plotCollider != null)
            {
                plotCollider.size = new Vector2(1.06f, 1.12f);
            }

            if (cropRenderer != null)
            {
                cropRenderer.sprite = squareSprite;
            }

            if (accentRenderer != null)
            {
                accentRenderer.sprite = squareSprite;
            }
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
