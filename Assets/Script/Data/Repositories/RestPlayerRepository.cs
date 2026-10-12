using Assets.Script.Data.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Script.Data.Repositories {
    public class RestPlayerRepository : IPlayerRepository {
        private readonly string baseUrl;

        public RestPlayerRepository( string baseUrl = "http://localhost:5000/api" ) {
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async UniTask<AuthResponse> LoginAsync( LoginRequest request ) {
            string url = $"{baseUrl}/auth/login";
            string bodyJson = JsonUtility.ToJson(request);
            return await SendRequestAsync<AuthResponse>(url, UnityWebRequest.kHttpVerbPOST, bodyJson);
        }

        public async UniTask<AuthResponse> RegisterAsync( RegisterRequest request ) {
            string url = $"{baseUrl}/auth/register";
            string bodyJson = JsonUtility.ToJson(request);
            return await SendRequestAsync<AuthResponse>(url, UnityWebRequest.kHttpVerbPOST, bodyJson);
        }

        public async UniTask<PlayerProfileData> GetProfileAsync( string token ) {
            string url = $"{baseUrl}/user/profile";
            return await SendRequestAsync<PlayerProfileData>(url, UnityWebRequest.kHttpVerbGET, null, token);
        }

        public async UniTask<PlayerProfileData> UpdateProfileAsync( string token, UpdateProfileRequest request ) {
            string url = $"{baseUrl}/user/profile";
            string bodyJson = JsonUtility.ToJson(request);
            return await SendRequestAsync<PlayerProfileData>(url, UnityWebRequest.kHttpVerbPOST, bodyJson, token);
        }

        public async UniTask<List<PlayerProfileData>> GetFriendsAsync( string token ) {
            string url = $"{baseUrl}/user/friends";
            string json = await SendRawRequestAsync(url, UnityWebRequest.kHttpVerbGET, null, token);
            return JsonHelper.FromJsonList<PlayerProfileData>(json);
        }

        public async UniTask<bool> AddFriendAsync( string token, string friendEmailOrName ) {
            string url = $"{baseUrl}/user/friends";
            string bodyJson = JsonUtility.ToJson(new AddFriendRequest { friendEmailOrName = friendEmailOrName });
            var res = await SendRequestAsync<ApiResponse<bool>>(url, UnityWebRequest.kHttpVerbPOST, bodyJson, token);
            return res != null && res.isSuccess;
        }

        public async UniTask<bool> RecordMatchResultAsync( string token, MatchResultReportRequest request ) {
            string url = $"{baseUrl}/matches/report";
            string bodyJson = JsonUtility.ToJson(request);
            var res = await SendRequestAsync<ApiResponse<bool>>(url, UnityWebRequest.kHttpVerbPOST, bodyJson, token);
            return res != null && res.isSuccess;
        }

        public async UniTask<PlayerProfileData> GetByIdAsync( string id ) {
            string url = $"{baseUrl}/user/{id}";
            return await SendRequestAsync<PlayerProfileData>(url, UnityWebRequest.kHttpVerbGET);
        }

        public async UniTask<bool> UpdateAsync( string id, PlayerProfileData data ) {
            string url = $"{baseUrl}/user/{id}";
            string bodyJson = JsonUtility.ToJson(data);
            var res = await SendRequestAsync<ApiResponse<bool>>(url, UnityWebRequest.kHttpVerbPOST, bodyJson);
            return res != null && res.isSuccess;
        }

        private async UniTask<T> SendRequestAsync<T>( string url, string method, string body = null, string token = null ) {
            string responseText = await SendRawRequestAsync(url, method, body, token);
            if ( string.IsNullOrEmpty(responseText) ) {
                return default;
            }

            try {
                return JsonUtility.FromJson<T>(responseText);
            }
            catch ( Exception ex ) {
                Debug.LogError($"[RestPlayerRepository] Json parse error for url {url}: {ex.Message}\nRaw: {responseText}");
                return default;
            }
        }

        private async UniTask<string> SendRawRequestAsync( string url, string method, string body = null, string token = null ) {
            using var webRequest = new UnityWebRequest(url, method);

            if ( !string.IsNullOrEmpty(body) ) {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            if ( !string.IsNullOrEmpty(token) ) {
                webRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            }

            try {
                await webRequest.SendWebRequest().ToUniTask();
            }
            catch ( Exception ex ) {
                Debug.LogWarning($"[RestPlayerRepository] Request error ({url}): {ex.Message} -> Response: {webRequest.downloadHandler?.text}");
            }

            return webRequest.downloadHandler?.text;
        }

        private static class JsonHelper {
            [Serializable]
            private class Wrapper<T> {
                public List<T> items;
            }

            public static List<T> FromJsonList<T>( string json ) {
                if ( string.IsNullOrEmpty(json) ) return new List<T>();
                if ( !json.TrimStart().StartsWith("[") ) {
                    try {
                        var single = JsonUtility.FromJson<Wrapper<T>>(json);
                        if ( single != null && single.items != null ) return single.items;
                    }
                    catch { }
                }

                string wrappedJson = "{\"items\":" + json + "}";
                try {
                    var wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
                    return wrapper?.items ?? new List<T>();
                }
                catch {
                    return new List<T>();
                }
            }
        }
    }
}
