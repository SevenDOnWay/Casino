using Assets.Script.Data.Services;
using Assets.Script.TienLen.Game;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetworkController : MonoBehaviour, INetworkRunnerCallbacks {
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string gameplaySceneName;
        [SerializeField] private string mainMenuSceneName;
        [SerializeField] private string testRoomName = "TienLenTest";

        [Inject] private ILobbyRestService lobbyRestService;
        private CancellationTokenSource heartbeatCts;

        private NetworkRunner runner;

        public event Action<List<SessionInfo>> SessionListUpdated;
        public string CurrentLobbyId { get; private set; }

        public void SetLobbyRestServiceForTest( ILobbyRestService service ) {
            this.lobbyRestService = service;
        }

        public static string GetLobbyId( int betStake, int channel = 1 ) {
            int safeChannel = channel < 1 ? 1 : channel;
            return $"Bet_{betStake}_Ch_{safeChannel}";
        }

        public void Start() {
            runner = Instantiate(runnerPrefab);
            runner.AddCallbacks(this);
        }

        public void HostRoom( string roomName, int betStake, int playerCount = 4, int channel = 1 ) {
            _ = StartSimulation(runner, GameMode.Host, roomName, betStake, playerCount, channel);
        }

        public void JoinRoom( string roomName, int betStake, int channel = 1 ) {
            _ = StartSimulation(runner, GameMode.Client, roomName, betStake, 4, channel);
        }

        public async Task<StartGameResult> JoinBetLobby( int betStake, int channel = 1 ) {
            if ( runner == null ) {
                runner = Instantiate(runnerPrefab);
                runner.AddCallbacks(this);
            }

            string lobbyId = GetLobbyId(betStake, channel);
            Debug.Log($"[TienLenNetworkController] Joining session lobby: {lobbyId}");
            var result = await runner.JoinSessionLobby(SessionLobby.Custom, lobbyId);
            if ( result.Ok ) {
                CurrentLobbyId = lobbyId;
            }
            else {
                Debug.LogError($"[TienLenNetworkController] Failed to join lobby '{lobbyId}': {result.ShutdownReason}");
            }
            return result;
        }

        public async Task<StartGameResult> QuickPlay( int betStake, int playerCount = 4, int channel = 1 ) {
            int targetChannel = channel;
            string targetSessionName = null;

            if ( lobbyRestService != null ) {
                try {
                    var qj = await lobbyRestService.QuickJoinAsync(betStake);
                    if ( qj != null ) {
                        if ( qj.channel > 0 ) targetChannel = qj.channel;
                        if ( qj.found && !string.IsNullOrEmpty(qj.sessionName) ) {
                            targetSessionName = qj.sessionName;
                        }
                    }
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[TienLenNetworkController] QuickJoin backend query fallback: {ex.Message}");
                }
            }

            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var sessionProperties = new Dictionary<string, SessionProperty> {{ "BetStake", betStake.ToString() }};
            var sessionToJoinOrHost = targetSessionName ?? GenerateRandomRoomName();

            var result = await runner.StartGame(new StartGameArgs{
                GameMode = GameMode.AutoHostOrClient,

                // Find or host a session in the specific bet lobby
                CustomLobbyName = GetLobbyId(betStake, targetChannel),
                SessionProperties = sessionProperties,

                SessionName = sessionToJoinOrHost,
                PlayerCount = playerCount,
                Scene = GetGameplaySceneRef(),
                SceneManager = sceneManager,

                // Allow new tables to accept players and appear in the lobby.
                IsOpen = true,
                IsVisible = true,

                MatchmakingMode = MatchmakingMode.FillRoom
            });

            if ( !result.Ok ) {
                Debug.LogError($"Failed to start session: {result.ShutdownReason}");
            }
            else {
                if ( runner.IsServer && lobbyRestService != null ) {
                    _ = RegisterAndStartHeartbeatAsync(runner.SessionInfo?.Name ?? sessionToJoinOrHost, betStake, targetChannel, playerCount);
                }
            }

            return result;
        }

        private SceneRef GetGameplaySceneRef() {
            var sceneIndex = SceneUtility.GetBuildIndexByScenePath(gameplaySceneName);
            if ( sceneIndex < 0 ) {
                sceneIndex = SceneManager.GetActiveScene().buildIndex;
            }
            return SceneRef.FromIndex(sceneIndex);
        }

        private async Task<StartGameResult> StartSimulation( NetworkRunner runner, GameMode mode, string sessionName, int betStake, int playerCount = 4, int channel = 1 ) {
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            Dictionary<string, SessionProperty> sessionProperties = new (){{ "BetStake", betStake.ToString() }};

            var sceneRef = GetGameplaySceneRef();

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                CustomLobbyName = GetLobbyId(betStake, channel),
                SessionProperties = sessionProperties,
                PlayerCount = playerCount,
                Scene = sceneRef,
                SceneManager = sceneManager
            });

            if ( !result.Ok ) {
                Debug.LogError($"Failed to start session: {result.ShutdownReason}");
            }
            else {
                if ( mode == GameMode.Host && lobbyRestService != null ) {
                    _ = RegisterAndStartHeartbeatAsync(sessionName, betStake, channel, playerCount);
                }
            }

            return result;
        }

        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) {
            Debug.Log($"[SessionListUpdated] Received {sessionList.Count} sessions.");
            SessionListUpdated?.Invoke(sessionList);
        }

        public string GenerateRandomRoomName() {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var stringBuilder = new StringBuilder(6);
            var random = new System.Random();
            
            for ( int i = 0; i < 6; i++ ) {
                int index = random.Next(chars.Length);
                stringBuilder.Append(chars[index]);
            }

            return stringBuilder.ToString();
        }

        #region Host Migration
        private void OnHostMigrationResume( NetworkRunner newRunner ) {
            Debug.Log("[HostMigration] Resuming Host on new Host instance...");

            // 1. Restore Scene Objects state from snapshot
            foreach ( var (sceneNO, header) in newRunner.GetResumeSnapshotNetworkSceneObjects() ) {
                if ( sceneNO != null ) {
                    sceneNO.CopyStateFrom(header);
                    Debug.Log($"[HostMigration] Restored scene object '{sceneNO.name}'");

                    if ( sceneNO.TryGetComponent<SeatManager>(out var seatManager) ) {
                        seatManager.CleanupDisconnectedSeats();
                    }
                }
            }

            // 2. Resume session registration and heartbeat as new host
            if ( newRunner != null && newRunner.IsServer && lobbyRestService != null && newRunner.SessionInfo != null ) {
                int betStake = 5;
                if ( newRunner.SessionInfo.Properties != null && newRunner.SessionInfo.Properties.TryGetValue("BetStake", out var prop) ) {
                    int.TryParse(prop.PropertyValue.ToString(), out betStake);
                }
                _ = RegisterAndStartHeartbeatAsync(newRunner.SessionInfo.Name, betStake, 1, newRunner.SessionInfo.MaxPlayers);
            }
        }

        public async void OnHostMigration( NetworkRunner oldRunner, HostMigrationToken hostMigrationToken ) {
            Debug.Log($"[HostMigration] Initiating host migration. New GameMode: {hostMigrationToken.GameMode}");

            // 1. Shut down old runner
            await oldRunner.Shutdown(destroyGameObject: true, shutdownReason: ShutdownReason.HostMigration);

            // 2. Create new runner
            runner = Instantiate(runnerPrefab);
            runner.AddCallbacks(this);

            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            // Disable scene takeover to force a clean scene reload so destroyed scene GameObjects (Canvas, Seat, etc.) are reloaded fresh
            sceneManager.IsSceneTakeOverEnabled = false;

            var sceneRef = GetGameplaySceneRef();

            // 3. Start game with migration token and scene
            var result = await runner.StartGame(new StartGameArgs
            {
                HostMigrationToken = hostMigrationToken,
                HostMigrationResume = OnHostMigrationResume,
                GameMode = hostMigrationToken.GameMode,
                Scene = sceneRef,
                SceneManager = sceneManager
            });

            if ( !result.Ok ) {
                Debug.LogError($"[HostMigration] Failed to resume simulation: {result.ShutdownReason}");
                if ( !string.IsNullOrEmpty(mainMenuSceneName) ) {
                    SceneManager.LoadScene(mainMenuSceneName);
                }
            }
            else {
                Debug.Log($"[HostMigration] Succeeded. New GameMode: {runner.GameMode}");
            }
        }

        #endregion

        #region Session Directory Sync & Heartbeats

        private void OnDestroy() {
            CancelHeartbeat();
        }

        private void CancelHeartbeat() {
            if ( heartbeatCts != null ) {
                heartbeatCts.Cancel();
                heartbeatCts.Dispose();
                heartbeatCts = null;
            }
        }

        private async UniTask RegisterAndStartHeartbeatAsync( string sessionName, int betStake, int channel, int maxPlayers ) {
            CancelHeartbeat();
            heartbeatCts = new CancellationTokenSource();
            var token = heartbeatCts.Token;

            if ( lobbyRestService != null ) {
                try {
                    await lobbyRestService.RegisterSessionAsync(sessionName, betStake, channel, playerCount: 1, maxPlayers: maxPlayers);
                    Debug.Log($"[TienLenNetworkController] Registered session '{sessionName}' (Bet: {betStake}, Ch: {channel}) to directory.");
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[TienLenNetworkController] RegisterSession error: {ex.Message}");
                }
            }

            try {
                while ( !token.IsCancellationRequested && runner != null && runner.IsRunning && runner.IsServer ) {
                    await UniTask.Delay(TimeSpan.FromSeconds(10), cancellationToken: token);
                    if ( lobbyRestService != null && runner != null && runner.IsRunning ) {
                        int currentPlayers = runner.SessionInfo != null ? runner.SessionInfo.PlayerCount : 1;
                        await lobbyRestService.SendHeartbeatAsync(sessionName, currentPlayers);
                    }
                }
            }
            catch ( OperationCanceledException ) { }
            catch ( Exception ex ) {
                Debug.LogWarning($"[TienLenNetworkController] Heartbeat error: {ex.Message}");
            }
        }

        private async UniTask StopHeartbeatAndUnregisterAsync( string sessionName ) {
            CancelHeartbeat();

            if ( lobbyRestService != null && !string.IsNullOrEmpty(sessionName) ) {
                try {
                    await lobbyRestService.UnregisterSessionAsync(sessionName);
                    Debug.Log($"[TienLenNetworkController] Unregistered session '{sessionName}' from directory.");
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[TienLenNetworkController] UnregisterSession error: {ex.Message}");
                }
            }
        }

        #endregion

        #region INetworkRunnerCallbacks

        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) { }
        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) { }
        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) {
            if ( runner != null && runner.IsServer && runner.SessionInfo != null && !string.IsNullOrEmpty(runner.SessionInfo.Name) ) {
                _ = StopHeartbeatAndUnregisterAsync(runner.SessionInfo.Name);
            }
            else {
                CancelHeartbeat();
            }
        }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer( NetworkRunner runner, NetDisconnectReason reason ) { }
        public void OnConnectRequest( NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token ) { }
        public void OnConnectFailed( NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason ) { }
        public void OnReliableDataReceived( NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data ) { }
        public void OnReliableDataProgress( NetworkRunner runner, PlayerRef player, ReliableKey key, float progress ) { }
        public void OnInput( NetworkRunner runner, NetworkInput input ) { }
        public void OnInputMissing( NetworkRunner runner, PlayerRef player, NetworkInput input ) { }
        void INetworkRunnerCallbacks.OnConnectedToServer( NetworkRunner runner ) { }
        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) { }

        public void OnSceneLoadDone( NetworkRunner runner ) { }
        public void OnSceneLoadStart( NetworkRunner runner ) { }

        #endregion
    }
}
