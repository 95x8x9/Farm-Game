using FarmGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmGame.Farm
{
    public sealed class FarmInputController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private FarmGameManager gameManager;

        public void Configure(Camera cameraReference, FarmGameManager manager)
        {
            worldCamera = cameraReference;
            gameManager = manager;
        }

        private void Update()
        {
            if (gameManager == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                gameManager.ResetProgress();
                return;
            }

            if (gameManager.IsMinigameActive || !TryGetPointerPress(out Vector2 screenPosition))
            {
                return;
            }

            worldCamera ??= Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
            Collider2D hit = Physics2D.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
            if (hit != null && hit.TryGetComponent(out FarmCellView cell))
            {
                gameManager.Interact(cell);
            }
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
    }
}
