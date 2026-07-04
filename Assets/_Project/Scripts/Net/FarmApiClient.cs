using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FarmGame.Net
{
    /// <summary>GET /api/farm 응답 (서버가 계산한 계정별 농장 상태).</summary>
    [Serializable]
    public sealed class FarmSnapshot
    {
        public int money;
        public int level;
        public int wheat_harvest_count;
        public bool batch_unlocked;
        public List<FarmPlotDto> plots;
    }

    [Serializable]
    public sealed class FarmPlotDto
    {
        public int plot_index;
        public int unlocked;
        public string crop_type;
        public string planted_at;
        public int water_count;
        public string ready_at;
        public string state;
    }

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

        // ===== 농장 API (게임 액션을 서버 DB에 반영) =====

        public void GetFarm(Action<bool, FarmSnapshot, string> onCompleted)
        {
            StartCoroutine(SendJson("GET", "/api/farm", null, true, (success, body) =>
            {
                if (!success)
                {
                    onCompleted?.Invoke(false, null, ExtractErrorMessage(body, "농장 정보를 불러오지 못했습니다."));
                    return;
                }

                FarmSnapshot snapshot = FromJsonSafe<FarmSnapshot>(body);
                if (snapshot == null)
                {
                    onCompleted?.Invoke(false, null, "농장 응답을 해석할 수 없습니다.");
                    return;
                }

                onCompleted?.Invoke(true, snapshot, "농장 정보를 불러왔습니다.");
            }));
        }

        public void BuyPlot(int plotIndex, Action<bool, string> onCompleted)
        {
            string json = JsonUtility.ToJson(new PlotRequest { plotIndex = plotIndex });
            StartCoroutine(SendJson("POST", "/api/plots/buy", json, true,
                (success, body) => onCompleted?.Invoke(success, success ? "ok" : ExtractErrorMessage(body, "밭 구매를 서버에 반영하지 못했습니다."))));
        }

        /// <summary>서버 규칙상 씨앗을 먼저 인벤토리에 구매한 뒤 심는다.</summary>
        public void PlantCrop(int plotIndex, string seedType, Action<bool, string> onCompleted)
        {
            string buyJson = JsonUtility.ToJson(new SeedBuyRequest { seedType = seedType, quantity = 1 });
            StartCoroutine(SendJson("POST", "/api/seeds/buy", buyJson, true, (buySuccess, buyBody) =>
            {
                if (!buySuccess)
                {
                    onCompleted?.Invoke(false, ExtractErrorMessage(buyBody, "씨앗 구매를 서버에 반영하지 못했습니다."));
                    return;
                }

                string plantJson = JsonUtility.ToJson(new PlantRequest { plotIndex = plotIndex, seedType = seedType });
                StartCoroutine(SendJson("POST", "/api/crops/plant", plantJson, true,
                    (success, body) => onCompleted?.Invoke(success, success ? "ok" : ExtractErrorMessage(body, "심기를 서버에 반영하지 못했습니다."))));
            }));
        }

        public void WaterCrop(int plotIndex, bool succeeded, Action<bool, string> onCompleted)
        {
            string json = JsonUtility.ToJson(new WaterRequest { plotIndex = plotIndex, succeeded = succeeded });
            StartCoroutine(SendJson("POST", "/api/crops/water", json, true,
                (success, body) => onCompleted?.Invoke(success, success ? "ok" : ExtractErrorMessage(body, "물주기를 서버에 반영하지 못했습니다."))));
        }

        public void HarvestCrop(int plotIndex, Action<bool, string> onCompleted)
        {
            StartCoroutine(HarvestWithRetry(plotIndex, 3, onCompleted));
        }

        // 클라이언트와 서버의 시계가 몇 초 어긋나면 서버는 아직 not_ready일 수 있어 잠시 후 재시도한다.
        private IEnumerator HarvestWithRetry(int plotIndex, int attemptsLeft, Action<bool, string> onCompleted)
        {
            string json = JsonUtility.ToJson(new PlotRequest { plotIndex = plotIndex });
            bool success = false;
            string responseBody = null;
            yield return SendJson("POST", "/api/crops/harvest", json, true, (ok, body) =>
            {
                success = ok;
                responseBody = body;
            });

            if (success)
            {
                onCompleted?.Invoke(true, "ok");
                yield break;
            }

            ErrorResponse error = FromJsonSafe<ErrorResponse>(responseBody);
            if (error != null && error.error == "not_ready" && attemptsLeft > 1)
            {
                yield return new WaitForSeconds(2f);
                yield return HarvestWithRetry(plotIndex, attemptsLeft - 1, onCompleted);
                yield break;
            }

            onCompleted?.Invoke(false, ExtractErrorMessage(responseBody, "수확을 서버에 반영하지 못했습니다."));
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
        private sealed class PlotRequest
        {
            public int plotIndex;
        }

        [Serializable]
        private sealed class SeedBuyRequest
        {
            public string seedType;
            public int quantity;
        }

        [Serializable]
        private sealed class PlantRequest
        {
            public int plotIndex;
            public string seedType;
        }

        [Serializable]
        private sealed class WaterRequest
        {
            public int plotIndex;
            public bool succeeded;
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
