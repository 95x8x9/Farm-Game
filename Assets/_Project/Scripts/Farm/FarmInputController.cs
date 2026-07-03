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

            bool cancelWithKeyboard = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool cancelWithMouse = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            if (gameManager.IsPlacingPlot && (cancelWithKeyboard || cancelWithMouse))
            {
                gameManager.CancelPlotPlacement();
                return;
            }

            if (gameManager.IsPlantingCrop && (cancelWithKeyboard || cancelWithMouse))
            {
                gameManager.CancelCropPlanting();
                return;
            }

            if (gameManager.IsInputBlocked)
            {
                return;
            }

            worldCamera ??= Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            if (gameManager.IsPlacingPlot)
            {
                if (TryGetPointerPosition(out Vector2 previewScreenPosition))
                {
                    Vector3 previewWorldPosition = worldCamera.ScreenToWorldPoint(previewScreenPosition);
                    gameManager.UpdatePlotPlacementPreview(new Vector2(previewWorldPosition.x, previewWorldPosition.y));
                }

                if (TryGetPointerPress(out Vector2 placementScreenPosition))
                {
                    Vector3 placementWorldPosition = worldCamera.ScreenToWorldPoint(placementScreenPosition);
                    gameManager.TryPlacePlotAt(new Vector2(placementWorldPosition.x, placementWorldPosition.y));
                }

                return;
            }

            if (!TryGetPointerPress(out Vector2 screenPosition))
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

        private static bool TryGetPointerPosition(out Vector2 position)
        {
            if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }
    }
}
