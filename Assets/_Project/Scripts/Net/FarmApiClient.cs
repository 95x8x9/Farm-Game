using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FarmGame.Net
{
    /// <summary>
    /// Server/src의 Express API(로그인·회원가입·로그아웃·내 정보)와 통신하는 클라이언트.
    /// WebGL에서 쿠키 대신 Authorization: Bearer 헤더를 사용한다.
    /// </summary>
    public sealed class FarmApiClient : MonoBehaviour
    {
        private const string DefaultBaseUrl = "https://educs242.ai-startpoint.com";
        private const string BaseUrlPrefKey = "farm_api_base_url";
        private const string TokenPrefKey = "farm_api_token";
        private const string UsernamePrefKey = "farm_api_username";
        private const int TimeoutSeconds = 10;

        private static FarmApiClient instance;

        public string Token { get; private set; }
        public string Username { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public static string BaseUrl => PlayerPrefs.GetString(BaseUrlPrefKey, DefaultBaseUrl).TrimEnd('/');

        public static FarmApiClient Ensure()
        {
            if (instance == null)
            {
                GameObject clientObject = new("FarmApiClient");
                DontDestroyOnLoad(clientObject);
                instance = clientObject.AddComponent<FarmApiClient>();
                instance.Token = PlayerPrefs.GetString(TokenPrefKey, string.Empty);
                instance.Username = PlayerPrefs.GetString(UsernamePrefKey, string.Empty);
            }

            return instance;
        }

        public void Login(string username, string password, Action<bool, string> onCompleted)
        {
            string json = JsonUtility.ToJson(new CredentialsRequest { username = username, password = password });
            StartCoroutine(SendJson("POST", "/api/auth/login", json, false, (success, body) =>
            {
                if (!success)
                {
                    onCompleted?.Invoke(false, ExtractErrorMessage(body, "로그인에 실패했습니다."));
                    return;
                }

                LoginResponse response = FromJsonSafe<LoginResponse>(body);
                if (response == null || string.IsNullOrEmpty(response.token))
                {
                    onCompleted?.Invoke(false, "서버 응답을 해석할 수 없습니다.");
                    return;
                }

                SetSession(response.token, response.user != null ? response.user.username : username);
                onCompleted?.Invoke(true, "로그인 성공");
            }));
        }

        public void Register(string username, string password, Action<bool, string> onCompleted)
        {
            string json = JsonUtility.ToJson(new CredentialsRequest { username = username, password = password });
            StartCoroutine(SendJson("POST", "/api/auth/register", json, false, (success, body) =>
            {
                if (!success)
                {
                    onCompleted?.Invoke(false, ExtractErrorMessage(body, "회원가입에 실패했습니다."));
                    return;
                }

                onCompleted?.Invoke(true, "회원가입이 완료되었습니다.");
            }));
        }

        public void Logout(Action<bool, string> onCompleted)
        {
            // JWT는 서버가 상태를 갖지 않으므로 로컬 세션 제거가 핵심이고, 서버 호출은 쿠키 정리용이다.
            StartCoroutine(SendJson("POST", "/api/auth/logout", "{}", true, (_, _) =>
            {
                ClearSession();
                onCompleted?.Invoke(true, "로그아웃 되었습니다.");
            }));
        }

        /// <summary>저장된 토큰이 아직 유효한지 /api/me로 확인한다.</summary>
        public void ValidateSession(Action<bool, string> onCompleted)
        {
            if (!IsLoggedIn)
            {
                onCompleted?.Invoke(false, "저장된 세션이 없습니다.");
                return;
            }

            StartCoroutine(SendJson("GET", "/api/me", null, true, (success, body) =>
            {
                if (!success)
                {
                    ClearSession();
                    onCompleted?.Invoke(false, "세션이 만료되었습니다. 다시 로그인해주세요.");
                    return;
                }

                MeResponse response = FromJsonSafe<MeResponse>(body);
                if (response?.user != null && !string.IsNullOrEmpty(response.user.username))
                {
                    Username = response.user.username;
                }

                onCompleted?.Invoke(true, "세션이 유효합니다.");
            }));
        }

        private void SetSession(string token, string username)
        {
            Token = token;
            Username = username ?? string.Empty;
            PlayerPrefs.SetString(TokenPrefKey, Token);
            PlayerPrefs.SetString(UsernamePrefKey, Username);
            PlayerPrefs.Save();
        }

        private void ClearSession()
        {
            Token = string.Empty;
            Username = string.Empty;
            PlayerPrefs.DeleteKey(TokenPrefKey);
            PlayerPrefs.DeleteKey(UsernamePrefKey);
            PlayerPrefs.Save();
        }

        private IEnumerator SendJson(
            string method,
            string path,
            string jsonBody,
            bool withAuth,
            Action<bool, string> onCompleted)
        {
            using UnityWebRequest request = new(BaseUrl + path, method);
            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = TimeoutSeconds;
            if (withAuth && IsLoggedIn)
            {
                request.SetRequestHeader("Authorization", "Bearer " + Token);
            }

            yield return request.SendWebRequest();

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            bool success = request.result == UnityWebRequest.Result.Success;
            if (!success && request.result == UnityWebRequest.Result.ConnectionError)
            {
                body = "{\"message\":\"서버에 연결할 수 없습니다. 네트워크를 확인해주세요.\"}";
            }

            onCompleted?.Invoke(success, body);
        }

        private static string ExtractErrorMessage(string body, string fallback)
        {
            ErrorResponse error = FromJsonSafe<ErrorResponse>(body);
            return error != null && !string.IsNullOrEmpty(error.message) ? error.message : fallback;
        }

        private static T FromJsonSafe<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        [Serializable]
        private sealed class CredentialsRequest
        {
            public string username;
            public string password;
        }

        [Serializable]
        private sealed class UserDto
        {
            public int id;
            public string username;
        }

        [Serializable]
        private sealed class LoginResponse
        {
            public string message;
            public string token;
            public UserDto user;
        }

        [Serializable]
        private sealed class MeResponse
        {
            public UserDto user;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string error;
            public string message;
        }
    }
}
