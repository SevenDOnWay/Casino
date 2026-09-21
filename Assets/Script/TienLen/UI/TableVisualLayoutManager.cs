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
            Debug.Log($"[TableLayout] HandlePlayerJoin fired. LocalPlayer Ref: {runner.LocalPlayer}");

            Dictionary<int, TienLenNetWorkPlayer> seatToPlayer = new();
            var networkedPlayers  = lobbySessionController.NetworkedPlayers;
            int localSeatIndex = -1;

            Debug.Log($"[TableLayout] NetworkPlayers count in LobbySessionController: {networkedPlayers.Count}");

            // --------------------------------------------------
            // 1. Build Seat -> Player mapping
            // --------------------------------------------------

            foreach ( var kvp in networkedPlayers ) {
                var networkObject = kvp.Value;

                if ( networkObject == null ) {
                    Debug.LogWarning(
                        $"[TableLayout] NetworkObject for {kvp.Key} is null."
                    );

                    continue;
                }

                var player = networkObject.GetComponent<TienLenNetWorkPlayer>();

                if ( player == null ) {
                    Debug.LogWarning(
                        $"[TableLayout] NetworkObject for {kvp.Key} " +
                        $"does not contain TienLenNetWorkPlayer."
                    );

                    continue;
                }

                int seatIndex = player.PlayerSeatIndex;

                Debug.Log(
                    $"[TableLayout] Network Player: {kvp.Key} " +
                    $"-> SeatIndex: {seatIndex}"
                );

                if ( seatIndex < 0 || seatIndex >= totalSeats ) {
                    Debug.LogWarning(
                        $"[TableLayout] Player {kvp.Key} has invalid " +
                        $"PlayerSeatIndex: {seatIndex}"
                    );

                    continue;
                }

                seatToPlayer[seatIndex] = player;

                // Find myself
                if ( kvp.Key == runner.LocalPlayer ) {
                    localSeatIndex = seatIndex;
                }
            }

            // --------------------------------------------------
            // 2. Make sure local player exists
            // --------------------------------------------------

            if ( localSeatIndex == -1 ) {
                Debug.LogWarning(
                    $"[TableLayout] Local player {runner.LocalPlayer} " +
                    $"not found in NetworkedPlayers."
                );

                return;
            }

            Debug.Log(
                $"[TableLayout] Local player seat resolved to: " +
                $"{localSeatIndex}"
            );


            // --------------------------------------------------
            // 3. Local player → Visual Slot 0
            // --------------------------------------------------

            if ( seatToPlayer.TryGetValue(
                    localSeatIndex,
                    out var localPlayer) ) {
                Debug.Log(
                    $"[TableLayout] Binding Local Player " +
                    $"Network Seat {localSeatIndex} → Visual Slot 0"
                );

                playerSeats[0].BindNetworkPlayer(localPlayer);
            }

            // --------------------------------------------------
            // 4. Calculate every other player's local slot
            // --------------------------------------------------

            foreach ( var kvp in seatToPlayer ) {
                int networkSeat = kvp.Key;
                var player = kvp.Value;

                // Skip local player
                if ( networkSeat == localSeatIndex )
                    continue;

                int visualSlotIndex =
            (networkSeat - localSeatIndex + totalSeats)
            % totalSeats;

                Debug.Log(
                    $"[TableLayout] Network Seat {networkSeat} " +
                    $"→ Visual Slot {visualSlotIndex}"
                );

                playerSeats[visualSlotIndex]
                    .BindNetworkPlayer(player);
            }

            Debug.Log(
                "[TableLayout] Visual layout processing complete."
            );
        }

        private void HandlePlayerLeft(NetworkRunner runner) {
            // Update the visual layout to reflect the player leaving
            Debug.Log("Player left. Updating table layout.");
            // Implement your logic to update the table layout here


        }





    }
}