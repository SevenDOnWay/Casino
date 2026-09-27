using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Fusion;
using NUnit.Framework;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class TableVisualLayoutManager : MonoBehaviour {
        [Header("Dependencies")]
        private SeatProvider seatProvider;
        private SeatManager seatManager;
        private LobbySessionController lobbySessionController;
        private ILocalPlayerService localPlayerService;

        private const int totalSeats = 4;

        private IReadOnlyList<PlayerSeat> playerSeats;

        [Inject]
        void Construct( SeatProvider seatProvider,
            SeatManager seatManager,
            LobbySessionController lobbySessionController,
            ILocalPlayerService localPlayerService ) {
            this.seatProvider = seatProvider;
            this.lobbySessionController = lobbySessionController;
            this.seatManager = seatManager;
            this.localPlayerService = localPlayerService;

            if ( seatProvider == null ) Debug.LogWarning("[TableLayout] Construct: seatProvider is null.");
            if ( seatManager == null ) Debug.LogWarning("[TableLayout] Construct: seatManager is null.");
            if ( lobbySessionController == null ) Debug.LogWarning("[TableLayout] Construct: lobbySessionController is null.");
            if ( localPlayerService == null ) Debug.LogWarning("[TableLayout] Construct: localPlayerService is null.");

            //playerSeats = seatProvider.AllSeats;
        }

        public void Init() {
            //lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoin;
            //lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
        }



        private async Task HandlePlayerJoin() {
            Debug.Log($"[TableLayout] HandlePlayerJoin fired.");

            if ( seatManager == null ) {
                Debug.LogWarning("[TableLayout] seatManager is null, skipping layout refresh.");
                return;
            }
            if ( seatProvider == null ) {
                Debug.LogWarning("[TableLayout] seatProvider is null, skipping layout refresh.");
                return;
            }
            if ( localPlayerService == null ) {
                Debug.LogWarning("[TableLayout] localPlayerService is null, skipping layout refresh.");
                return;
            }

            NetworkDictionary<int, NetworkObject> networkDictionary = seatManager.GetNetworkOccupiedSeats();
            Dictionary<int, TienLenNetWorkPlayer> seatToPlayer = new Dictionary<int, TienLenNetWorkPlayer>();
            playerSeats ??= await seatProvider.GetPlayerSeatsAsync();
            if ( playerSeats == null || playerSeats.Count == 0 ) {
                Debug.LogWarning("[TableLayout] playerSeats is null or empty, skipping layout refresh.");
                return;
            }

            Debug.Log($"[TableLayout] NetworkPlayers count in LobbySessionController: {networkDictionary.Count}");

            FindLocalSeatIndex(networkDictionary, seatToPlayer, out int localSeatIndex);

            if ( localSeatIndex == -1 ) {
                var localNetworkPlayer = localPlayerService.GetLocalNetworkPlayer();
                if ( localNetworkPlayer == null ) {
                    Debug.LogWarning("[TableLayout] Local network player not set yet");
                }
                else {
                    Debug.LogWarning($"[TableLayout] Local player {localNetworkPlayer.PlayerRef} not found in occupied seats.");
                }
                // Fall back to rendering seat 0 as local so avatar still appears for host.
                if ( seatToPlayer.Count > 0 ) {
                    using var enumerator = seatToPlayer.Keys.GetEnumerator();
                    enumerator.MoveNext();
                    localSeatIndex = enumerator.Current;
                }
                else {
                    localSeatIndex = 0;
                }
                Debug.Log($"[TableLayout] Falling back to localSeatIndex={localSeatIndex} for avatar rendering.");
            }

            BindToLocalSeat(seatToPlayer, localSeatIndex);
        }

        private void BindToLocalSeat( Dictionary<int, TienLenNetWorkPlayer> seatToPlayer, int localSeatIndex ) {
            if ( seatToPlayer == null ) {
                Debug.LogWarning("[TableLayout] seatToPlayer is null, skipping bind.");
                return;
            }
            if ( playerSeats == null || playerSeats.Count == 0 ) {
                Debug.LogWarning("[TableLayout] playerSeats is null or empty, skipping bind.");
                return;
            }
            foreach ( var (networkSeat, player) in seatToPlayer ) {
                if ( player == null ) {
                    Debug.LogWarning($"[TableLayout] Player is NULL for seat {networkSeat}");
                    continue;
                }

                int visualSlotIndex = (networkSeat - localSeatIndex + totalSeats) % totalSeats;

                var tienlenPlayer = new TienLenPlayer(player);

                Debug.Log($"[TableLayout] Network Seat {networkSeat} → Visual Slot {visualSlotIndex} (Player: {player.PlayerRef})");
                if ( visualSlotIndex < 0 || visualSlotIndex >= playerSeats.Count || playerSeats[visualSlotIndex] == null ) {
                    Debug.LogWarning($"[TableLayout] Visual slot {visualSlotIndex} out of range or null, skipping.");
                    continue;
                }
                playerSeats[visualSlotIndex].BindPlayer(tienlenPlayer);
            }
        }

        private int FindLocalSeatIndex( NetworkDictionary<int, NetworkObject> networkDictionary, Dictionary<int, TienLenNetWorkPlayer> seatToPlayer, out int localSeatIndex ) {
            localSeatIndex = -1;
            if ( seatToPlayer == null ) {
                Debug.LogWarning("[TableLayout] FindLocalSeatIndex: null map.");
                return localSeatIndex;
            }
            if ( localPlayerService == null ) {
                Debug.LogWarning("[TableLayout] FindLocalSeatIndex: localPlayerService is null.");
                return localSeatIndex;
            }
            var localNetworkPlayer = localPlayerService.GetLocalNetworkPlayer();
            bool hasLocal = localNetworkPlayer != null;
            foreach ( var kvp in networkDictionary ) {
                int seatIndex = kvp.Key;
                NetworkObject netObj = kvp.Value;
                if ( netObj == null ) continue;

                var networkPlayer = netObj.GetComponent<TienLenNetWorkPlayer>();
                if ( networkPlayer == null ) continue;

                seatToPlayer[seatIndex] = networkPlayer;

                if ( hasLocal && networkPlayer.PlayerRef == localNetworkPlayer.PlayerRef ) {
                    localSeatIndex = seatIndex;
                    Debug.Log($"[TableLayout] Found local player at seat {seatIndex}");
                }
            }

            return localSeatIndex;
        }

        private void HandlePlayerLeft( NetworkRunner runner ) {
            // Update the visual layout to reflect the player leaving
            Debug.Log("Player left. Updating table layout.");
            // Implement your logic to update the table layout here


        }


        //TODO: This method shuold handle both player join and leave events, and update the visual layout accordingly.
        // Note: HandlePlayerJoin returns Task (not async void) so this fire-and-forget
        // call is intentional; failures are logged inside HandlePlayerJoin.
        public void RefreshLayout() {
            Debug.Log("[TableLayout] RefreshLayout called.");
            _ = HandlePlayerJoin();
        }

    }
}