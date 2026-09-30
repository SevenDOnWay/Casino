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
    public class LobbySessionController : NetworkBehaviour, INetworkRunnerCallbacks {
        [Header("Dependencies")]
        private IPlayerRegisterService playerRegisterService;
        private ISeatQueryService seatQueryService;

        [Header("Prefab")]
        [SerializeField] private GameObject networkPlayerPrefab;

        [Inject]
        void Construct( IPlayerRegisterService playerRegisterService, ISeatQueryService seatQueryService ) {
            this.playerRegisterService = playerRegisterService;
            this.seatQueryService = seatQueryService;
        }

        public override void Spawned() {
            Runner.AddCallbacks(this);
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

            if ( !CheckPlayerCanJoin(player) ) return;

            TienLenNetWorkPlayer networkPlayer = SpawnNetworkPlayer(player);
            if ( networkPlayer != null ) {
                playerRegisterService.RegisterPlayer(networkPlayer);
                Debug.Log($"[Lobby] Spawned and registered network player '{networkPlayer.PlayerName}' for {player.PlayerId}");
            }
        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;
            RemovePlayer(player);
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

            return networkPlayer;
        }

        private bool CheckPlayerCanJoin( PlayerRef player ) {
            if ( seatQueryService != null && seatQueryService.TryGetSeat(player, out _) ) {
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
            if ( seatQueryService != null ) {
                foreach ( var kvp in seatQueryService.GetNetworkPlayerMap() ) {
                    if ( kvp.Value != null && kvp.Value.PlayerRef == player ) {
                        netObj = kvp.Value.Object;
                        break;
                    }
                }
            }

            playerRegisterService?.UnregisterPlayer(player);

            if ( netObj != null ) {
                Runner.Despawn(netObj);
                Debug.Log($"[Lobby] Despawned network player for {player.PlayerId}");
            }
        }

        #region INetworkRunnerCallbacks Boilerplate

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

        #endregion
    }
}
