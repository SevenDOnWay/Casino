using Assets.Script.TienLen.UI;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Script.TienLen.Game {
    public class SeatProvider : MonoBehaviour {
        private const int TotalSeats = 4;

        [SerializeField, Header("Player Seat"), Tooltip("This is for debugging purposes only do not assign in the inspector.")]
        private PlayerSeat[] playerSeats = new PlayerSeat[TotalSeats];

        private TaskCompletionSource<PlayerSeat[]> _initTcs;

        private void Awake() {
            EnsureInitialized();
        }

        private PlayerSeat[] EnsureInitialized() {
            if ( playerSeats == null || playerSeats.Length == 0 || playerSeats[0] == null ) {
                playerSeats = GetComponentsInChildren<PlayerSeat>(true);
            }

            for ( int i = 0; i < playerSeats.Length; i++ ) {
                if ( playerSeats[i] != null ) {
                    playerSeats[i].setVisualIndex(i);
                }
            }

            return playerSeats;
        }

        public PlayerSeat[] GetPlayerSeats() {
            return EnsureInitialized();
        }

        public Task<PlayerSeat[]> GetPlayerSeatsAsync() {
            // If already cached/set up, return immediately
            if ( playerSeats != null && playerSeats.Length > 0 && playerSeats[0] != null ) {
                return Task.FromResult(playerSeats);
            }

            // Initialize and return
            return Task.FromResult(EnsureInitialized());
        }
    }
}