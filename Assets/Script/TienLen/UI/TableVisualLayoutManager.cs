using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Fusion;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    /// <summary>
    /// Dumb view: renders seat data pushed down by UiManager.
    /// Maps network seat indices to visual slots using SeatProvider.
    /// </summary>
    public class TableVisualLayoutManager : MonoBehaviour {
        [Header("Dependencies")]
        private SeatProvider seatProvider;

        private PlayerSeat[] visualSlots;

        // Latest data pushed down by UiManager
        private IReadOnlyDictionary<int, TienLenPlayer> latestSeatedPlayers;
        private IReadOnlyDictionary<int, TienLenNetWorkPlayer> latestNetworkPlayers;
        private PlayerRef latestLocalPlayer;

        [Inject]
        void Construct( SeatProvider seatProvider ) {
            this.seatProvider = seatProvider;
        }

        /// <summary>
        /// Single entry point. UiManager calls this with fresh data whenever a seat change occurs.
        /// </summary>
        public void RenderSeats(
            IReadOnlyDictionary<int, TienLenPlayer> seatedPlayers,
            IReadOnlyDictionary<int, TienLenNetWorkPlayer> networkPlayers,
            PlayerRef localPlayer ) {
            latestSeatedPlayers = seatedPlayers;
            latestNetworkPlayers = networkPlayers;
            latestLocalPlayer = localPlayer;

            RefreshLayout();
        }

        /// <summary>
        /// Rebuilds the visual assignment synchronously from the last pushed-down data.
        /// Uses a diff so unchanged slots are not re-bound.
        /// </summary>
        public void RefreshLayout() {
            if ( seatProvider == null ) {
                Debug.LogWarning("[TableLayout] seatProvider is null, skipping layout refresh.");
                return;
            }

            visualSlots ??= seatProvider.GetPlayerSeats();

            if ( visualSlots == null || visualSlots.Length == 0 ) {
                Debug.LogWarning("[TableLayout] visualSlots is null or empty, skipping layout refresh.");
                return;
            }

            var seatedPlayers = latestSeatedPlayers ?? new Dictionary<int, TienLenPlayer>();

            int localSeatIndex = ResolveLocalSeatIndex();

            // Build target mapping: visualSlotIndex -> (networkSeatIndex, TienLenPlayer)
            var targetAssignments = new Dictionary<int, (int netSeat, TienLenPlayer player)>();

            foreach ( var kvp in seatedPlayers ) {
                int netSeat = kvp.Key;
                var logicPlayer = kvp.Value;

                int visualSlotIndex = seatProvider.CalculateVisualSlotIndex(netSeat, localSeatIndex);

                if ( visualSlotIndex < 0 || visualSlotIndex >= visualSlots.Length ) {
                    Debug.LogWarning($"[TableLayout] Visual slot {visualSlotIndex} out of range for net seat {netSeat}, skipping.");
                    continue;
                }

                targetAssignments[visualSlotIndex] = (netSeat, logicPlayer);
            }

            // Diff: only touch slots that actually changed.
            for ( int i = 0; i < visualSlots.Length; i++ ) {
                var slot = visualSlots[i];
                if ( slot == null ) continue;

                if ( targetAssignments.TryGetValue(i, out var assignment) ) {
                    if ( slot.BoundSeatIndex != assignment.netSeat ||
                        !ReferenceEquals(slot.tienLenPlayer, assignment.player) ) {
                        slot.BindPlayer(assignment.player, assignment.netSeat);
                    }
                }
                else {
                    if ( slot.IsOccupied ) {
                        slot.ClearSeat();
                    }
                }
            }

            Debug.Log($"[TableLayout] Layout refreshed. Local anchor seat={localSeatIndex}, assigned={targetAssignments.Count}");
        }

        /// <summary>
        /// Finds the network seat index of the local player in the last pushed data.
        /// </summary>
        private int ResolveLocalSeatIndex() {
            if ( latestNetworkPlayers == null ) return -1;
            if ( !latestLocalPlayer.IsValid ) return -1;

            foreach ( var kvp in latestNetworkPlayers ) {
                var netPlayer = kvp.Value;
                if ( netPlayer == null ) continue;

                if ( netPlayer.Object != null
                    && netPlayer.Object.IsValid
                    && netPlayer.Object.InputAuthority == latestLocalPlayer ) {
                    return kvp.Key;
                }

                if ( netPlayer.PlayerRef == latestLocalPlayer ) {
                    return kvp.Key;
                }
            }

            return -1;
        }

        public PlayerSeat GetSeatByNetworkIndex( int networkSeatIndex ) {
            if ( visualSlots == null || networkSeatIndex < 0 ) return null;
            for ( int i = 0; i < visualSlots.Length; i++ ) {
                if ( visualSlots[i] != null && visualSlots[i].BoundSeatIndex == networkSeatIndex ) {
                    return visualSlots[i];
                }
            }
            return null;
        }

        public CardHolder GetCardHolderForSeat( int networkSeatIndex ) {
            return GetSeatByNetworkIndex(networkSeatIndex)?.CardHolder;
        }

        private void EnsureVisualSlots() {
            if ( visualSlots == null || visualSlots.Length == 0 ) {
                if ( seatProvider != null ) {
                    visualSlots = seatProvider.GetPlayerSeats();
                }
                else {
                    seatProvider = GetComponent<SeatProvider>() ?? GetComponentInParent<SeatProvider>() ?? FindAnyObjectByType<SeatProvider>();
                    visualSlots = seatProvider != null ? seatProvider.GetPlayerSeats() : FindObjectsByType<PlayerSeat>(FindObjectsSortMode.None);
                }
            }
        }

        public PlayerSeat GetVisualSlot( int visualIndex ) {
            EnsureVisualSlots();
            if ( visualSlots == null || visualIndex < 0 || visualIndex >= visualSlots.Length ) return null;
            return visualSlots[visualIndex];
        }

        public PlayerSeat GetLocalSeat() {
            return GetVisualSlot(0);
        }

        public CardHolder GetLocalCardHolder() {
            return GetLocalSeat()?.CardHolder;
        }

        public IReadOnlyList<PlayerSeat> GetAllVisualSlots() {
            EnsureVisualSlots();
            return visualSlots;
        }
    }
}
