using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Fusion;
using NUnit.Framework;
using Photon.Realtime;
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
            //lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoin;
            //lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
        }


        private void OnDisable() {
            //lobbySessionController.OnPlayerJoinedEvent -= HandlePlayerJoin;
            //lobbySessionController.OnPlayerLeftEvent -= HandlePlayerLeft;
        }

        public void Init() {
            //lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoin;
            //lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
        }


        private void HandlePlayerJoin( NetworkRunner runner ) {
            Debug.Log($"[TableLayout] HandlePlayerJoin fired. LocalPlayer Ref: {runner.LocalPlayer}");

            NetworkDictionary<int, NetworkObject> kvps = seatProvider.GetOccupiedSeats();
            Dictionary<int, TienLenNetWorkPlayer> seatToPlayer = new();

            int localSeatIndex = -1;

            Debug.Log($"[TableLayout] NetworkPlayers count in LobbySessionController: {kvps.Count}");

            if ( kvps.Count == 0 ) return;

            // find local player seat index and build seat to player mapping

            foreach ( var kvp in kvps ) {
                int seatIndex = kvp.Key;
                NetworkObject netObj = kvp.Value;

                if ( netObj == null ) continue;

                var networkPlayer = netObj.GetComponent<TienLenNetWorkPlayer>();
                if ( networkPlayer == null ) {
                    Debug.LogWarning($"[TableLayout] TienLenNetWorkPlayer component is NULL for seat {seatIndex}");
                    continue;
                }

                seatToPlayer[seatIndex] = networkPlayer;

                if ( networkPlayer.PlayerRef == runner.LocalPlayer ) {
                    localSeatIndex = seatIndex;
                    Debug.Log($"[TableLayout] Found local player at seat {seatIndex}");
                }
            }

            if ( localSeatIndex == -1 ) {
                Debug.LogWarning($"[TableLayout] Local player {runner.LocalPlayer} not found in occupied seats.");
                return;
            }


            foreach ( var (networkSeat, player) in seatToPlayer ) {
                if(player == null ) {
                    Debug.LogWarning($"[TableLayout] Player is NULL for seat {networkSeat}");
                    continue;
                }

                int visualSlotIndex = (networkSeat - localSeatIndex + totalSeats) % totalSeats;

                Debug.Log($"[TableLayout] Network Seat {networkSeat} → Visual Slot {visualSlotIndex} (Player: {player.PlayerRef})");
                playerSeats[visualSlotIndex].assignSprite(true);
            }

            /*
            for ( int i = 0; i < kvps.Count; i++ ) {
                var kvp = kvps.Get(i);
                var networkObj = kvp.GetComponent<TienLenNetWorkPlayer>();

                if ( networkObj == null ) {
                    Debug.LogWarning($"[TableLayout] TienLenNetWorkPlayer component is NULL for seat {kvp.Key}");
                    continue;
                }

                seatToPlayer[i] = networkObj;

                if ( networkObj.PlayerRef == runner.LocalPlayer ) {
                    localSeatIndex = kvp.Key;
                    Debug.Log($"[TableLayout] Found local player at seat {kvp.Key}");
                }
            }

            if ( localSeatIndex == -1 ) {
                Debug.LogWarning($"[TableLayout] Local player {runner.LocalPlayer} not found in occupied seats.");
                return;
            }

            // Bind local player to visual slot 0
            foreach ( var kvp in seatToPlayer ) {
                int networkSeat = kvp.Key;
                var player = kvp.Value;
                if ( networkSeat == localSeatIndex )
                    continue;
                int visualSlotIndex = (networkSeat - localSeatIndex + totalSeats) % totalSeats;
                Debug.Log($"[TableLayout] Network Seat {networkSeat} → Visual Slot {visualSlotIndex}");
                playerSeats[visualSlotIndex].BindNetworkPlayer(player);
            }
            */

            /*
            for ( int i = 0; i < totalSeats; i++ ) {
                var networkObj = playerSeats[i].GetComponent<TienLenNetWorkPlayer>();

                if ( networkObj == null ) {
                    Debug.LogWarning($"[TableLayout] TienLenNetWorkPlayer component is NULL for seat {i}");
                    continue;
                }

                if ( networkObj.PlayerRef == runner.LocalPlayer ) {
                    localSeatIndex = i;
                    Debug.Log($"[TableLayout] Found local player at seat {i}");

                    int j = i;
                    while ( j > 0 ) {
                        j--;

                        if ( seatToPlayer.TryGetValue(localSeatIndex, out var localPlayer) ) {
                            playerSeats[j].BindNetworkPlayer(localPlayer);
                        }

                    }
                }
                else {
                    unassignedPlayers.Push(networkObj);
                }

            }

            */


            /*
            // --------------------------------------------------
            // 1. Build Seat -> Player mapping
            // --------------------------------------------------

            foreach ( var networkObj in networkedPlayers ) {
                var player = networkObj.GetComponent<TienLenNetWorkPlayer>();

                if ( player == null ) {
                    Debug.LogWarning(
                        $"[TableLayout] TienLenNetWorkPlayer component is NULL for {player.Id}"
                    );
                    continue;
                }

                Debug.Log($"[TableLayout] GetComponent result: " + $"{player}");

                int seatIndex = player.PlayerSeatIndex;

                Debug.Log(
                    $"[TableLayout] Network Player: {networkObj.Key} " +
                    $"-> SeatIndex: {seatIndex}"
                );

                if ( seatIndex < 0 || seatIndex >= totalSeats ) {
                    Debug.LogWarning(
                        $"[TableLayout] Player {networkObj.Key} has invalid " +
                        $"PlayerSeatIndex: {seatIndex}"
                    );

                    continue;
                }

                seatToPlayer[seatIndex] = player;

                // Find myself
                if ( networkObj.Key == runner.LocalPlayer ) {
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
            */

        }

        private void HandlePlayerLeft( NetworkRunner runner ) {
            // Update the visual layout to reflect the player leaving
            Debug.Log("Player left. Updating table layout.");
            // Implement your logic to update the table layout here


        }

        public void RefreshLayout( NetworkRunner runner ) {
            HandlePlayerJoin(runner);
        }

    }
}