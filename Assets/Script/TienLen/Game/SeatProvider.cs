using Assets.Script.TienLen.UI;
using UnityEngine;

namespace Assets.Script.TienLen.Game {
    /// <summary>
    /// Base class for seat management and visual slot mapping.
    /// Provides standard 4-seat clockwise rotation by default, and can be extended
    /// for custom card games (e.g. Poker 6-max, 9-max).
    /// </summary>
    public class SeatProvider : MonoBehaviour {
        private const int DefaultTotalSeats = 4;

        [SerializeField, Header("Player Seats"), Tooltip("Cached player seats in visual order (0 = local anchor, then clockwise).")]
        protected PlayerSeat[] playerSeats;

        /// <summary>Maximum number of seats supported by this game type.</summary>
        public virtual int TotalSeats => DefaultTotalSeats;

        /// <summary>
        /// Maps a network seat index to a visual slot index relative to the local player anchor.
        /// </summary>
        /// <param name="networkSeatIndex">Authoritative network seat index (0..TotalSeats-1)</param>
        /// <param name="localSeatIndex">Network seat index of the local player (-1 if unseated/spectator)</param>
        /// <returns>Visual slot index</returns>
        public virtual int CalculateVisualSlotIndex( int networkSeatIndex, int localSeatIndex ) {
            if ( localSeatIndex == -1 ) {
                return networkSeatIndex;
            }

            return (networkSeatIndex - localSeatIndex + TotalSeats) % TotalSeats;
        }

        protected virtual void Awake() {
            EnsureInitialized();
        }

        public virtual PlayerSeat[] GetPlayerSeats() {
            return EnsureInitialized();
        }

        protected virtual PlayerSeat[] EnsureInitialized() {
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
    }
}
