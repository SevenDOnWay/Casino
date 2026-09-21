using Assets.Script.TienLen.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.TienLen.Game {
    public class SeatProvider : MonoBehaviour {
        private const int TotalSeats = 4;

        [SerializeField] private PlayerSeat[] playerSeats = new PlayerSeat[TotalSeats];

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
                    playerSeats[i].seatIndex = i;
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




    }
}