using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.UI;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Assets.Script.TienLen.Game.TienLenGameController;
using static Unity.Collections.Unicode;

namespace Assets.Script.TienLen.Game {
    public class LobbySessionController : NetworkBehaviour {

        [SerializeField] private GameObject networkPlayerPrefab;

        private const int totalSeats = 4;

        [SerializeField] private PlayerSeat[] playerSeats = new PlayerSeat[totalSeats];


        private readonly PlayerRef[] seatAssignments = new PlayerRef[totalSeats];
        private Dictionary<PlayerRef, TienLenNetWorkPlayer> networkPlayers = new();

        public IReadOnlyDictionary<PlayerRef, TienLenNetWorkPlayer> NetworkPlayers => networkPlayers;

        public event Action<NetworkRunner> OnPlayerJoinedEvent;
        public event Action<NetworkRunner> OnPlayerLeftEvent;

        public override void Spawned() {
            if ( Object.HasStateAuthority ) {
                RegisterExistingPlayers();
            }
        }

        public void OnPlayerJoined( PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;

            if ( Runner.ActivePlayers.Count() > totalSeats ) {
                Debug.LogWarning($"Player {player.PlayerId} tried to join, but the lobby is full.");
                return;
            }

            SpawnNetworkPlayer(player);
        }

        public void OnPlayerLeft( PlayerRef player ) {
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

            Debug.Log($"Spawned network player for {player.PlayerId}");
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


    }
}