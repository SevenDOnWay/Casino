using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.Script.TienLen.Game;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetworkController : MonoBehaviour, INetworkRunnerCallbacks {
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string gameplaySceneName;
        [SerializeField] private string mainMenuSceneName;
        [SerializeField] private string testRoomName = "TienLenTest";

        private NetworkRunner runner;

        public void Start() {
            runner = Instantiate(runnerPrefab);
            runner.AddCallbacks(this);
        }

        public async void HostRoomButton() {
            await StartSimulation(runner, GameMode.Host, testRoomName, 4);
        }

        public async void JoinRoomButton( string roomName ) {
            string targetRoom = string.IsNullOrEmpty(roomName) ? testRoomName : roomName;
            await StartSimulation(runner, GameMode.Client, targetRoom);
        }

        private SceneRef GetGameplaySceneRef() {
            var sceneIndex = SceneUtility.GetBuildIndexByScenePath(gameplaySceneName);
            if ( sceneIndex < 0 ) {
                sceneIndex = SceneManager.GetActiveScene().buildIndex;
            }
            return SceneRef.FromIndex(sceneIndex);
        }

        private async Task<StartGameResult> StartSimulation( NetworkRunner runner, GameMode mode, string sessionName, int playerCount = 4 ) {
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var sceneRef = GetGameplaySceneRef();

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = playerCount,
                Scene = sceneRef,
                SceneManager = sceneManager
            });

            if ( !result.Ok ) {
                Debug.LogError($"Failed to start session: {result.ShutdownReason}");
            }

            return result;
        }

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
        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) { }
        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) { }

        public void OnSceneLoadDone( NetworkRunner runner ) { }
        public void OnSceneLoadStart( NetworkRunner runner ) { }

        #endregion
    }
}
