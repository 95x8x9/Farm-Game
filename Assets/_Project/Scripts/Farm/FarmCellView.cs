using FarmGame.Data;
using UnityEngine;

namespace FarmGame.Farm
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class FarmCellView : MonoBehaviour
    {
        private static Sprite squareSprite;

        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private SpriteRenderer soilRenderer;
        [SerializeField] private SpriteRenderer cropRenderer;
        [SerializeField] private SpriteRenderer accentRenderer;

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
            EnsureVisuals();
            FarmCellStatus status = state.GetStatus(crop, nowUtc);

            soilRenderer.color = status switch
            {
                FarmCellStatus.Locked => new Color(0.26f, 0.29f, 0.30f),
                FarmCellStatus.Empty => new Color(0.48f, 0.29f, 0.15f),
                FarmCellStatus.NeedsWater => new Color(0.60f, 0.38f, 0.18f),
                FarmCellStatus.Growing => new Color(0.38f, 0.24f, 0.12f),
                FarmCellStatus.Ready => new Color(0.45f, 0.28f, 0.12f),
                _ => Color.magenta
            };

            cropRenderer.enabled = status is FarmCellStatus.NeedsWater or FarmCellStatus.Growing or FarmCellStatus.Ready;
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

        private static float GetGrowthProgress(FarmCellState state, long nowUtc)
        {
            long duration = state.readyAtUtc - state.growthStartedAtUtc;
            if (duration <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(nowUtc - state.growthStartedAtUtc) / duration);
        }

        private void EnsureVisuals()
        {
            squareSprite ??= CreateSquareSprite();
            soilRenderer ??= GetComponent<SpriteRenderer>();
            soilRenderer.sprite = squareSprite;

            if (cropRenderer != null)
            {
                cropRenderer.sprite = squareSprite;
            }

            if (accentRenderer != null)
            {
                accentRenderer.sprite = squareSprite;
            }
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
