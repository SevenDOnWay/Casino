using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Fusion;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class SeatManager : NetworkBehaviour, IPlayerRegisterService, ISeatQueryService {
        private const int TotalSeats = 4;
        private const float RetryDelay = 0.5f;
        private const int MaxRetryAttempts = 20;

        private ILocalPlayerService localPlayerService;

        /// <summary>
        /// A networked dictionary that maps seat indices to the NetworkObject of the player occupying that seat.
        /// Client will receive updates when players join or leave seats.
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
        /// UI detects changes by polling or checking this.
        /// </summary>
        public int SeatRevision { get; private set; }

        public bool HasUnresolvedSeats => retryPending;

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
        void Construct( ILocalPlayerService localPlayerService ) {
            this.localPlayerService = localPlayerService;
        }

        public override void Spawned() {
            base.Spawned();
            // Initial snapshot rebuild to pick up seats that already replicated (e.g. late-joining client).
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
            if ( !retryPending && seatedPlayers.Count != ResolvedSeatCount ) {
                _ = OnOccupiedSeatsChangedAsync();
            }
        }

        public NetworkDictionary<int, NetworkObject> GetNetworkOccupiedSeats() {
            return networkOccupiedSeats;
        }

        #region IPlayerRegisterService (Mutations)

        public void RegisterPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( !Object.HasStateAuthority ) return;
            if ( tienLenNetWorkPlayer == null || tienLenNetWorkPlayer.Object == null ) return;
            if ( !CheckPlayerRegistered(tienLenNetWorkPlayer) ) return;

            int seatIndex = FindAvailableSeat();
            if ( seatIndex == -1 ) {
                Debug.LogWarning("[SeatManager] No available seat for " + tienLenNetWorkPlayer.PlayerRef);
                return;
            }

            networkOccupiedSeats.Add(seatIndex, tienLenNetWorkPlayer.Object);
            Debug.Log($"[SeatManager] Registered player {tienLenNetWorkPlayer.PlayerRef.PlayerId} into seat {seatIndex}");
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
                Debug.Log($"[SeatManager] Unregistered seat {foundSeat} for player {player.PlayerId}");
                return;
            }

            Debug.LogWarning($"[SeatManager] No seat found for leaving player {player.PlayerId}");
        }

        private bool CheckPlayerRegistered( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( networkOccupiedSeats.ContainsValue(tienLenNetWorkPlayer.Object) ) {
                Debug.LogWarning($"[SeatManager] Player {tienLenNetWorkPlayer.PlayerRef} is already registered in a seat.");
                return false;
            }

            return true;
        }

        private int FindAvailableSeat() {
            for ( int i = 0; i < TotalSeats; i++ ) {
                if ( !networkOccupiedSeats.ContainsKey(i) ) {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region ISeatQueryService (Queries)

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

        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer() {
            List<TienLenNetWorkPlayer> result = new();

            if ( Object == null || !Object.IsValid ) return result;

            foreach ( var kvp in networkOccupiedSeats ) {
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
                if ( kvp.Value == null ) continue;
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(kvp.Key, player);
                }
            }

            return result;
        }

        public IReadOnlyDictionary<int, TienLenPlayer> GetSeatedPlayers() {
            return seatedPlayers;
        }

        public TienLenPlayer GetLogicPlayer( PlayerRef player ) {
            if ( !player.IsValid ) return null;
            if ( !TryGetSeat(player, out int seatIndex) ) return null;

            return seatedPlayers.TryGetValue(seatIndex, out var logicPlayer) ? logicPlayer : null;
        }

        #endregion

        private void OnOccupiedSeatsChanged() {
            _ = OnOccupiedSeatsChangedAsync();
        }

        private async Task OnOccupiedSeatsChangedAsync() {
            var newSeatedPlayers = new Dictionary<int, TienLenPlayer>();
            bool hasUnresolved = false;

            foreach ( var kvp in networkOccupiedSeats ) {
                int seatIndex = kvp.Key;
                var netObj = kvp.Value;

                if ( netObj == null || !netObj.TryGetComponent<TienLenNetWorkPlayer>(out var netPlayer) ) {
                    hasUnresolved = true;
                    continue;
                }

                if ( seatedPlayers.TryGetValue(seatIndex, out var existingPlayer)
                    && existingPlayer != null
                    && existingPlayer.Id == netPlayer.PlayerRef.PlayerId ) {
                    newSeatedPlayers[seatIndex] = existingPlayer;
                }
                else {
                    newSeatedPlayers[seatIndex] = new TienLenPlayer(netPlayer);
                }
            }

            var before = new Dictionary<int, TienLenPlayer>(seatedPlayers);

            seatedPlayers.Clear();
            foreach ( var kvp in newSeatedPlayers ) {
                seatedPlayers[kvp.Key] = kvp.Value;
            }

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

            // Automatically resolve and notify local player service upon seat change
            if ( localPlayerService != null && Runner != null && Runner.LocalPlayer.IsValid ) {
                TienLenPlayer localLogic = GetLogicPlayer(Runner.LocalPlayer);
                if ( localLogic != null && !ReferenceEquals(localPlayerService.GetLocalLogicPlayer(), localLogic) ) {
                    localPlayerService.SetLocalLogicPlayer(localLogic);
                }
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
    }
}
