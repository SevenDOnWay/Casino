using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Fusion;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class TableVisualLayoutManager : MonoBehaviour {

        LobbySessionController lobbySessionController;
        private const int totalSeats = 4;

        [SerializeField]
        private PlayerSeat[] visualSlots = new PlayerSeat[totalSeats];

        [Inject]
        void Construct( LobbySessionController lobbySessionController ) {
            this.lobbySessionController = lobbySessionController;
        }


        private void OnEnable() {
            lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoin;
            lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
        }


        private void OnDisable() {
            lobbySessionController.OnPlayerJoinedEvent -= HandlePlayerJoin;
            lobbySessionController.OnPlayerLeftEvent -= HandlePlayerLeft;
        }


        private void HandlePlayerJoin( NetworkRunner runner ) {
            int localSeatIndex = -1;
            // Update the visual layout to reflect the new player
            // Implement your logic to update the table layout here

            Dictionary<int, TienLenNetWorkPlayer> seatToPlayer = new();

            foreach ( var kvp in lobbySessionController.NetworkPlayers ) {
                var player = kvp.Value;
                seatToPlayer[player.SeatIndex] = player;

                if ( kvp.Key == runner.LocalPlayer ) {
                    localSeatIndex = player.SeatIndex;
                }
            }

            if ( localSeatIndex == -1 ) return;

            bool[] visited = new bool[totalSeats];

            // 1. Mark and bind local player to Slot 0
            visited[localSeatIndex] = true;
            if ( seatToPlayer.TryGetValue(localSeatIndex, out var localPlayer) ) {
                visualSlots[0].BindPlayer(localPlayer);
            }

            // 2. Walk backwards (previous seats in order)
            for ( int step = 1; step < totalSeats; step++ ) {
                int previousSeat = (localSeatIndex - step + totalSeats) % totalSeats;
                if ( visited[previousSeat] ) continue;

                visited[previousSeat] = true;

                if ( seatToPlayer.TryGetValue(previousSeat, out var prevPlayer) ) {
                    int visualSlotIndex = (previousSeat - localSeatIndex + totalSeats) % totalSeats;
                    visualSlots[visualSlotIndex].BindPlayer(prevPlayer);
                }
            }

            // 3. Walk forwards (next seats in order)
            for ( int step = 1; step < totalSeats; step++ ) {
                int nextSeat = (localSeatIndex + step) % totalSeats;
                if ( visited[nextSeat] ) continue;

                visited[nextSeat] = true;

                if ( seatToPlayer.TryGetValue(nextSeat, out var nextPlayer) ) {
                    int visualSlotIndex = (nextSeat - localSeatIndex + totalSeats) % totalSeats;
                    visualSlots[visualSlotIndex].BindPlayer(nextPlayer);
                }
            }


        }

        private void HandlePlayerLeft(NetworkRunner runner) {
            // Update the visual layout to reflect the player leaving
            Debug.Log("Player left. Updating table layout.");
            // Implement your logic to update the table layout here


        }





    }
}