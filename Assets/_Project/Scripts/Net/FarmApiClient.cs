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

        public void GetFarm(Action<bool, string, FarmDataResponse> onCompleted)
        {
            SendAuthenticated<FarmDataResponse>("GET", "/api/farm", null, "농장 정보를 불러오지 못했습니다.", onCompleted);
        }

        public void BuyPlot(int plotIndex, float worldX, float worldY, Action<bool, string, BuyPlotResponse> onCompleted)
        {
            string json = JsonUtility.ToJson(new PlotBuyRequest { plotIndex = plotIndex, worldX = worldX, worldY = worldY });
            SendAuthenticated<BuyPlotResponse>("POST", "/api/plots/buy", json, "밭 구매에 실패했습니다.", onCompleted);
        }

        public void DeletePlot(int plotIndex, Action<bool, string, DeletePlotResponse> onCompleted)
        {
            string json = JsonUtility.ToJson(new PlotRequest { plotIndex = plotIndex });
            SendAuthenticated<DeletePlotResponse>("POST", "/api/plots/delete", json, "밭 삭제에 실패했습니다.", onCompleted);
        }

        public void BuySeed(string seedType, int quantity, Action<bool, string, BuySeedResponse> onCompleted)
        {
            string json = JsonUtility.ToJson(new BuySeedRequest { seedType = seedType, quantity = quantity });
            SendAuthenticated<BuySeedResponse>("POST", "/api/seeds/buy", json, "씨앗 구매에 실패했습니다.", onCompleted);
        }

        public void PlantCrop(int plotIndex, string seedType, Action<bool, string, PlantCropResponse> onCompleted)
        {
            string json = JsonUtility.ToJson(new PlantCropRequest { plotIndex = plotIndex, seedType = seedType });
            SendAuthenticated<PlantCropResponse>("POST", "/api/crops/plant", json, "작물 심기에 실패했습니다.", onCompleted);
        }

        public void WaterCrop(int plotIndex, bool succeeded, Action<bool, string, WaterCropResponse> onCompleted)
        {
            string json = JsonUtility.ToJson(new WaterCropRequest { plotIndex = plotIndex, succeeded = succeeded });
            SendAuthenticated<WaterCropResponse>("POST", "/api/crops/water", json, "물주기 저장에 실패했습니다.", onCompleted);
        }

        public void HarvestCrop(int plotIndex, Action<bool, string, HarvestCropResponse> onCompleted)
        {
            StartCoroutine(HarvestWithRetry(plotIndex, 3, onCompleted));
        }

        // 클라이언트와 서버의 시계가 몇 초 어긋나면 서버는 아직 not_ready일 수 있어 잠시 후 재시도한다.
        private IEnumerator HarvestWithRetry(int plotIndex, int attemptsLeft, Action<bool, string, HarvestCropResponse> onCompleted)
        {
            if (!IsLoggedIn)
            {
                onCompleted?.Invoke(false, "로그인이 필요합니다.", null);
                yield break;
            }

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
                HarvestCropResponse response = FromJsonSafe<HarvestCropResponse>(responseBody);
                if (response == null)
                {
                    onCompleted?.Invoke(false, "서버 응답을 해석할 수 없습니다.", null);
                    yield break;
                }

                onCompleted?.Invoke(true, string.Empty, response);
                yield break;
            }

            ErrorResponse error = FromJsonSafe<ErrorResponse>(responseBody);
            if (error != null && error.error == "not_ready" && attemptsLeft > 1)
            {
                yield return new WaitForSeconds(2f);
                yield return HarvestWithRetry(plotIndex, attemptsLeft - 1, onCompleted);
                yield break;
            }

            onCompleted?.Invoke(false, ExtractErrorMessage(responseBody, "수확 저장에 실패했습니다."), null);
        }

        private void SendAuthenticated<T>(
            string method,
            string path,
            string jsonBody,
            string fallbackMessage,
            Action<bool, string, T> onCompleted) where T : class
        {
            if (!IsLoggedIn)
            {
                onCompleted?.Invoke(false, "로그인이 필요합니다.", null);
                return;
            }

            StartCoroutine(SendJson(method, path, jsonBody, true, (success, body) =>
            {
                if (!success)
                {
                    onCompleted?.Invoke(false, ExtractErrorMessage(body, fallbackMessage), null);
                    return;
                }

                T response = FromJsonSafe<T>(body);
                if (response == null)
                {
                    onCompleted?.Invoke(false, "서버 응답을 해석할 수 없습니다.", null);
                    return;
                }

                onCompleted?.Invoke(true, string.Empty, response);
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
        private sealed class PlotRequest
        {
            public int plotIndex;
        }

        [Serializable]
        private sealed class PlotBuyRequest
        {
            public int plotIndex;
            public float worldX;
            public float worldY;
        }

        [Serializable]
        private sealed class BuySeedRequest
        {
            public string seedType;
            public int quantity;
        }

        [Serializable]
        private sealed class PlantCropRequest
        {
            public int plotIndex;
            public string seedType;
        }

        [Serializable]
        private sealed class WaterCropRequest
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

        [Serializable]
        public sealed class FarmDataResponse
        {
            public int money;
            public int level;
            public int wheat_harvest_count;
            public bool batch_unlocked;
            public PlotDto[] plots;
            public InventoryDto[] inventory;
        }

        [Serializable]
        public sealed class PlotDto
        {
            public int plot_index;
            // MySQL BOOLEAN(TINYINT)은 mysql2에서 JSON 숫자 0/1로 직렬화된다.
            public int unlocked;
            public string crop_type;
            public string planted_at;
            public int water_count;
            public string ready_at;
            public string state;
            public float world_x;
            public float world_y;
            public int has_position;
        }

        [Serializable]
        public sealed class InventoryDto
        {
            public string item_type;
            public int quantity;
        }

        [Serializable]
        public sealed class BuyPlotResponse
        {
            public int plotIndex;
            public int spent;
            public int money;
        }

        [Serializable]
        public sealed class DeletePlotResponse
        {
            public int plotIndex;
            public int refunded;
        }

        [Serializable]
        public sealed class BuySeedResponse
        {
            public string seedType;
            public int quantity;
            public int spent;
            public int money;
        }

        [Serializable]
        public sealed class PlantCropResponse
        {
            public int plotIndex;
            public string cropType;
            public int growSeconds;
        }

        [Serializable]
        public sealed class WaterCropResponse
        {
            public int plotIndex;
            public bool succeeded;
            public int reducedSeconds;
            public int waterCount;
            public int maxWaterCount;
            public string readyAt;
            public string state;
        }

        [Serializable]
        public sealed class HarvestCropResponse
        {
            public int plotIndex;
            public string cropType;
            public int earned;
            public int money;
            public int wheat_harvest_count;
            public bool batch_unlocked;
        }
    }
}
