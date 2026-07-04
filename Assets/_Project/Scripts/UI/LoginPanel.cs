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
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text primaryActionLabel;
        [SerializeField] private Text registerToggleLabel;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject logoutButton;

        private FarmApiClient api;
        private Action<string> messageSink;
        private Action<string> sessionStarted;
        private Action sessionEnded;
        private bool requestInFlight;
        private AuthMode authMode = AuthMode.Login;

        private static readonly Color InfoColor = new(0.34f, 0.25f, 0.12f);
        private static readonly Color ErrorColor = new(0.74f, 0.18f, 0.12f);
        private static readonly Color SuccessColor = new(0.18f, 0.42f, 0.18f);

        private enum AuthMode
        {
            Login,
            Register
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        public static LoginPanel Create(Transform parent)
        {
            EnsureEventSystem();

            // 전체 화면 컨테이너: 배경 아트가 게임 화면을 가리고 뒤쪽 클릭을 차단한다.
            GameObject screenObject = CreateImage("Login Screen", parent, Color.clear);
            SetStretch((RectTransform)screenObject.transform, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
            screenObject.GetComponent<Image>().raycastTarget = true;

            GameObject backdrop = CreateImage("Backdrop", screenObject.transform, Color.white);
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.sprite = LoadFirstSprite("Image/bg_login");
            backdropImage.raycastTarget = false;
            SetStretch(backdrop.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);

            GameObject panelObject = CreateImage("Login Panel", screenObject.transform, Color.white);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetAnchored(panelRect, new Vector2(0.5f, 0.5f), new Vector2(474f, 322f), new Vector2(0f, 40f));
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = LoadFirstSprite("Image/panel_login");
            panelImage.raycastTarget = true;

            Text title = CreateText("Title", panelObject.transform, "Cloud Farm 로그인", 24, TextAnchor.MiddleCenter, new Color(0.24f, 0.19f, 0.10f));
            title.fontStyle = FontStyle.Bold;
            SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(400f, 40f), new Vector2(0f, -8f), new Vector2(0.5f, 1f));

            InputField username = CreateInputField(panelObject.transform, "Username Field", "아이디 (3~50자)", false, new Vector2(-1f, 66f));
            InputField password = CreateInputField(panelObject.transform, "Password Field", "비밀번호 (4자 이상)", true, new Vector2(-1f, -12f));

            Button loginButton = CreateButton(panelObject.transform, "Login Button", new Vector2(-1f, -108f), new Vector2(344f, 60f), LoadFirstSprite("Image/btn_pixel_green"), Color.white);
            Text loginLabel = CreateText("Label", loginButton.transform, "로그인", 24, TextAnchor.MiddleCenter, new Color(0.07f, 0.12f, 0.04f));
            loginLabel.fontStyle = FontStyle.Bold;
            SetStretch(loginLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 0f, -4f, 0f);

            Button registerButton = CreateButton(parent, "Register Button", new Vector2(-96f, -145f), new Vector2(176f, 46f), LoadFirstSprite("Image/btn_pixel_green"), Color.white);
            registerButton.transform.SetParent(panelObject.transform, false);
            ((RectTransform)registerButton.transform).anchoredPosition = new Vector2(-96f, -185f);
            Text registerLabel = CreateText("Label", registerButton.transform, "회원가입", 19, TextAnchor.MiddleCenter, new Color(0.07f, 0.12f, 0.04f));
            registerLabel.fontStyle = FontStyle.Bold;
            SetStretch(registerLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 6f, -4f, -4f);

            Button guestButton = CreateButton(parent, "Guest Button", new Vector2(96f, -145f), new Vector2(176f, 46f), LoadFirstSprite("Image/btn_pixel_tan"), Color.white);
            guestButton.transform.SetParent(panelObject.transform, false);
            ((RectTransform)guestButton.transform).anchoredPosition = new Vector2(96f, -185f);
            Text guestLabel = CreateText("Label", guestButton.transform, "게스트로 시작", 19, TextAnchor.MiddleCenter, new Color(0.07f, 0.12f, 0.04f));
            guestLabel.fontStyle = FontStyle.Bold;
            SetStretch(guestLabel.rectTransform, 0f, 0f, 1f, 1f, 4f, 6f, -4f, -4f);

            Text status = CreateText("Status", panelObject.transform, string.Empty, 15, TextAnchor.MiddleCenter, ErrorColor);
            status.fontStyle = FontStyle.Bold;
            SetAnchored(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420f, 24f), new Vector2(0f, -63f));

            // 로그인 후 표시되는 로그아웃 버튼 (상단바 오른쪽 아래, 문 아이콘만 사용)
            Button logout = CreateButton(parent, "Logout Button", Vector2.zero, new Vector2(108f, 46f), LoadFirstSprite("Image/btn_login"), Color.white);
            RectTransform logoutRect = (RectTransform)logout.transform;
            logoutRect.anchorMin = new Vector2(1f, 1f);
            logoutRect.anchorMax = new Vector2(1f, 1f);
            logoutRect.pivot = new Vector2(1f, 1f);
            logoutRect.anchoredPosition = new Vector2(-14f, -88f);
            logout.gameObject.SetActive(false);

            LoginPanel loginPanel = parent.gameObject.AddComponent<LoginPanel>();
            loginPanel.panel = screenObject;
            loginPanel.usernameField = username;
            loginPanel.passwordField = password;
            loginPanel.titleLabel = title;
            loginPanel.primaryActionLabel = loginLabel;
            loginPanel.registerToggleLabel = registerLabel;
            loginPanel.statusText = status;
            loginPanel.logoutButton = logout.gameObject;
            loginPanel.SetMode(AuthMode.Login, string.Empty);

            loginButton.onClick.AddListener(loginPanel.HandlePrimaryActionClicked);
            registerButton.onClick.AddListener(loginPanel.HandleRegisterToggleClicked);
            guestButton.onClick.AddListener(loginPanel.HandleGuestClicked);
            logout.onClick.AddListener(loginPanel.HandleLogoutClicked);

            return loginPanel;
        }

        public void Initialize(
            Action<string> hudMessageSink,
            Action<string> onSessionStarted = null,
            Action onSessionEnded = null)
        {
            messageSink = hudMessageSink;
            sessionStarted = onSessionStarted;
            sessionEnded = onSessionEnded;
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
                        sessionStarted?.Invoke(api.Username);
                        messageSink?.Invoke($"{api.Username}님, 다시 오셨네요!");
                    }
                    else
                    {
                        sessionEnded?.Invoke();
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

        private void SetMode(AuthMode mode, string status, Color? statusColor = null)
        {
            authMode = mode;

            if (titleLabel != null)
            {
                titleLabel.text = authMode == AuthMode.Register
                    ? "Cloud Farm 회원가입"
                    : "Cloud Farm 로그인";
            }

            if (primaryActionLabel != null)
            {
                primaryActionLabel.text = authMode == AuthMode.Register ? "회원가입" : "로그인";
            }

            if (registerToggleLabel != null)
            {
                registerToggleLabel.text = authMode == AuthMode.Register ? "로그인으로" : "회원가입";
            }

            SetStatus(status, statusColor ?? InfoColor);
        }

        private void HandlePrimaryActionClicked()
        {
            if (authMode == AuthMode.Register)
            {
                HandleRegisterClicked();
                return;
            }

            HandleLoginClicked();
        }

        private void HandleRegisterToggleClicked()
        {
            if (authMode == AuthMode.Register)
            {
                SetMode(AuthMode.Login, "아이디와 비밀번호를 입력하고 로그인하세요.");
                return;
            }

            SetMode(AuthMode.Register, "아이디와 비밀번호를 입력하고 회원가입을 누르세요.");
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
                    SetStatus(message, ErrorColor);
                    return;
                }

                Hide();
                logoutButton.SetActive(true);
                sessionStarted?.Invoke(api.Username);
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
                    SetStatus(message, ErrorColor);
                    return;
                }

                // 가입 성공을 패널 안에 보여주고 로그인 모드로 돌아간다.
                requestInFlight = false;
                SetMode(AuthMode.Login, string.Empty);
                SetStatus("회원가입이 완료되었습니다. 로그인해 주세요.", SuccessColor);
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
                sessionEnded?.Invoke();
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
                SetStatus("아이디와 비밀번호를 모두 입력해 주세요.", ErrorColor);
                return false;
            }

            return true;
        }

        private void SetStatus(string message, Color? color = null)
        {
            if (statusText != null)
            {
                statusText.color = color ?? InfoColor;
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
            SetStretch(placeholder.rectTransform, 0f, 0f, 1f, 1f, 16f, 11f, -16f, -1f);

            Text text = CreateText("Text", fieldObject.transform, string.Empty, 19, TextAnchor.MiddleLeft, new Color(0.13f, 0.10f, 0.05f));
            text.supportRichText = false;
            SetStretch(text.rectTransform, 0f, 0f, 1f, 1f, 16f, 11f, -16f, -1f);

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
                // 테두리가 설정된 스프라이트는 9-슬라이스로 늘려 모서리를 보존한다.
                if (sprite.border != Vector4.zero)
                {
                    image.type = Image.Type.Sliced;
                }
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
