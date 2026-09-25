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

        PlayerSeat[] playerSeats;

        private const int TotalSeats = 4;


        /// <summary>
        /// A networked dictionary that maps seat indices to the NetworkObject of the player occupying that seat.
        /// Client will receive updates when players join or leave seats, allowing for real-time synchronization of seat occupancy across the network.
        /// </summary>
        [Networked, Capacity(TotalSeats)] public NetworkDictionary<int, NetworkObject> OccupiedSeats => default;

        [Inject]
        void Construct(SeatProvider seatProvider) {
            this.seatProvider = seatProvider;

            playerSeats = seatProvider.GetPlayerSeats();
        }

        public NetworkDictionary<int, NetworkObject> GetOccupiedSeats() {
            return OccupiedSeats;
        }

        public void RegisterPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( !CheckPlayerRegistered(tienLenNetWorkPlayer) ) return;
            if ( FindAvailableSeat(out int seatIndex) == -1 ) return;

            OccupiedSeats.Add(seatIndex, tienLenNetWorkPlayer.Object);
        }

        private bool CheckPlayerRegistered( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            if ( OccupiedSeats.ContainsValue(tienLenNetWorkPlayer.Object) ) {
                Debug.LogWarning($"[SeatManager] Player {tienLenNetWorkPlayer.PlayerRef} is already registered in a seat.");
                return false;
            }

            return true;
        }

        private int FindAvailableSeat(out int seatIndex) {
            for ( int i = 0; i < TotalSeats; i++ ) {
                if ( playerSeats[i].isOccupied ) continue;
                seatIndex = i;
                return seatIndex;
            }

            Debug.LogWarning($"[SeatManager] No available seats for player");
            seatIndex = -1;
            return -1;
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

        public PlayerSeat[] GetAllOccupiedSeats() { 
            List<PlayerSeat> result = new();

            foreach ( PlayerSeat seat in playerSeats ) {
                if ( seat != null && seat.isOccupied ) {
                    result.Add(seat);
                }
            }

            return result.ToArray();
        }

        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer() {
            List<TienLenNetWorkPlayer> result = new();

            foreach ( var kvp in OccupiedSeats ) {
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(player);
                }
            }

            return result;
        }

        public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap() {
            Dictionary<int, TienLenNetWorkPlayer> result = new();

            foreach ( var kvp in OccupiedSeats ) {
                if ( kvp.Value.TryGetComponent<TienLenNetWorkPlayer>(out var player) ) {
                    result.Add(kvp.Key, player);
                }
            }

            return result;
        }

        public IReadOnlyList<PlayerSeat> GetPlayerSeats() {
            List<PlayerSeat> result = new();

            foreach ( var seat in playerSeats ) {
                if ( seat != null ) {
                    result.Add(seat);
                }
            }

            return result;
        }
    }
}