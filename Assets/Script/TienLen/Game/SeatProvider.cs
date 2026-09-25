using Assets.Script.TienLen.UI;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class SeatProvider : NetworkBehaviour {
        [SerializeField] TableVisualLayoutManager tableVisualLayoutManager;

        private const int TotalSeats = 4;

        [SerializeField] private PlayerSeat[] playerSeats = new PlayerSeat[TotalSeats];

        /// <summary>
        /// A networked dictionary that maps seat indices to the NetworkObject of the player occupying that seat.
        /// Client will receive updates when players join or leave seats, allowing for real-time synchronization of seat occupancy across the network.
        /// </summary>
        [Networked, Capacity(TotalSeats)] public NetworkDictionary<int, NetworkObject> OccupiedSeats => default;


        public NetworkDictionary<int, NetworkObject> GetOccupiedSeats() {
            if ( !IsValid ) {
                Debug.LogWarning("[SeatProvider] Attempted to access OccupiedSeats before Spawned() was called.");
                return default;
            }
            return OccupiedSeats;
        }

        public IReadOnlyList<PlayerSeat> AllSeats => playerSeats;

        public int SeatCount => playerSeats != null ? playerSeats.Length : 0;

        private void Awake() {
            // Automatically cache children if not wired in the inspector
            if ( playerSeats == null || playerSeats.Length == 0 || playerSeats[0] == null ) {
                playerSeats = GetComponentsInChildren<PlayerSeat>();
            }

            // Tag visual index order automatically
            for ( int i = 0; i < playerSeats.Length; i++ ) {
                if ( playerSeats[i] != null ) {
                    playerSeats[i].setSeatIndex(i);
                }
            }
        }

        /// <summary>
        /// Gets a seat by visual slot index (0 to 3).
        /// </summary>
        public PlayerSeat GetSeat( int index ) {
            if ( index < 0 || index >= playerSeats.Length ) {
                Debug.LogError($"[SeatProvider] Visual seat index {index} is out of bounds.");
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

        public PlayerSeat GetPreviousAvailableSeat( int index ) {
            for ( int i = index - 1; i >= 0; i-- ) {
                if ( playerSeats[i] != null && !playerSeats[i].isOccupied ) {
                    return playerSeats[i];
                }
            }
            return null;
        }

        public PlayerSeat GetNextAvailableSeat( int index ) {
            for ( int i = index + 1; i < playerSeats.Length; i++ ) {
                if ( playerSeats[i] != null && !playerSeats[i].isOccupied ) {
                    return playerSeats[i];
                }
            }
            return null;
        }


        /// <summary>
        /// Clears all seats (useful on match restart or disconnect).
        /// </summary>
        public void ResetAllSeats() {
            for ( int i = 0; i < playerSeats.Length; i++ ) {
                if ( playerSeats[i] != null ) {
                    playerSeats[i].ClearSeat();
                }
            }
        }

        public void assignSeat( KeyValuePair<int, NetworkObject> kvp ) {
            OccupiedSeats.Add(kvp.Key, kvp.Value);
            tableVisualLayoutManager?.RefreshLayout(Runner);
        }


    }
}