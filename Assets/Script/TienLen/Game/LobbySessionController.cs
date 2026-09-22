using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class LobbySessionController : NetworkBehaviour, INetworkRunnerCallbacks {

        [Header("Dependencies")]
        private SeatProvider seatProvider;

        [Header("Prefab")]
        [SerializeField] private GameObject networkPlayerPrefab;

        [Header("Start Game Button")]
        [SerializeField] private Button startGameBtn;
        [SerializeField] private TMP_Text startBtnText;


        [Header("Player Seats")]
        private IReadOnlyList<PlayerSeat> playerSeats;


        [Header("Network Players")]
        [Networked, Capacity(4)]
        private NetworkArray<NetworkObject> networkedPlayers => default;
        public NetworkArray<NetworkObject> NetworkedPlayers => networkedPlayers;


        private const int minPlayerToStart = 2;
        private const int totalSeats = 4;
        public bool isGameStartable { get; set; }

        [Networked] public NetworkBool IsGameStarted { get; set; }


        public event Action<NetworkRunner> OnPlayerJoinedEvent;
        public event Action<NetworkRunner> OnPlayerLeftEvent;
        public event Action OnGameStartedEvent;

        [Inject]
        void Construct( SeatProvider seatProvider ) {
            this.seatProvider = seatProvider;

            playerSeats = seatProvider.AllSeats;
        }

        public override void Spawned() {
            Runner.AddCallbacks(this);

            if ( Object.HasStateAuthority ) {
                RegisterExistingPlayers();
            }
        }

        public void OnEnable() {
            startGameBtn.onClick.AddListener(OnStartGameButtonClicked);
        }

        public void OnDisable() {
            startGameBtn.onClick.RemoveListener(OnStartGameButtonClicked);
        }

        //TODO: Handle cases player join mid game
        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            UpdateStartButtonUI();

            if ( !Object.HasStateAuthority ) return;

            if ( Runner.ActivePlayers.Count() > totalSeats ) {
                Debug.LogWarning($"Player {player.PlayerId} tried to join, but the lobby is full.");
                return;
            }

            SpawnNetworkPlayer(player);
            RpcPlayerJoined(player);
        }

        //TODO: Handle cases player leave mid game
        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            Debug.Log($"[Lobby] OnPlayerLeft: {player}");

            if ( !Object.HasStateAuthority ) return;

            RemovePlayer(player);
        }

        private void RegisterExistingPlayers() {
            foreach ( var player in Runner.ActivePlayers ) {
                SpawnNetworkPlayer(player);
            }
        }


        //TODO: Handle cases player join mid game
        private void SpawnNetworkPlayer( PlayerRef player ) {
            TienLenNetWorkPlayer networkPlayer = null;

            bool flowControl = CheckPlayerExist(player);
            if ( !flowControl ) return;

            int seatIndex = FindAvailableSeat();
            if ( seatIndex == -1 ) {
                Debug.LogWarning($"Lobby is full. Cannot seat {player.PlayerId}");
                return;
            }

            var networkPlayerObject = Runner.Spawn(networkPlayerPrefab,
                                        Vector3.zero,
                                        Quaternion.identity,
                                        inputAuthority: player,
                                        onBeforeSpawned: (runner, obj) => {
                                            networkPlayer = obj.GetComponent<TienLenNetWorkPlayer>();
                                            networkPlayer.PlayerRef = player;
                                            networkPlayer.PlayerSeatIndex = seatIndex;
                                            networkPlayer.PlayerName = $"Player {player.PlayerId + 1}";
                                        }
                                        );

            var tienLenPlayer = new TienLenPlayer(player.PlayerId,
                player,
                "test"
                );

            networkedPlayers.Set(seatIndex, networkPlayerObject);
            seatProvider.assignSeat(new KeyValuePair<int, NetworkObject>(seatIndex, networkPlayerObject));

            if ( seatProvider != null ) {
                var seat = seatProvider.GetSeat(seatIndex);
                if ( seat != null ) {
                    seat.BindNetworkPlayer(networkPlayer);
                    seat.BindLogicPlayer(tienLenPlayer);
                }
            }

            Debug.Log($"[Lobby] Spawned network player for {player.PlayerId}");

            CheckPlayer();
        }

        private bool CheckPlayerExist( PlayerRef player ) {
            foreach ( var existingPlayer in networkedPlayers ) {
                if ( existingPlayer != null && existingPlayer.TryGetComponent<TienLenNetWorkPlayer>(out var existingNetworkPlayer) ) {
                    if ( existingNetworkPlayer.PlayerRef == player ) {
                        Debug.LogWarning($"Player {player.PlayerId} already has a network player object. Skipping spawn.");
                        return false;
                    }
                }
            }

            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcPlayerJoined( PlayerRef player ) {
            Debug.Log($"[Lobby] RpcPlayerJoined: {player.PlayerId}");
            OnPlayerJoinedEvent?.Invoke(Runner);
        }

        //TODO: Handle cases player leave mid game
        private void RemovePlayer( PlayerRef player ) {
            //if ( networkedPlayers.TryGet(player, out var networkPlayer) ) {
            //    Runner.Despawn(networkPlayer);
            //    networkedPlayers.Remove(player);
            //    OnPlayerLeftEvent?.Invoke(Runner);
            //}
        }

        #region Start Game Button Logic
        private void UpdateStartButtonUI() {
            Debug.Log(
                    $"[StartUI] " +
                    $"Runner={Runner.name}, " +
                    $"LocalPlayer={Runner.LocalPlayer}, " +
                    $"IsServer={Runner.IsServer}, " +
                    $"HasStateAuthority={Object.HasStateAuthority}, " +
                    $"IsGameStarted={IsGameStarted}"
                );

            if ( startGameBtn == null ) return;

            // Hide the button for everyone once the game has started
            if ( IsGameStarted ) {
                startGameBtn.gameObject.SetActive(false);
                return;
            }

            // Keep visible for everyone before the match starts
            startGameBtn.gameObject.SetActive(true);

            // ONLY the host can click it, and ONLY if enough players joined
            bool isHost = Runner.IsServer;
            startGameBtn.interactable = isHost && isGameStartable;

            // Optional: Provide visual feedback text
            if ( startBtnText != null ) {
                if ( !isHost ) {
                    startBtnText.text = "Waiting for Host to start...";
                }
                else if ( !isGameStartable ) {
                    startBtnText.text = $"Need {minPlayerToStart - Runner.ActivePlayers.Count()} more to start";
                }
                else {
                    startBtnText.text = "Start Game";
                }
            }
        }

        private void OnStartGameButtonClicked() {

            Debug.Log($"[StartGame] Start button clicked, IsGameStarted={IsGameStarted}, isGameStartable={isGameStartable}");

            if ( !Object.HasStateAuthority ) return;
            if ( !isGameStartable ) return;
            if ( IsGameStarted ) return;

            Debug.Log("[StartGame] Host starting authoritative game state.");

            IsGameStarted = true;

            OnGameStartedEvent?.Invoke();
        }

        #endregion

        private int FindAvailableSeat() {
            for ( int i = 0; i < totalSeats; i++ ) {
                if ( playerSeats[i].isOccupied ) continue;
                return i;
            }

            return -1;
        }

        private void CheckPlayer() {
            int currentConnectedPlayers = Runner.ActivePlayers.Count();

            Debug.Log($"[TienLen] Current connected players: {currentConnectedPlayers}/{minPlayerToStart}");

            if ( currentConnectedPlayers >= minPlayerToStart ) {
                Debug.Log($"[TienLen] Player threshold reached ({currentConnectedPlayers}/{minPlayerToStart}). Starting game...");
                isGameStartable = true;
            }
            else {
                Debug.Log($"[TienLen] Waiting for more players... ({currentConnectedPlayers}/{minPlayerToStart})");
                isGameStartable = false;
            }

            UpdateStartButtonUI();
        }

        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }

        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }

        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) { }

        public void OnDisconnectedFromServer( NetworkRunner runner, NetDisconnectReason reason ) { }

        public void OnConnectRequest( NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token ) { }

        public void OnConnectFailed( NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason ) { }

        public void OnReliableDataReceived( NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data ) { }

        public void OnReliableDataProgress( NetworkRunner runner, PlayerRef player, ReliableKey key, float progress ) { }

        public void OnInput( NetworkRunner runner, NetworkInput input ) { }

        public void OnInputMissing( NetworkRunner runner, PlayerRef player, NetworkInput input ) { }

        public void OnConnectedToServer( NetworkRunner runner ) { }

        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) { }

        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) { }

        public void OnHostMigration( NetworkRunner runner, HostMigrationToken hostMigrationToken ) { }

        public void OnSceneLoadDone( NetworkRunner runner ) { }

        public void OnSceneLoadStart( NetworkRunner runner ) { }
    }
}