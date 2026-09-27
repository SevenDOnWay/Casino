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
        private ILocalPlayerService localPlayerService;

        private const int totalSeats = 4;

        private IReadOnlyList<PlayerSeat> playerSeats;

        [Inject]
        void Construct( SeatProvider seatProvider,
            SeatManager seatManager,
            ILocalPlayerService localPlayerService ) {
            this.seatProvider = seatProvider;
            this.seatManager = seatManager;
            this.localPlayerService = localPlayerService;
        }

        public void Init() {
        }



        private async Task HandlePlayerJoin() {
            Debug.Log($"[TableLayout] HandlePlayerJoin fired.");

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
                Debug.LogWarning($"[TableLayout] Local seat not found. Falling back to unrotated bind for avatar rendering.");
            }

            BindToLocalSeat(seatToPlayer, localSeatIndex);
        }

        private void BindToLocalSeat( Dictionary<int, TienLenNetWorkPlayer> seatToPlayer, int localSeatIndex ) {
            // Clear-first: drop stale occupants so slots left by departed
            // players do not keep showing old avatars.
            var occupiedSlots = new HashSet<int>();
            foreach ( var (networkSeat, player) in seatToPlayer ) {
                if ( player == null ) continue;
                int slot = localSeatIndex == -1
                    ? networkSeat
                    : (networkSeat - localSeatIndex + totalSeats) % totalSeats;
                occupiedSlots.Add(slot);
            }
            for ( int i = 0; i < playerSeats.Count; i++ ) {
                if ( !occupiedSlots.Contains(i) && playerSeats[i] != null ) {
                    playerSeats[i].ClearSeat();
                }
            }

            foreach ( var (networkSeat, player) in seatToPlayer ) {
                if ( player == null ) {
                    Debug.LogWarning($"[TableLayout] Player is NULL for seat {networkSeat}");
                    continue;
                }

                int visualSlotIndex = localSeatIndex == -1
                    ? networkSeat
                    : (networkSeat - localSeatIndex + totalSeats) % totalSeats;

                var tienlenPlayer = new TienLenPlayer(player);

                if ( visualSlotIndex < 0 || visualSlotIndex >= playerSeats.Count || playerSeats[visualSlotIndex] == null ) {
                    Debug.LogWarning($"[TableLayout] Visual slot {visualSlotIndex} out of range or null, skipping.");
                    continue;
                }
                playerSeats[visualSlotIndex].BindPlayer(tienlenPlayer);
            }
        }

        private int FindLocalSeatIndex( NetworkDictionary<int, NetworkObject> networkDictionary, Dictionary<int, TienLenNetWorkPlayer> seatToPlayer, out int localSeatIndex ) {
            localSeatIndex = -1;

            var localNetworkPlayer = localPlayerService?.GetLocalNetworkPlayer();
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
            RefreshLayout();
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