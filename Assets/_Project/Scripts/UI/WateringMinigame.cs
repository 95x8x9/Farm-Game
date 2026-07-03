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
        [SerializeField, Min(0f)] private float markerTravelPadding = 28f;

        private Action<bool> onCompleted;
        private float normalizedPosition;
        private float successCenterNormalized = 0.5f;
        private float inputBlockUntil;
        private RectTransform successZone;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            ApplyProgressBarVisuals();
        }

        public void Configure(GameObject panelObject, RectTransform markerTransform, Text result)
        {
            panel = panelObject;
            marker = markerTransform;
            resultText = result;
            ApplyProgressBarVisuals();
            panel.SetActive(false);
        }

        public void Begin(Action<bool> completedCallback)
        {
            onCompleted = completedCallback;
            normalizedPosition = 0f;
            inputBlockUntil = Time.unscaledTime + 0.18f;
            IsPlaying = true;
            panel.SetActive(true);
            RandomizeSuccessZone();
            resultText.text = "초록 영역에 들어왔을 때 클릭 또는 Space";
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

            bool succeeded = Mathf.Abs(normalizedPosition - successCenterNormalized) <= successHalfWidth;
            if (!succeeded)
            {
                Complete(false);
                return;
            }

            Complete(true);
        }

        private void ApplyProgressBarVisuals()
        {
            if (panel == null || marker == null)
            {
                return;
            }

            if (panel.TryGetComponent(out Image panelImage))
            {
                panelImage.color = new Color(0.12f, 0.09f, 0.06f, 0.97f);
            }

            Transform headingTransform = panel.transform.Find("Heading");
            if (headingTransform != null && headingTransform.TryGetComponent(out Text heading))
            {
                heading.text = "물주기 타이밍";
                heading.color = new Color(0.68f, 0.90f, 1f);
            }

            RectTransform track = marker.parent as RectTransform;
            if (track == null || !track.TryGetComponent(out Image trackImage))
            {
                return;
            }

            track.sizeDelta = new Vector2(520f, 54f);
            ConfigureImage(trackImage, CreateRuntimeSprite("ProgressBar/progressbar-track"));

            Transform successTransform = track.Find("Success Zone");
            if (successTransform != null && successTransform.TryGetComponent(out Image successImage))
            {
                successZone = (RectTransform)successTransform;
                successZone.sizeDelta = new Vector2(146f, 54f);
                ConfigureImage(successImage, CreateRuntimeSprite("ProgressBar/progressbar-fill"));
            }

            Transform frameTransform = track.Find("Frame");
            if (frameTransform == null)
            {
                GameObject frameObject = new("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameObject.transform.SetParent(track, false);
                frameTransform = frameObject.transform;
            }

            RectTransform frameRect = (RectTransform)frameTransform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = Vector2.zero;
            frameRect.sizeDelta = new Vector2(548f, 90f);
            ConfigureImage(frameTransform.GetComponent<Image>(), CreateRuntimeSprite("ProgressBar/progressbar-frame"));
            frameTransform.SetSiblingIndex(marker.GetSiblingIndex());

            marker.sizeDelta = new Vector2(25f, 94f);
            ConfigureImage(marker.GetComponent<Image>(), CreateRuntimeSprite("ProgressBar/progressbar-handle"), true);
            marker.SetAsLastSibling();
        }

        private void RandomizeSuccessZone()
        {
            if (successZone == null || marker == null)
            {
                return;
            }

            RectTransform track = marker.parent as RectTransform;
            float trackWidth = track != null ? track.rect.width : 500f;
            float travelWidth = Mathf.Max(0f, trackWidth - markerTravelPadding * 2f);

            successCenterNormalized = UnityEngine.Random.Range(successHalfWidth, 1f - successHalfWidth);
            successZone.anchoredPosition = new Vector2(
                (successCenterNormalized - 0.5f) * travelWidth,
                successZone.anchoredPosition.y);
            successZone.sizeDelta = new Vector2(travelWidth * successHalfWidth * 2f, successZone.sizeDelta.y);
        }

        private static Sprite CreateRuntimeSprite(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            return texture == null
                ? null
                : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void ConfigureImage(Image image, Sprite sprite, bool preserveAspect = false)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
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
            float travelWidth = Mathf.Max(0f, width - markerTravelPadding * 2f);
            marker.anchoredPosition = new Vector2((normalizedPosition - 0.5f) * travelWidth, marker.anchoredPosition.y);
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
