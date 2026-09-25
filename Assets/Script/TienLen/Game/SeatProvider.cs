using Assets.Script.TienLen.UI;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.Game {
    public class SeatProvider : MonoBehaviour {
        private const int TotalSeats = 4;

        [SerializeField, Header("Player Seat"), Tooltip("This is for debugging purposes only do not assign in the inspector.")]
        private PlayerSeat[] playerSeats = new PlayerSeat[TotalSeats];

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

        public PlayerSeat[] GetPlayerSeats() {
            return playerSeats;
        }
    }
}