using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Fusion;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class SeatManager : NetworkBehaviour, IPlayerRegisterService {
        [Header("Dependencies")]
        SeatProvider seatProvider;

        private const int TotalSeats = 4;
        private const float RetryDelay = 0.5f;
        private const int MaxRetryAttempts = 20;

        /// <summary>
        /// A networked dictionary that maps seat indices to the NetworkObject of the player occupying that seat.
        /// Client will receive updates when players join or leave seats, allowing for real-time synchronization of seat occupancy across the network.
        /// </summary>
        [Networked, Capacity(TotalSeats), OnChangedRender(nameof(OnOccupiedSeatsChanged))]
        public NetworkDictionary<int, NetworkObject> networkOccupiedSeats => default;

        /// <summary>
        /// Model: network seat index -> stable TienLenPlayer instance.
        /// This is the single owner of player instances; hands survive rebinds.
        /// </summary>
        private readonly Dictionary<int, TienLenPlayer> seatedPlayers = new();

        /// <summary>
        /// Monotonic counter bumped every time the seat model actually changes.
        /// UI detects changes by polling this instead of subscribing to events.
        /// </summary>
        public int SeatRevision { get; private set; }

        /// <summary>
        /// True while a rebuild saw refs that have not spawned locally yet.
        /// UiManager re-renders while this is set, so seats appear as soon as
        /// their objects resolve even when no further network change arrives.
        /// </summary>
        public bool HasUnresolvedSeats => retryPending;

        /// <summary>
        /// Entries whose objects have spawned locally and can actually be
        /// rendered. Allocation-free, so UiManager can poll it every frame.
        /// </summary>
        public int ResolvedSeatCount {
            get {
                if ( Object == null || !Object.IsValid ) return 0;

                int count = 0;
                foreach ( var kvp in networkOccupiedSeats ) {
                    if ( kvp.Value == null ) continue;
                    if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out _) ) {
                        count++;
                    }
                }

                return count;
            }
        }

        private bool retryPending;
        private float retryAt;
        private int retryAttempts;

        [Inject]
        void Construct( SeatProvider seatProvider ) {
            this.seatProvider = seatProvider;
        }

        public override void Spawned() {
            base.Spawned();
            // OnChanged does not fire for the initial snapshot state, so
            // rebuild once here to pick up seats that already replicated
            // (e.g. late-joining client).
            _ = OnOccupiedSeatsChangedAsync();
        }

        private void Update() {
            // Retry a rebuild whose NetworkObject refs were not yet resolved.
            if ( retryPending && Time.time >= retryAt ) {
                retryPending = false;
                if ( retryAttempts >= MaxRetryAttempts ) {
                    Debug.LogWarning("[SeatManager] Gave up waiting for unresolved seat objects.");
                }
                else {
                    _ = OnOccupiedSeatsChangedAsync();
                }
            }

            // Self-heal: the model must mirror every resolvable dict entry.
            // Covers Spawned running before the snapshot arrived (empty dict,
            // so no retry was scheduled) and any otherwise missed delta.
            // Both counts are local reads; the rebuild stops once caught up.
            if ( !retryPending && seatedPlayers.Count != ResolvedSeatCount ) {
                _ = OnOccupiedSeatsChangedAsync();
            }
        }

        public NetworkDictionary<int, NetworkObject> GetNetworkOccupiedSeats() {
            return networkOccupiedSeats;
        }

        public void RegisterPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            _ = RegisterPlayerAsync(tienLenNetWorkPlayer);
        }

        public void UnregisterPlayer( PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;

            int foundSeat = -1;
            foreach ( var kvp in networkOccupiedSeats ) {
                if ( kvp.Value != null && kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var netPlayer) ) {
                    if ( netPlayer.PlayerRef == player ) {
                        foundSeat = kvp.Key;
                        break;
                    }
                }
            }

            if ( foundSeat != -1 ) {
                networkOccupiedSeats.Remove(foundSeat);
                Debug.Log($"[SeatManager] Unregistered seat {foundSeat} for {player.PlayerId}");
                return;
            }

            Debug.LogWarning($"[SeatManager] No seat found for leaving player {player.PlayerId}");
        }

        public bool TryGetSeat( PlayerRef player, out int seatIndex ) {
            foreach ( var kvp in networkOccupiedSeats ) {
                if ( kvp.Value != null && kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var netPlayer) ) {
                    if ( netPlayer.PlayerRef == player ) {
                        seatIndex = kvp.Key;
                        return true;
                    }
                }
            }

            seatIndex = -1;
            return false;
        }

        public async Task RegisterPlayerAsync( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( tienLenNetWorkPlayer == null ) return;
            if ( !CheckPlayerRegistered(tienLenNetWorkPlayer) ) return;

            int seatIndex = await FindAvailableSeat();
            if ( seatIndex == -1 ) {
                Debug.LogWarning("[SeatManager] No available seat for " + tienLenNetWorkPlayer.PlayerRef);
                return;
            }

            networkOccupiedSeats.Add(seatIndex, tienLenNetWorkPlayer.Object);
        }

        private bool CheckPlayerRegistered( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( networkOccupiedSeats.ContainsValue(tienLenNetWorkPlayer.Object) ) {
                Debug.LogWarning($"[SeatManager] Player {tienLenNetWorkPlayer.PlayerRef} is already registered in a seat.");
                return false;
            }

            return true;
        }

        private async Task<int> FindAvailableSeat() {
            // The replicated dictionary is the only source of truth for occupancy.
            // Do not read PlayerSeat.isOccupied (visual state) here.
            for ( int i = 0; i < TotalSeats; i++ ) {
                if ( networkOccupiedSeats.ContainsKey(i) ) continue;

                var seats = await seatProvider.GetPlayerSeatsAsync();
                if ( i >= seats.Length || seats[i] == null ) continue;

                return i;
            }

            return -1;
        }

        private void OnOccupiedSeatsChanged() {
            _ = OnOccupiedSeatsChangedAsync();
        }

        private async Task OnOccupiedSeatsChangedAsync() {
            // Rebuild the model from the replicated dictionary.
            // Reuse existing TienLenPlayer instances for seats that stay occupied
            // by the same network player.
            var newSeatedPlayers = new Dictionary<int, TienLenPlayer>();
            bool hasUnresolved = false;

            foreach ( var kvp in networkOccupiedSeats ) {
                int seatIndex = kvp.Key;
                var netObj = kvp.Value;

                if ( netObj == null || !netObj.TryGetComponent<TienLenNetWorkPlayer>(out var netPlayer) ) {
                    // The ref replicated before the object spawned locally.
                    // Don't drop the seat; retry shortly instead.
                    hasUnresolved = true;
                    continue;
                }

                if ( seatedPlayers.TryGetValue(seatIndex, out var existingPlayer)
                    && existingPlayer != null
                    && existingPlayer.Id == netPlayer.PlayerRef.PlayerId ) {
                    // Seat retained by same network player -> keep the logic player
                    // (and its Hand) intact.
                    newSeatedPlayers[seatIndex] = existingPlayer;
                }
                else {
                    // New occupant (or different player reusing the seat) ->
                    // create a fresh logic player.
                    newSeatedPlayers[seatIndex] = new TienLenPlayer(netPlayer);
                }
            }

            var before = new Dictionary<int, TienLenPlayer>(seatedPlayers);

            seatedPlayers.Clear();
            foreach ( var kvp in newSeatedPlayers ) {
                seatedPlayers[kvp.Key] = kvp.Value;
            }

            // Bump only on real content change: same keys but a replaced
            // occupant (leave+rejoin in one snapshot) must count too, so
            // compare instance identity, not just keys.
            bool modelChanged = before.Count != seatedPlayers.Count;
            if ( !modelChanged ) {
                foreach ( var kvp in seatedPlayers ) {
                    if ( !before.TryGetValue(kvp.Key, out var oldPlayer)
                        || !ReferenceEquals(oldPlayer, kvp.Value) ) {
                        modelChanged = true;
                        break;
                    }
                }
            }

            if ( modelChanged ) {
                SeatRevision++;
            }

            if ( hasUnresolved ) {
                retryAttempts++;
                retryAt = Time.time + RetryDelay;
                retryPending = true;
            }
            else {
                retryAttempts = 0;
            }

            Debug.Log($"[SeatManager] Seats changed, occupied={seatedPlayers.Count}, revision={SeatRevision}");
        }

        #region IPlayerRegisterService

        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer() {
            List<TienLenNetWorkPlayer> result = new();

            if ( Object == null || !Object.IsValid ) return result;

            foreach ( var kvp in networkOccupiedSeats ) {
                // On a client the ref can replicate before the object spawns
                // locally, reading back as null. Skip instead of NREing.
                if ( kvp.Value == null ) continue;
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(player);
                }
            }

            return result;
        }

        public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap() {
            Dictionary<int, TienLenNetWorkPlayer> result = new();

            if ( Object == null || !Object.IsValid ) return result;

            foreach ( var kvp in networkOccupiedSeats ) {
                // On a client the ref can replicate before the object spawns
                // locally, reading back as null. Skip instead of NREing.
                if ( kvp.Value == null ) continue;
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(kvp.Key, player);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the stable logic player instances keyed by their **network seat
        /// index**. Presenters read this and apply their own visual re-indexing.
        /// </summary>
        public IReadOnlyDictionary<int, TienLenPlayer> GetSeatedPlayers() {
            return seatedPlayers;
        }

        public TienLenPlayer GetLogicPlayer( PlayerRef player ) {
            if ( !player.IsValid ) return null;
            if ( !TryGetSeat( player, out int seatIndex ) ) return null;

            return seatedPlayers.TryGetValue(seatIndex, out var logicPlayer) ? logicPlayer : null;
        }

        #endregion
    }
}