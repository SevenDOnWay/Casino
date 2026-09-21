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
        [Header("Dependencies")]
        private SeatProvider seatProvider;
        private LobbySessionController lobbySessionController;


        private const int totalSeats = 4;

        private IReadOnlyList<PlayerSeat> playerSeats;

        [Inject]
        void Construct( LobbySessionController lobbySessionController, SeatProvider seatProvider ) {
            this.lobbySessionController = lobbySessionController;
            this.seatProvider = seatProvider;

            playerSeats = seatProvider.AllSeats;
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
            //Debug.Log($"[TableLayout] HandlePlayerJoin fired. LocalPlayer Ref: {runner.LocalPlayer}");

            int localSeatIndex = -1;
            Dictionary<int, TienLenNetWorkPlayer> seatToPlayer = new();

            //Debug.Log($"[TableLayout] NetworkPlayers count in LobbySessionController: {lobbySessionController.NetworkPlayers.Count}");

            foreach ( var kvp in lobbySessionController.NetworkPlayers ) {
                var player = kvp.Value;
                seatToPlayer[player.SeatIndex] = player;

                //Debug.Log($"[TableLayout] Cached Player: {kvp.Key} -> SeatIndex: {player.SeatIndex}");

                if ( kvp.Key == runner.LocalPlayer ) {
                    localSeatIndex = player.SeatIndex;
                }
            }

            if ( localSeatIndex == -1 ) {
                Debug.LogWarning($"[TableLayout] Local player {runner.LocalPlayer} not found in NetworkPlayers or has unassigned seat (-1). Aborting layout update.");
                return;
            }

            //Debug.Log($"[TableLayout] Local player seat resolved to: {localSeatIndex}");

            bool[] visited = new bool[totalSeats];

            // 1. Mark and bind local player to Slot 0
            visited[localSeatIndex] = true;
            if ( seatToPlayer.TryGetValue(localSeatIndex, out var localPlayer) ) {
                //Debug.Log($"[TableLayout] Binding Local Player (Seat {localSeatIndex}) to Visual Slot 0");
                visualSlots[0].BindNetworkPlayer(localPlayer);
            }
            else {
                Debug.LogWarning($"[TableLayout] Local seat {localSeatIndex} not found in seatToPlayer dictionary!");
            }

            // 2. Walk backwards (previous seats in order)
            for ( int step = 1; step < totalSeats; step++ ) {
                int previousSeat = (localSeatIndex - step + totalSeats) % totalSeats;
                if ( visited[previousSeat] ) {
                    //Debug.Log($"[TableLayout] Step {step} (Backwards): Seat {previousSeat} already visited, skipping.");
                    continue;
                }

                visited[previousSeat] = true;

                if ( seatToPlayer.TryGetValue(previousSeat, out var prevPlayer) ) {
                    int visualSlotIndex = (previousSeat - localSeatIndex + totalSeats) % totalSeats;
                    //Debug.Log($"[TableLayout] Step {step} (Backwards): Bound Player at Seat {previousSeat} to Visual Slot {visualSlotIndex}");
                    visualSlots[visualSlotIndex].BindNetworkPlayer(prevPlayer);
                }
                else {
                    //Debug.Log($"[TableLayout] Step {step} (Backwards): Seat {previousSeat} is empty.");
                }
            }

            // 3. Walk forwards (next seats in order)
            for ( int step = 1; step < totalSeats; step++ ) {
                int nextSeat = (localSeatIndex + step) % totalSeats;
                if ( visited[nextSeat] ) {
                    //Debug.Log($"[TableLayout] Step {step} (Forwards): Seat {nextSeat} already visited, skipping.");
                    continue;
                }

                visited[nextSeat] = true;

                if ( seatToPlayer.TryGetValue(nextSeat, out var nextPlayer) ) {
                    int visualSlotIndex = (nextSeat - localSeatIndex + totalSeats) % totalSeats;
                    //Debug.Log($"[TableLayout] Step {step} (Forwards): Bound Player at Seat {nextSeat} to Visual Slot {visualSlotIndex}");
                    visualSlots[visualSlotIndex].BindNetworkPlayer(nextPlayer);
                }
                else {
                    //Debug.Log($"[TableLayout] Step {step} (Forwards): Seat {nextSeat} is empty.");
                }
            }

            //Debug.Log("[TableLayout] HandlePlayerJoin visual layout processing complete.");
        }

        private void HandlePlayerLeft(NetworkRunner runner) {
            // Update the visual layout to reflect the player leaving
            Debug.Log("Player left. Updating table layout.");
            // Implement your logic to update the table layout here


        }





    }
}