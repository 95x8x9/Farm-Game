using System;
using FarmGame.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// panel_form 이미지를 사용한 로그인/회원가입 화면.
    /// 로그인 성공 시 우측 상단에 로그아웃 버튼(btn_login 아이콘)이 나타난다.
    /// </summary>
    public sealed class LoginPanel : MonoBehaviour
    {
        private const string KoreanFontPath = "Fonts/NotoSansKR-VF";

        [SerializeField] private GameObject panel;
        [SerializeField] private InputField usernameField;
        [SerializeField] private InputField passwordField;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject logoutButton;
        [SerializeField] private Text logoutLabel;

        private FarmApiClient api;
        private Action<string> messageSink;
        private bool requestInFlight;

        public bool IsVisible => panel != null && panel.activeSelf;

        public static LoginPanel Create(Transform parent)
        {
            EnsureEventSystem();

            GameObject panelObject = CreateImage("Login Panel", parent, Color.white);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetAnchored(panelRect, new Vector2(0.5f, 0.5f), new Vector2(474f, 322f), new Vector2(0f, 40f));
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = LoadFirstSprite("Image/panel_form");
            panelImage.raycastTarget = true;

            Text title = CreateText("Title", panelObject.transform, "Cloud Farm 로그인", 24, TextAnchor.MiddleCenter, new Color(0.24f, 0.19f, 0.10f));
            title.fontStyle = FontStyle.Bold;
            SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(400f, 40f), new Vector2(0f, -8f), new Vector2(0.5f, 1f));

            InputField username = CreateInputField(panelObject.transform, "Username Field", "아이디 (3~50자)", false, new Vector2(-1f, 66f));
            InputField password = CreateInputField(panelObject.transform, "Password Field", "비밀번호 (4자 이상)", true, new Vector2(-1f, -12f));

            // panel_form에 그려진 초록 버튼 위에 투명 버튼을 얹는다.
            Button loginButton = CreateButton(panelObject.transform, "Login Button", new Vector2(-1f, -97f), new Vector2(344f, 68f), null, Color.clear);
            Text loginLabel = CreateText("Label", loginButton.transform, "로그인", 24, TextAnchor.MiddleCenter, new Color(0.07f, 0.12f, 0.04f));
            loginLabel.fontStyle = FontStyle.Bold;
            SetStretch(loginLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 8f, -4f, -4f);

            Sprite longButtonSprite = LoadFirstSprite("Image/btn_green_long");
            Button registerButton = CreateButton(parent, "Register Button", new Vector2(-96f, -145f), new Vector2(176f, 46f), longButtonSprite, Color.white);
            registerButton.transform.SetParent(panelObject.transform, false);
            ((RectTransform)registerButton.transform).anchoredPosition = new Vector2(-96f, -185f);
            Text registerLabel = CreateText("Label", registerButton.transform, "회원가입", 19, TextAnchor.MiddleCenter, new Color(0.07f, 0.12f, 0.04f));
            registerLabel.fontStyle = FontStyle.Bold;
            SetStretch(registerLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 6f, -4f, -4f);

            Button guestButton = CreateButton(parent, "Guest Button", new Vector2(96f, -145f), new Vector2(176f, 46f), longButtonSprite, Color.white);
            guestButton.transform.SetParent(panelObject.transform, false);
            ((RectTransform)guestButton.transform).anchoredPosition = new Vector2(96f, -185f);
            Text guestLabel = CreateText("Label", guestButton.transform, "게스트로 시작", 19, TextAnchor.MiddleCenter, new Color(0.07f, 0.12f, 0.04f));
            guestLabel.fontStyle = FontStyle.Bold;
            SetStretch(guestLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 6f, -4f, -4f);

            Text status = CreateText("Status", panelObject.transform, string.Empty, 18, TextAnchor.MiddleCenter, new Color(0.55f, 0.15f, 0.10f));
            status.fontStyle = FontStyle.Bold;
            SetAnchored(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(460f, 34f), new Vector2(0f, -66f), new Vector2(0.5f, 1f));

            // 로그인 후 표시되는 로그아웃 버튼 (상단바 오른쪽 아래)
            Button logout = CreateButton(parent, "Logout Button", Vector2.zero, new Vector2(118f, 50f), LoadFirstSprite("Image/btn_login"), Color.white);
            RectTransform logoutRect = (RectTransform)logout.transform;
            logoutRect.anchorMin = new Vector2(1f, 1f);
            logoutRect.anchorMax = new Vector2(1f, 1f);
            logoutRect.pivot = new Vector2(1f, 1f);
            logoutRect.anchoredPosition = new Vector2(-14f, -88f);
            Text logoutText = CreateText("Label", logout.transform, "로그아웃", 15, TextAnchor.MiddleCenter, new Color(0.05f, 0.10f, 0.03f));
            logoutText.fontStyle = FontStyle.Bold;
            SetAnchored(logoutText.rectTransform, new Vector2(0.5f, 0f), new Vector2(118f, 22f), new Vector2(0f, 2f), new Vector2(0.5f, 0f));
            logout.gameObject.SetActive(false);

            LoginPanel loginPanel = parent.gameObject.AddComponent<LoginPanel>();
            loginPanel.panel = panelObject;
            loginPanel.usernameField = username;
            loginPanel.passwordField = password;
            loginPanel.statusText = status;
            loginPanel.logoutButton = logout.gameObject;
            loginPanel.logoutLabel = logoutText;

            loginButton.onClick.AddListener(loginPanel.HandleLoginClicked);
            registerButton.onClick.AddListener(loginPanel.HandleRegisterClicked);
            guestButton.onClick.AddListener(loginPanel.HandleGuestClicked);
            logout.onClick.AddListener(loginPanel.HandleLogoutClicked);

            return loginPanel;
        }

        public void Initialize(Action<string> hudMessageSink)
        {
            messageSink = hudMessageSink;
            api = FarmApiClient.Ensure();

            if (api.IsLoggedIn)
            {
                // 저장된 세션이 있으면 유효성만 확인하고 로그인 화면은 건너뛴다.
                panel.SetActive(false);
                logoutButton.SetActive(true);
                api.ValidateSession((valid, message) =>
                {
                    if (valid)
                    {
                        messageSink?.Invoke($"{api.Username}님, 다시 오셨네요!");
                    }
                    else
                    {
                        logoutButton.SetActive(false);
                        Show(message);
                    }
                });
                return;
            }

            Show("로그인하거나 게스트로 시작하세요.");
        }

        public void Show(string status)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            SetStatus(status);
        }

        private void Hide()
        {
            panel.SetActive(false);
        }

        private void HandleLoginClicked()
        {
            if (requestInFlight || !TryReadCredentials(out string username, out string password))
            {
                return;
            }

            requestInFlight = true;
            SetStatus("로그인 중...");
            api.Login(username, password, (success, message) =>
            {
                requestInFlight = false;
                if (!success)
                {
                    SetStatus(message);
                    return;
                }

                Hide();
                logoutButton.SetActive(true);
                messageSink?.Invoke($"{api.Username}님, 환영합니다!");
            });
        }

        private void HandleRegisterClicked()
        {
            if (requestInFlight || !TryReadCredentials(out string username, out string password))
            {
                return;
            }

            requestInFlight = true;
            SetStatus("회원가입 중...");
            api.Register(username, password, (success, message) =>
            {
                if (!success)
                {
                    requestInFlight = false;
                    SetStatus(message);
                    return;
                }

                // 가입 성공 시 같은 정보로 바로 로그인한다.
                api.Login(username, password, (loginSuccess, loginMessage) =>
                {
                    requestInFlight = false;
                    if (!loginSuccess)
                    {
                        SetStatus(loginMessage);
                        return;
                    }

                    Hide();
                    logoutButton.SetActive(true);
                    messageSink?.Invoke($"{api.Username}님, 가입을 환영합니다!");
                });
            });
        }

        private void HandleGuestClicked()
        {
            Hide();
            messageSink?.Invoke("게스트로 시작합니다. 진행 상황은 이 브라우저에만 저장됩니다.");
        }

        private void HandleLogoutClicked()
        {
            if (requestInFlight)
            {
                return;
            }

            requestInFlight = true;
            api.Logout((_, message) =>
            {
                requestInFlight = false;
                logoutButton.SetActive(false);
                messageSink?.Invoke(message);
                Show("로그아웃 되었습니다. 다시 로그인하거나 게스트로 시작하세요.");
            });
        }

        private bool TryReadCredentials(out string username, out string password)
        {
            username = usernameField != null ? usernameField.text.Trim() : string.Empty;
            password = passwordField != null ? passwordField.text : string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetStatus("아이디와 비밀번호를 모두 입력해주세요.");
                return false;
            }

            return true;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(null, false);
        }

        private static InputField CreateInputField(
            Transform parent,
            string name,
            string placeholderText,
            bool isPassword,
            Vector2 position)
        {
            GameObject fieldObject = CreateImage(name, parent, new Color(1f, 1f, 1f, 0f));
            RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
            SetAnchored(fieldRect, new Vector2(0.5f, 0.5f), new Vector2(356f, 56f), position);
            Image background = fieldObject.GetComponent<Image>();
            background.raycastTarget = true;

            Text placeholder = CreateText("Placeholder", fieldObject.transform, placeholderText, 19, TextAnchor.MiddleLeft, new Color(0.55f, 0.48f, 0.38f));
            SetStretch(placeholder.rectTransform, 0f, 0f, 1f, 1f, 16f, 6f, -16f, -6f);

            Text text = CreateText("Text", fieldObject.transform, string.Empty, 19, TextAnchor.MiddleLeft, new Color(0.13f, 0.10f, 0.05f));
            text.supportRichText = false;
            SetStretch(text.rectTransform, 0f, 0f, 1f, 1f, 16f, 6f, -16f, -6f);

            InputField inputField = fieldObject.AddComponent<InputField>();
            inputField.targetGraphic = background;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 50;
            inputField.lineType = InputField.LineType.SingleLine;
            if (isPassword)
            {
                inputField.contentType = InputField.ContentType.Password;
            }

            return inputField;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            GameObject buttonObject = CreateImage(name, parent, color);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetAnchored(rect, new Vector2(0.5f, 0.5f), size, position);
            Image image = buttonObject.GetComponent<Image>();
            image.raycastTarget = true;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = false;
            }

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Sprite LoadFirstSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            return sprites.Length > 0 ? sprites[0] : Resources.Load<Sprite>(resourcePath);
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            return gameObject;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.Load<Font>(KoreanFontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetAnchored(
            RectTransform rect,
            Vector2 anchor,
            Vector2 size,
            Vector2 position,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetStretch(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
