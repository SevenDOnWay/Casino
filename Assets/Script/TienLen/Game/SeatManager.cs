using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
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

        PlayerSeat[] playerSeats = new PlayerSeat[4];

        private const int TotalSeats = 4;


        /// <summary>
        /// A networked dictionary that maps seat indices to the NetworkObject of the player occupying that seat.
        /// Client will receive updates when players join or leave seats, allowing for real-time synchronization of seat occupancy across the network.
        /// </summary>
        [Networked, Capacity(TotalSeats), OnChangedRender(nameof(OnOccupiedSeatsChanged))]
        public NetworkDictionary<int, NetworkObject> networkOccupiedSeats => default;


        public event Action OnSeatsChanged;

        [Inject]
        void Construct( SeatProvider seatProvider ) {
            this.seatProvider = seatProvider;
        }

        public override void Spawned() {
            base.Spawned();
        }

        public NetworkDictionary<int, NetworkObject> GetNetworkOccupiedSeats() {
            return networkOccupiedSeats;
        }

        public void RegisterPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            _ = RegisterPlayerAsync(tienLenNetWorkPlayer);
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
            var seats = await seatProvider.GetPlayerSeatsAsync();

            for ( int i = 0; i < TotalSeats; i++ ) {
                if ( networkOccupiedSeats.ContainsKey(i) ) continue;
                if ( i >= seats.Length || seats[i] == null ) continue;
                if ( seats[i].isOccupied ) continue;

                return i;
            }

            return -1;
        }


        private void OnOccupiedSeatsChanged() {
            _ = OnOccupiedSeatsChangedAsync();
        }

        private async Task OnOccupiedSeatsChangedAsync() {
            // Sync visual seats from the replicated dictionary so every
            // peer (host + clients) sees the same occupancy. Each entry's
            // TienLenNetWorkPlayer carries its PlayerRef.
            playerSeats ??= await seatProvider.GetPlayerSeatsAsync();

            for ( int i = 0; i < playerSeats.Length; i++ ) {
                if ( playerSeats[i] == null ) continue;
                if ( playerSeats[i].isOccupied ) continue;

                if ( playerSeats[i].tienLenPlayer == null ) {
                    var tienLenPlayer = new TienLenPlayer(i, name);
                    playerSeats[i].BindLogicPlayer(tienLenPlayer);
                }
                else {
                    playerSeats[i].ClearSeat();
                }
            }

            OnSeatsChanged?.Invoke();
        }


        /// <summary>
        /// Gets a seat by visual slot index (0 to 3).
        /// </summary>
        public PlayerSeat GetSeat( int index ) {
            if ( index < 0 || index >= playerSeats.Length ) {
                Debug.LogError($"[SeatManager] Visual seat index {index} is out of bounds.");
                return null;
            }
            return playerSeats[index];
        }


        #region interface
        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer() {
            List<TienLenNetWorkPlayer> result = new();

            if ( Object == null || !Object.IsValid ) return result;

            foreach ( var kvp in networkOccupiedSeats ) {
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
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(kvp.Key, player);
                }
            }

            return result;
        }

        public IReadOnlyList<PlayerSeat> GetOccupiedPlayerSeats() {
            List<PlayerSeat> result = new();

            if ( Object == null || !Object.IsValid ) return result;

            var seats = seatProvider != null ? seatProvider.GetPlayerSeats() : playerSeats;
            if ( seats == null ) return result;

            foreach ( var kvp in networkOccupiedSeats ) {
                int seatIndex = kvp.Key;
                if ( seatIndex >= 0 && seatIndex < seats.Length && seats[seatIndex] != null ) {
                    result.Add(seats[seatIndex]);
                }
            }

            return result;
        }
        #endregion
    }
}