using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Unity.Collections.Unicode;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetworkController : MonoBehaviour, INetworkRunnerCallbacks {
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string gameplaySceneName;
        [SerializeField] private string mainMenuSceneName;


        [SerializeField] private int testClientCount = 1;

        [SerializeField] private string testRoomName = "TienLenTest";
        private readonly List<NetworkRunner> runners = new();

        private NetworkRunner runner;

        public async void Start() {

            runner = Instantiate(runnerPrefab);

            //await runner.StartGame(new StartGameArgs {
            //    GameMode = GameMode.AutoHostOrClient,
            //    SessionName = "TienLenTest",
            //    PlayerCount = 4
            //});
        }

        #region Multi-Peer Test
        /*
        private async Task StartMultiPeerTest() {
            Debug.Log("[Fusion] Starting Multi-Peer test...");
            // ------------------------- // HOST // -------------------------
            NetworkRunner hostRunner = await CreateRunner();
            StartGameResult hostResult = await StartSimulation( hostRunner, GameMode.Host, testRoomName, 4 );

            if ( !hostResult.Ok ) {
                Debug.LogError($"[Fusion] Failed to start Host: {hostResult.ShutdownReason}");
                return;
            }
            Debug.Log("[Fusion] Host started.");
            // ------------------------- // CLIENTS // -------------------------
            for ( int i = 0; i < testClientCount; i++ ) {
                NetworkRunner clientRunner = await CreateRunner();
                StartGameResult clientResult = await StartSimulation( clientRunner, GameMode.Client, testRoomName );
                if ( !clientResult.Ok ) {
                    Debug.LogError($"[Fusion] Failed to start Client {i}: " + $"{clientResult.ShutdownReason}");
                    continue;
                }
                Debug.Log($"[Fusion] Client {i + 1} started.");
            }
            Debug.Log($"[Fusion] Multi-Peer test ready. " + $"Peers: {runners.Count}");
        }

        private async Task<NetworkRunner> CreateRunner() {
            NetworkRunner runner = Instantiate(runnerPrefab);
            runner.name = $"FusionRunner_{runners.Count}";
            runner.AddCallbacks(this);
            runners.Add(runner); return runner;
        }
        */

        #endregion

        public async void HostRoomButton() {
            await StartSimulation(runner, GameMode.Host, testRoomName, 4);
        }

        public async void JoinRoomButton( string roomName ) {
            await StartSimulation(runner, GameMode.Client, testRoomName);
        }

        private async Task<StartGameResult> StartSimulation( NetworkRunner runner, GameMode mode, string sessionName, int playerCount = 4 ) {

            // Let Fusion know how to handle scene transitions
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if ( sceneManager == null ) {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }


            var sceneIndex = SceneUtility.GetBuildIndexByScenePath(gameplaySceneName);
            var sceneRef = SceneRef.FromIndex(sceneIndex);

            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

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



        public void OnObjectExitAOI( NetworkRunner runner, Fusion.NetworkObject obj, PlayerRef player ) {

        }

        public void OnObjectEnterAOI( NetworkRunner runner, Fusion.NetworkObject obj, PlayerRef player ) {

        }

        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {

        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {

        }

        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) {

        }

        public void OnDisconnectedFromServer( NetworkRunner runner, NetDisconnectReason reason ) {

        }

        public void OnConnectRequest( NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token ) {

        }

        public void OnConnectFailed( NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason ) {

        }

        public void OnReliableDataReceived( NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data ) {

        }

        public void OnReliableDataProgress( NetworkRunner runner, PlayerRef player, ReliableKey key, float progress ) {

        }

        public void OnInput( NetworkRunner runner, NetworkInput input ) {

        }

        public void OnInputMissing( NetworkRunner runner, PlayerRef player, NetworkInput input ) {

        }

        public void OnConnectedToServer( NetworkRunner runner ) {

        }

        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) {

        }

        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) {

        }

        public void OnHostMigration( NetworkRunner runner, HostMigrationToken hostMigrationToken ) {

        }

        public void OnSceneLoadDone( NetworkRunner runner ) {
        }

        public void OnSceneLoadStart( NetworkRunner runner ) {

        }

        /*
        public async Task CreateRoom() {
            await runner.StartGame(new StartGameArgs {
                GameMode = GameMode.Host,
                SessionName = "MyRoom",
                PlayerCount = 4
            });
        }

        public async Task JoinRoom( string roomName ) {
            await runner.StartGame(new StartGameArgs {
                GameMode = GameMode.Host,
                SessionName = roomName
            });
        }
        */



    }
}
