using UnityEngine;

namespace FarmGame.Farm
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RuntimeSquareVisual : MonoBehaviour
    {
        private static Sprite squareSprite;

        private void Awake()
        {
            squareSprite ??= CreateSquareSprite();
            GetComponent<SpriteRenderer>().sprite = squareSprite;
        }

        private static Sprite CreateSquareSprite()
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Background Square",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
