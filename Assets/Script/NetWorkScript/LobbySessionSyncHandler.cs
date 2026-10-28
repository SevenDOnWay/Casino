using Assets.Script.Data.Services;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    /// <summary>
    /// Dedicated component attached to NetworkRunner when hosting a match.
    /// Event-driven: reports creation (register), player count changes (join/leave),
    /// and match end (unregister) to the backend database without continuous polling.
    /// </summary>
    public class LobbySessionSyncHandler : MonoBehaviour, INetworkRunnerCallbacks {
        private ILobbyRestService lobbyRestService;
        private NetworkRunner runner;
        private string sessionName;
        private int betStake;
        private int channel;
        private bool isRegistered = false;

        public bool IsRegistered => isRegistered;
        public string SessionName => sessionName;

        public async UniTask<bool> InitializeAsync(
            ILobbyRestService restService,
            NetworkRunner networkRunner,
            string roomName,
            int stake,
            int channelNumber,
            int initialPlayerCount,
            int maxPlayers = 4
        ) {
            this.lobbyRestService = restService;
            this.runner = networkRunner;
            this.sessionName = roomName;
            this.betStake = stake;
            this.channel = channelNumber;

            if ( runner != null ) {
                runner.AddCallbacks(this);
            }

            if ( lobbyRestService != null ) {
                try {
                    isRegistered = await lobbyRestService.RegisterSessionAsync(
                        sessionName,
                        betStake,
                        channel,
                        initialPlayerCount,
                        maxPlayers
                    );
                    Debug.Log($"[LobbySessionSyncHandler] Session '{sessionName}' registered to backend (Bet: {betStake}, Ch: {channel}, Registered: {isRegistered})");
                }
                catch ( Exception ex ) {
                    Debug.LogError($"[LobbySessionSyncHandler] Failed to register session '{sessionName}': {ex.Message}");
                    isRegistered = false;
                }
            }

            return isRegistered;
        }

        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            if ( runner != null && runner.IsServer && isRegistered && lobbyRestService != null ) {
                int count = runner.SessionInfo != null ? runner.SessionInfo.PlayerCount : 1;
                Debug.Log($"[LobbySessionSyncHandler] Player joined ({player.PlayerId}). Updating live player count to {count}");
                _ = lobbyRestService.SendHeartbeatAsync(sessionName, count);
            }
        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            if ( runner != null && runner.IsServer && isRegistered && lobbyRestService != null ) {
                int count = runner.SessionInfo != null ? runner.SessionInfo.PlayerCount : 1;
                Debug.Log($"[LobbySessionSyncHandler] Player left ({player.PlayerId}). Updating live player count to {count}");
                _ = lobbyRestService.SendHeartbeatAsync(sessionName, count);
            }
        }

        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) {
            if ( isRegistered && lobbyRestService != null && !string.IsNullOrEmpty(sessionName) ) {
                Debug.Log($"[LobbySessionSyncHandler] Runner shutting down ({shutdownReason}). Unregistering session '{sessionName}'");
                _ = lobbyRestService.UnregisterSessionAsync(sessionName);
                isRegistered = false;
            }
        }

        private void OnDestroy() {
            if ( runner != null ) {
                runner.RemoveCallbacks(this);
            }

            if ( isRegistered && lobbyRestService != null && !string.IsNullOrEmpty(sessionName) ) {
                _ = lobbyRestService.UnregisterSessionAsync(sessionName);
                isRegistered = false;
            }
        }

        #region INetworkRunnerCallbacks Stubs
        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnDisconnectedFromServer( NetworkRunner runner, NetDisconnectReason reason ) { }
        public void OnConnectRequest( NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token ) { }
        public void OnConnectFailed( NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason ) { }
        public void OnUserSimulationMessage( NetworkRunner runner, SimulationMessagePtr message ) { }
        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) { }
        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) { }
        public void OnHostMigration( NetworkRunner runner, HostMigrationToken hostMigrationToken ) { }
        public void OnReliableDataReceived( NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data ) { }
        public void OnReliableDataProgress( NetworkRunner runner, PlayerRef player, ReliableKey key, float progress ) { }
        public void OnSceneLoadDone( NetworkRunner runner ) { }
        public void OnSceneLoadStart( NetworkRunner runner ) { }
        public void OnInput( NetworkRunner runner, NetworkInput input ) { }
        public void OnInputMissing( NetworkRunner runner, PlayerRef player, NetworkInput input ) { }
        public void OnConnectedToServer( NetworkRunner runner ) { }
        #endregion
    }
}
