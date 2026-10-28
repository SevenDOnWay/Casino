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
            if ( runner == null && runnerPrefab != null ) {
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

        public async Task<StartGameResult> QuickPlay( int betStake, int playerCount = 4 ) {
            if ( betStake < 5 ) {
                throw new ArgumentException("Minimum bet stake to play is 5.", nameof(betStake));
            }

            if ( runner == null && runnerPrefab != null ) {
                runner = Instantiate(runnerPrefab);
                runner.AddCallbacks(this);
            }

            if ( lobbyRestService == null ) {
                throw new InvalidOperationException("[TienLenNetworkController] LobbyRestService is not configured. Cannot perform matchmaking.");
            }

            QuickJoinResponseDto qj;
            try {
                qj = await lobbyRestService.QuickJoinAsync(betStake);
            }
            catch ( Exception ex ) {
                Debug.LogError($"[TienLenNetworkController] QuickJoin failed: {ex.Message}");
                throw new Exception($"Cannot connect to matchmaking service: {ex.Message}", ex);
            }

            if ( qj == null ) {
                throw new Exception("Matchmaking service returned an empty response.");
            }

            // Target channel is strictly dictated by the backend matchmaking response (backend sharding)
            int targetChannel = qj.channel > 0 ? qj.channel : 1;

            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var sessionProperties = new Dictionary<string, SessionProperty> {{ "BetStake", betStake.ToString() }};
            var sceneRef = GetGameplaySceneRef();
            StartGameResult result;

            if ( qj.found && !string.IsNullOrEmpty(qj.sessionName) ) {
                // Table found: Join existing session as Client
                Debug.Log($"[TienLenNetworkController] QuickPlay joining existing table '{qj.sessionName}' on channel {targetChannel} for stake {betStake}");
                result = await runner.StartGame(new StartGameArgs {
                    GameMode = GameMode.Client,
                    SessionName = qj.sessionName,
                    CustomLobbyName = GetLobbyId(betStake, targetChannel),
                    SessionProperties = sessionProperties,
                    Scene = sceneRef,
                    SceneManager = sceneManager
                });
            }
            else {
                // No table available: Host new session on backend-assigned channel
                string sessionName = GenerateRandomRoomName();
                Debug.Log($"[TienLenNetworkController] QuickPlay hosting new table '{sessionName}' on channel {targetChannel} for stake {betStake}");
                result = await runner.StartGame(new StartGameArgs {
                    GameMode = GameMode.Host,
                    SessionName = sessionName,
                    CustomLobbyName = GetLobbyId(betStake, targetChannel),
                    SessionProperties = sessionProperties,
                    PlayerCount = playerCount,
                    Scene = sceneRef,
                    SceneManager = sceneManager,
                    IsOpen = true,
                    IsVisible = true
                });

                if ( result.Ok ) {
                    // Attach event-driven sync handler to runner (persists with runner into gameplay scene)
                    var syncHandler = runner.gameObject.AddComponent<LobbySessionSyncHandler>();
                    bool registered = await syncHandler.InitializeAsync(lobbyRestService, runner, sessionName, betStake, targetChannel, initialPlayerCount: 1, maxPlayers: playerCount);
                    if ( !registered ) {
                        // Retry once after a brief delay
                        await UniTask.Delay(500);
                        registered = await syncHandler.InitializeAsync(lobbyRestService, runner, sessionName, betStake, targetChannel, initialPlayerCount: 1, maxPlayers: playerCount);
                    }

                    if ( !registered ) {
                        Debug.LogError($"[TienLenNetworkController] Failed to register hosted session '{sessionName}' with backend directory. Shutting down runner.");
                        await runner.Shutdown();
                        throw new InvalidOperationException($"Failed to register hosted room '{sessionName}' with backend directory.");
                    }
                }
            }

            if ( !result.Ok ) {
                Debug.LogError($"Failed to start session: {result.ShutdownReason}");
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
                    var syncHandler = runner.gameObject.AddComponent<LobbySessionSyncHandler>();
                    bool registered = await syncHandler.InitializeAsync(lobbyRestService, runner, sessionName, betStake, channel, initialPlayerCount: 1, maxPlayers: playerCount);
                    if ( !registered ) {
                        await UniTask.Delay(500);
                        registered = await syncHandler.InitializeAsync(lobbyRestService, runner, sessionName, betStake, channel, initialPlayerCount: 1, maxPlayers: playerCount);
                    }

                    if ( !registered ) {
                        Debug.LogError($"[TienLenNetworkController] Failed to register hosted session '{sessionName}' with backend directory. Shutting down runner.");
                        await runner.Shutdown();
                        throw new InvalidOperationException($"Failed to register hosted room '{sessionName}' with backend directory.");
                    }
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

            // 2. Resume session registration as new host
            if ( newRunner != null && newRunner.IsServer && lobbyRestService != null && newRunner.SessionInfo != null ) {
                int betStake = 5;
                if ( newRunner.SessionInfo.Properties != null && newRunner.SessionInfo.Properties.TryGetValue("BetStake", out var prop) ) {
                    int.TryParse(prop.PropertyValue.ToString(), out betStake);
                }
                var syncHandler = newRunner.gameObject.AddComponent<LobbySessionSyncHandler>();
                _ = syncHandler.InitializeAsync(lobbyRestService, newRunner, newRunner.SessionInfo.Name, betStake, 1, initialPlayerCount: newRunner.SessionInfo.PlayerCount, maxPlayers: newRunner.SessionInfo.MaxPlayers);
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

        #region INetworkRunnerCallbacks

        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) { }
        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) { }
        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) { }
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
