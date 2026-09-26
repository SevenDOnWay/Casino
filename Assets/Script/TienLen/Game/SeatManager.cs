using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using System.Collections.Generic;
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
        [Networked, Capacity(TotalSeats), OnChangedRender(nameof(OnOccupiedSeatsChanged))] public NetworkDictionary<int, NetworkObject> networkOccupiedSeats => default;

        [Inject]
        void Construct( SeatProvider seatProvider ) {
            this.seatProvider = seatProvider;

            playerSeats = seatProvider.GetPlayerSeats();
        }

        public override void Spawned() {
            base.Spawned();
        }

        public NetworkDictionary<int, NetworkObject> GetNetworkOccupiedSeats() {
            return networkOccupiedSeats;
        }

        public void RegisterPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( !CheckPlayerRegistered(tienLenNetWorkPlayer) ) return;
            if ( FindAvailableSeat(out int seatIndex) == -1 ) return;

            networkOccupiedSeats.Add(seatIndex, tienLenNetWorkPlayer.Object);
        }

        private bool CheckPlayerRegistered( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( networkOccupiedSeats.ContainsValue(tienLenNetWorkPlayer.Object) ) {
                Debug.LogWarning($"[SeatManager] Player {tienLenNetWorkPlayer.PlayerRef} is already registered in a seat.");
                return false;
            }

            return true;
        }

        private int FindAvailableSeat( out int seatIndex ) {
            var seats = seatProvider.GetPlayerSeats();
            for ( int i = 0; i < TotalSeats; i++ ) {
                if ( i >= seats.Length || seats[i] == null ) continue;
                if ( seats[i].isOccupied ) continue;
                seatIndex = i; return i;
            }

            seatIndex = -1; return -1;
        }


        private void OnOccupiedSeatsChanged() {
            // Handle changes to the networked occupied seats


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

            foreach ( var kvp in networkOccupiedSeats ) {
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(player);
                }
            }

            return result;
        }

        public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap() {
            Dictionary<int, TienLenNetWorkPlayer> result = new();

            foreach ( var kvp in networkOccupiedSeats ) {
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(kvp.Key, player);
                }
            }

            return result;
        }

        public IReadOnlyList<PlayerSeat> GetOccupiedPlayerSeats() {
            List<PlayerSeat> result = new();

            foreach ( var seat in playerSeats ) {
                if ( seat != null ) {
                    result.Add(seat);
                }
            }

            return result;
        }
        #endregion
    }
}