using Assets.Script.TienLen.Game;
using Fusion;
using Fusion.Sockets;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetworkController : MonoBehaviour, INetworkRunnerCallbacks {
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string gameplaySceneName;
        [SerializeField] private string mainMenuSceneName;
        [SerializeField] private string testRoomName = "TienLenTest";

        private NetworkRunner runner;

        public event Action<List<SessionInfo>> SessionListUpdated;
        public string CurrentLobbyId { get; private set; }

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
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var sessionProperties = new Dictionary<string, SessionProperty> {{ "BetStake", betStake.ToString() }};

            var result = await runner.StartGame(new StartGameArgs{
                GameMode = GameMode.AutoHostOrClient,

                // Find or host a session in the specific bet lobby
                CustomLobbyName = GetLobbyId(betStake, channel),
                SessionProperties = sessionProperties,

                SessionName = GenerateRandomRoomName(),
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
