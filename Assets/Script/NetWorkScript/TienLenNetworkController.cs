using Fusion;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetworkController : MonoBehaviour {
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private string gameplaySceneName;

        private NetworkRunner runner;

        public async void Start() {
            runner = Instantiate(runnerPrefab);

            //await runner.StartGame(new StartGameArgs {
            //    GameMode = GameMode.AutoHostOrClient,
            //    SessionName = "TienLenTest",
            //    PlayerCount = 4
            //});
        }


        public async void HostRoomButton() {
            await StartSimulation(GameMode.Host, "MyRoom", 4);
        }

        public async void JoinRoomButton( string roomName ) {
            await StartSimulation(GameMode.Client, roomName);
        }

        private async Task StartSimulation( GameMode mode, string sessionName, int playerCount = 4 ) {
            // Clean up existing runner if one already exists
            if ( runner == null ) {
                runner = Instantiate(runnerPrefab);
            }

            // Let Fusion know how to handle scene transitions
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
        }

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


    }
}
