using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.UI;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Assets.Script.TienLen.Game.TienLenGameController;
using static Unity.Collections.Unicode;

namespace Assets.Script.TienLen.Game {
    public class LobbySessionController : NetworkBehaviour, INetworkRunnerCallbacks {

        [SerializeField] private GameObject networkPlayerPrefab;

        private const int totalSeats = 4;

        [SerializeField] private PlayerSeat[] playerSeats = new PlayerSeat[totalSeats];


        private readonly PlayerRef[] seatAssignments = new PlayerRef[totalSeats];
        private Dictionary<PlayerRef, TienLenNetWorkPlayer> networkPlayers = new();

        public IReadOnlyDictionary<PlayerRef, TienLenNetWorkPlayer> NetworkPlayers => networkPlayers;

        public event Action<NetworkRunner> OnPlayerJoinedEvent;
        public event Action<NetworkRunner> OnPlayerLeftEvent;

        public override void Spawned() {
            Runner.AddCallbacks(this);

            if ( Object.HasStateAuthority ) {
                RegisterExistingPlayers();
            }
        }


        //TODO: Handle cases player join mid game
        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            Debug.Log($"OnPlayerJoined fired for player: {player.PlayerId} | HasStateAuthority: {Object.HasStateAuthority}");
            if ( !Object.HasStateAuthority ) return;

            if ( Runner.ActivePlayers.Count() > totalSeats ) {
                Debug.LogWarning($"Player {player.PlayerId} tried to join, but the lobby is full.");
                return;
            }

            SpawnNetworkPlayer(player);
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
            if ( networkPlayers.ContainsKey(player) ) return;

            int seatIndex = FindAvailableSeat();
            if ( seatIndex == -1 ) {
                Debug.LogWarning($"Lobby is full. Cannot seat {player.PlayerId}");
                return;
            }

            seatAssignments[seatIndex] = player;

            var networkPlayerObject = Runner.Spawn(networkPlayerPrefab, Vector3.zero, Quaternion.identity, player);
            var networkPlayer = networkPlayerObject.GetComponent<TienLenNetWorkPlayer>();

            networkPlayer.SeatIndex = seatIndex;
            networkPlayer.PlayerRef = player;

            networkPlayers.Add(player, networkPlayer);
            OnPlayerJoinedEvent?.Invoke(Runner);

            Debug.Log($"[Lobby] Spawned network player for {player.PlayerId}");
        }

        //TODO: Handle cases player leave mid game
        private void RemovePlayer( PlayerRef player ) {
            if ( networkPlayers.TryGetValue(player, out var networkPlayer) ) {
                Runner.Despawn(networkPlayer.Object);
                networkPlayers.Remove(player);
                OnPlayerLeftEvent?.Invoke(Runner);
            }
        }

        private int FindAvailableSeat() {
            for ( int i = 0; i < totalSeats; i++ ) {
                if ( playerSeats[i].IsOccupied ) continue;
                return i;
            }

            return -1;
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