using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    /// <summary>
    /// Authoritative lobby controller. Handles player joins, leaves,
    /// network player object spawning, and seat registration.
    /// </summary>
    public class SessionController : NetworkBehaviour, INetworkRunnerCallbacks {
        [Header("Scene Dependencies (Serialized / Injected)")]
        [SerializeField] private SeatManager seatManager;
        [SerializeField] private TienLenGameController gameController;

        private IPlayerRegisterService playerRegisterService;
        private ISeatQueryService seatQueryService;

        public IPlayerRegisterService RegisterService => playerRegisterService ?? seatManager;
        public ISeatQueryService SeatQuery => seatQueryService ?? seatManager;
        public TienLenGameController GameController => gameController;

        [Header("Prefab")]
        [SerializeField] private GameObject networkPlayerPrefab;

        [Inject]
        public void Construct(
            IPlayerRegisterService playerRegisterService = null,
            ISeatQueryService seatQueryService = null,
            TienLenGameController gameController = null ) {
            if ( playerRegisterService != null ) this.playerRegisterService = playerRegisterService;
            if ( seatQueryService != null ) this.seatQueryService = seatQueryService;
            if ( gameController != null ) this.gameController = gameController;
        }

        public override void Spawned() {
            Runner.AddCallbacks(this);

            if ( Object.HasStateAuthority && Runner != null && Runner.LocalPlayer.IsValid ) {
                if ( CheckPlayerCanJoin(Runner.LocalPlayer) ) {
                    OnPlayerJoined(Runner, Runner.LocalPlayer);
                }
            }
        }

        public override void Despawned( NetworkRunner runner, bool hasState ) {
            if ( runner != null ) {
                runner.RemoveCallbacks(this);
            }
        }

        private void OnDestroy() {
            if ( Runner != null ) {
                Runner.RemoveCallbacks(this);
            }
        }

        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;

            Debug.Log($"[Lobby] Player joined: {player.PlayerId}");

            if ( runner.IsResume ) {
                if ( SeatQuery != null && SeatQuery.TryGetSeat(player, out _) ) {
                    Debug.Log($"[Lobby] Player {player.PlayerId} already has a restored seat from snapshot.");
                    return;
                }

                if ( SeatQuery != null ) {
                    var networkPlayerMap = SeatQuery.GetNetworkPlayerMap();
                    foreach ( var kvp in networkPlayerMap ) {
                        if ( kvp.Value != null && kvp.Value.PlayerRef == player ) {
                            Debug.Log($"[Lobby] Found matching network player for {player.PlayerId} at seat {kvp.Key}.");
                            return;
                        }
                    }
                }
            }

            if ( !CheckPlayerCanJoin(player) ) return;

            TienLenNetWorkPlayer networkPlayer = SpawnNetworkPlayer(player);
            if ( networkPlayer != null ) {
                RegisterService?.RegisterPlayer(networkPlayer);
                Debug.Log($"[Lobby] Spawned and registered network player '{networkPlayer.PlayerName}' for {player.PlayerId}");
            }
        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;
            RemovePlayer(player);
            GameController?.HandlePlayerLeftMidGame(player);
        }

        private TienLenNetWorkPlayer SpawnNetworkPlayer( PlayerRef player ) {
            TienLenNetWorkPlayer networkPlayer = null;

            var networkPlayerObject = Runner.Spawn(
                networkPlayerPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: player,
                onBeforeSpawned: ( runner, obj ) => {
                    networkPlayer = obj.GetComponent<TienLenNetWorkPlayer>();
                    if ( networkPlayer != null ) {
                        networkPlayer.PlayerRef = player;
                        networkPlayer.PlayerName = $"Player {player.PlayerId}";
                    }
                }
            );

            if ( networkPlayer == null && networkPlayerObject != null ) {
                networkPlayer = networkPlayerObject.GetComponent<TienLenNetWorkPlayer>();
                if ( networkPlayer != null ) {
                    networkPlayer.PlayerRef = player;
                    networkPlayer.PlayerName = $"Player {player.PlayerId}";
                }
            }

            return networkPlayer;
        }

        private bool CheckPlayerCanJoin( PlayerRef player ) {
            if ( SeatQuery != null && SeatQuery.TryGetSeat(player, out _) ) {
                Debug.LogWarning($"[Lobby] Player {player.PlayerId} already has a registered seat. Skipping spawn.");
                return false;
            }

            return true;
        }

        private void RemovePlayer( PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;
            if ( Runner == null ) return;

            Debug.Log($"[Lobby] Player left: {player.PlayerId}");

            NetworkObject netObj = null;
            if ( SeatQuery != null ) {
                foreach ( var kvp in SeatQuery.GetNetworkPlayerMap() ) {
                    if ( kvp.Value != null && kvp.Value.PlayerRef == player ) {
                        netObj = kvp.Value.Object;
                        break;
                    }
                }
            }

            RegisterService?.UnregisterPlayer(player);

            if ( netObj != null ) {
                Runner.Despawn(netObj);
                Debug.Log($"[Lobby] Despawned network player for {player.PlayerId}");
            }
        }

        #region INetworkRunnerCallbacks

        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
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
