using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class TableVisualLayoutManager : MonoBehaviour {
        [Header("Dependencies")]
        private SeatProvider seatProvider;
        private SeatManager seatManager;
        private ILocalPlayerService localPlayerService;

        private const int TotalSeats = 4;

        [Header("Configuration")]
        [Tooltip("Visual slot index that corresponds to the local player (bottom). Default 0 matches current scene hierarchy.")]
        [SerializeField] private int localAnchorIndex = 0;

        private PlayerSeat[] visualSlots;

        [Inject]
        void Construct( SeatProvider seatProvider,
            SeatManager seatManager,
            ILocalPlayerService localPlayerService ) {
            this.seatProvider = seatProvider;
            this.seatManager = seatManager;
            this.localPlayerService = localPlayerService;
        }

        private void OnEnable() {
            seatManager.OnSeatsChanged += OnSeatsChanged;
        }

        private void OnDisable() {
            seatManager.OnSeatsChanged -= OnSeatsChanged;
        }

        private void OnSeatsChanged() {
            _ = RefreshLayoutAsync();
        }

        /// <summary>
        /// Rebuilds the visual assignment from the network-indexed model.
        /// Uses a diff so unchanged slots are not re-bound.
        /// </summary>
        public async Task RefreshLayoutAsync() {
            visualSlots ??= await seatProvider.GetPlayerSeatsAsync();

            if ( visualSlots == null || visualSlots.Length == 0 ) {
                Debug.LogWarning("[TableLayout] visualSlots is null or empty, skipping layout refresh.");
                return;
            }

            var seatedPlayers = seatManager.GetSeatedPlayers();
            var networkPlayerMap = seatManager.GetNetworkPlayerMap();

            // Resolve local anchor (network seat index of the local player).
            int localSeatIndex = ResolveLocalSeatIndex(networkPlayerMap);

            // Build target mapping: visualSlotIndex -> (networkSeatIndex, TienLenPlayer)
            var targetAssignments = new Dictionary<int, (int netSeat, TienLenPlayer player)>();

            foreach ( var kvp in seatedPlayers ) {
                int netSeat = kvp.Key;
                var logicPlayer = kvp.Value;

                int visualSlotIndex = localSeatIndex == -1
                    ? netSeat
                    : (netSeat - localSeatIndex + TotalSeats) % TotalSeats;

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
                    // This slot should show this network seat.
                    if ( slot.BoundSeatIndex != assignment.netSeat ) {
                        // Different occupant (or first bind) -> bind.
                        slot.BindPlayer(assignment.player, assignment.netSeat);
                    }
                    // else: already shows the correct player -> no-op.
                }
                else {
                    // No occupant for this visual slot -> clear.
                    if ( slot.IsOccupied ) {
                        slot.ClearSeat();
                    }
                }
            }

            Debug.Log($"[TableLayout] Layout refreshed. Local anchor seat={localSeatIndex}, assigned={targetAssignments.Count}");
        }

        /// <summary>
        /// Finds the network seat index of the local player, if any.
        /// </summary>
        private int ResolveLocalSeatIndex( IReadOnlyDictionary<int, TienLenNetWorkPlayer> networkPlayerMap ) {
            var localNetworkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( localNetworkPlayer == null ) return -1;

            foreach ( var kvp in networkPlayerMap ) {
                if ( kvp.Value != null && kvp.Value.PlayerRef == localNetworkPlayer.PlayerRef ) {
                    return kvp.Key;
                }
            }

            return -1;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if ( localAnchorIndex < 0 || localAnchorIndex >= TotalSeats ) {
                Debug.LogWarning($"[TableLayout] localAnchorIndex {localAnchorIndex} out of range [0, {TotalSeats - 1}]");
            }
        }

        private void OnDrawGizmosSelected() {
            if ( visualSlots == null || visualSlots.Length != TotalSeats ) return;

            var positions = new Vector3[TotalSeats];
            for ( int i = 0; i < TotalSeats; i++ ) {
                var slot = visualSlots[i];
                if ( slot != null ) positions[i] = slot.transform.position;
            }

            // Warn if slots are not in clockwise-from-bottom order.
            // (Heuristic: bottom should have lowest y, right highest x, top highest y, left lowest x)
            if ( positions[localAnchorIndex].y > positions[(localAnchorIndex + 2) % TotalSeats].y ) {
                Debug.LogWarning("[TableLayout] Visual slot order may not be clockwise from bottom. " +
                    $"Slot {localAnchorIndex} (anchor) y={positions[localAnchorIndex].y}, " +
                    $"opposite y={positions[(localAnchorIndex + 2) % TotalSeats].y}. " +
                    "Check hierarchy order or adjust localAnchorIndex.");
            }
        }
#endif
    }
}