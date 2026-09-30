using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
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

        public void Start() {
            runner = Instantiate(runnerPrefab);
        }

        public async void HostRoomButton() {
            await StartSimulation(runner, GameMode.Host, testRoomName, 4);
        }

        public async void JoinRoomButton( string roomName ) {
            string targetRoom = string.IsNullOrEmpty(roomName) ? testRoomName : roomName;
            await StartSimulation(runner, GameMode.Client, targetRoom);
        }

        private async Task<StartGameResult> StartSimulation( NetworkRunner runner, GameMode mode, string sessionName, int playerCount = 4 ) {
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var sceneIndex = SceneUtility.GetBuildIndexByScenePath(gameplaySceneName);
            var sceneRef = SceneRef.FromIndex(sceneIndex);

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
        public void OnHostMigration( NetworkRunner runner, HostMigrationToken hostMigrationToken ) { }
        public void OnSceneLoadDone( NetworkRunner runner ) { }
        public void OnSceneLoadStart( NetworkRunner runner ) { }

        #endregion
    }
}
