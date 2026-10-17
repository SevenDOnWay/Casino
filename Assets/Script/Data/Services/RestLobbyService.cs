using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Script.Data.Services {
    [Serializable]
    public class ChannelQueryResponseDto {
        public int betStake;
        public List<int> channels;
        public int recommendedChannel;
    }

    [Serializable]
    public class LobbySessionDto {
        public string sessionName;
        public int betStake;
        public int channel;
        public int playerCount;
        public int maxPlayers;
    }

    [Serializable]
    public class QuickJoinResponseDto {
        public bool found;
        public string action;
        public string sessionName;
        public int channel;
        public int betStake;
    }

    [Serializable]
    public class RegisterSessionPayload {
        public string sessionName;
        public int betStake;
        public int channel;
        public int playerCount;
        public int maxPlayers;
    }

    [Serializable]
    public class HeartbeatPayload {
        public string sessionName;
        public int playerCount;
    }

    [Serializable]
    internal class ApiResponseWrapper<T> {
        public bool isSuccess;
        public T data;
        public string message;
        public string errorCode;
    }

    [Serializable]
    internal class ApiListResponseWrapper<T> {
        public bool isSuccess;
        public List<T> data;
        public string message;
        public string errorCode;
    }

    public interface ILobbyRestService {
        UniTask<ChannelQueryResponseDto> GetChannelsAsync( int betStake );
        UniTask<List<LobbySessionDto>> GetSessionsAsync( int betStake, int channel );
        UniTask<bool> RegisterSessionAsync( string sessionName, int betStake, int channel, int playerCount = 1, int maxPlayers = 4 );
        UniTask<bool> SendHeartbeatAsync( string sessionName, int playerCount );
        UniTask<bool> UnregisterSessionAsync( string sessionName );
        UniTask<QuickJoinResponseDto> QuickJoinAsync( int betStake );
    }

    public class RestLobbyService : ILobbyRestService {
        private readonly string baseUrl;

        public RestLobbyService( string baseUrl = "http://localhost:5000/api" ) {
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async UniTask<ChannelQueryResponseDto> GetChannelsAsync( int betStake ) {
            string url = $"{baseUrl}/lobby/channels?betStake={betStake}";
            return await SendGetAsync<ChannelQueryResponseDto>(url);
        }

        public async UniTask<List<LobbySessionDto>> GetSessionsAsync( int betStake, int channel ) {
            string url = $"{baseUrl}/lobby/sessions?betStake={betStake}&channel={channel}";
            using var req = UnityWebRequest.Get(url);
            await req.SendWebRequest();

            if ( req.result != UnityWebRequest.Result.Success ) {
                return new List<LobbySessionDto>();
            }

            var wrapper = JsonUtility.FromJson<ApiListResponseWrapper<LobbySessionDto>>(req.downloadHandler.text);
            return wrapper?.data ?? new List<LobbySessionDto>();
        }

        public async UniTask<bool> RegisterSessionAsync( string sessionName, int betStake, int channel, int playerCount = 1, int maxPlayers = 4 ) {
            string url = $"{baseUrl}/lobby/register";
            var payload = new RegisterSessionPayload {
                sessionName = sessionName,
                betStake = betStake,
                channel = channel,
                playerCount = playerCount,
                maxPlayers = maxPlayers
            };
            string json = JsonUtility.ToJson(payload);
            return await SendPostAsync<bool>(url, json);
        }

        public async UniTask<bool> SendHeartbeatAsync( string sessionName, int playerCount ) {
            string url = $"{baseUrl}/lobby/heartbeat";
            var payload = new HeartbeatPayload {
                sessionName = sessionName,
                playerCount = playerCount
            };
            string json = JsonUtility.ToJson(payload);
            return await SendPostAsync<bool>(url, json);
        }

        public async UniTask<bool> UnregisterSessionAsync( string sessionName ) {
            string url = $"{baseUrl}/lobby/sessions/{UnityWebRequest.EscapeURL(sessionName)}";
            using var req = UnityWebRequest.Delete(url);
            await req.SendWebRequest();
            return req.result == UnityWebRequest.Result.Success;
        }

        public async UniTask<QuickJoinResponseDto> QuickJoinAsync( int betStake ) {
            string url = $"{baseUrl}/lobby/quick-join?betStake={betStake}";
            return await SendGetAsync<QuickJoinResponseDto>(url);
        }

        private async UniTask<T> SendGetAsync<T>( string url ) {
            using var req = UnityWebRequest.Get(url);
            await req.SendWebRequest();
            if ( req.result != UnityWebRequest.Result.Success ) {
                throw new Exception($"[RestLobbyService] GET {url} failed: {req.error}");
            }
            var wrapper = JsonUtility.FromJson<ApiResponseWrapper<T>>(req.downloadHandler.text);
            return wrapper.data;
        }

        private async UniTask<bool> SendPostAsync<T>( string url, string bodyJson ) {
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            byte[] body = Encoding.UTF8.GetBytes(bodyJson);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();
            return req.result == UnityWebRequest.Result.Success;
        }
    }
}
