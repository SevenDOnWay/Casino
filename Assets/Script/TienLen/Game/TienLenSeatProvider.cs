using UnityEngine;

namespace Assets.Script.TienLen.Game {
    /// <summary>
    /// Specialized SeatProvider for 4-player Tiến Lên table layouts.
    /// </summary>
    public class TienLenSeatProvider : SeatProvider {
        private const int MaxTienLenSeats = 4;

        public override int TotalSeats => MaxTienLenSeats;

        public override int CalculateVisualSlotIndex( int networkSeatIndex, int localSeatIndex ) {
            if ( localSeatIndex == -1 ) {
                return networkSeatIndex;
            }

            return (networkSeatIndex - localSeatIndex + TotalSeats) % TotalSeats;
        }
    }
}
