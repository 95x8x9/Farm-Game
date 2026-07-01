using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public sealed class WateringMinigame : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform marker;
        [SerializeField] private Text resultText;
        [SerializeField, Range(0.05f, 0.45f)] private float successHalfWidth = 0.14f;
        [SerializeField, Min(0.1f)] private float speed = 0.85f;

        private Action<bool> onCompleted;
        private float normalizedPosition;
        private float inputBlockUntil;

        public bool IsPlaying { get; private set; }

        public void Configure(GameObject panelObject, RectTransform markerTransform, Text result)
        {
            panel = panelObject;
            marker = markerTransform;
            resultText = result;
            panel.SetActive(false);
        }

        public void Begin(Action<bool> completedCallback)
        {
            onCompleted = completedCallback;
            normalizedPosition = 0f;
            inputBlockUntil = Time.unscaledTime + 0.18f;
            IsPlaying = true;
            panel.SetActive(true);
            resultText.text = "포인터가 초록 영역에 들어왔을 때 클릭 또는 Space";
            UpdateMarker();
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            normalizedPosition = Mathf.PingPong(Time.unscaledTime * speed, 1f);
            UpdateMarker();

            if (Time.unscaledTime < inputBlockUntil || !WasConfirmPressed())
            {
                return;
            }

            bool succeeded = Mathf.Abs(normalizedPosition - 0.5f) <= successHalfWidth;
            if (!succeeded)
            {
                Complete(false);
                return;
            }

            Complete(true);
        }

        private void Complete(bool succeeded)
        {
            IsPlaying = false;
            panel.SetActive(false);
            Action<bool> callback = onCompleted;
            onCompleted = null;
            callback?.Invoke(succeeded);
        }

        private void UpdateMarker()
        {
            if (marker == null)
            {
                return;
            }

            RectTransform track = marker.parent as RectTransform;
            float width = track != null ? track.rect.width : 500f;
            marker.anchoredPosition = new Vector2((normalizedPosition - 0.5f) * width, marker.anchoredPosition.y);
        }

        private static bool WasConfirmPressed()
        {
            bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool keyboard = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool touch = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            return mouse || keyboard || touch;
        }
    }
}
