using Assets.Script.NetWorkScript;
using Fusion;
using System.Collections.Generic;

namespace Assets.Script.TienLen.Player {
    /// <summary>
    /// Read-only querying service for seat occupancy and player mapping.
    /// Used by UI, controllers, and rule systems that must not mutate seats.
    /// </summary>
    public interface ISeatQueryService {
        /// <summary>
        /// Monotonic counter bumped every time the seat model is rebuilt.
        /// UI polls or checks this to detect changes.
        /// </summary>
        int SeatRevision { get; }

        bool TryGetSeat( PlayerRef player, out int seatIndex );
        IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer();
        IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap();

        /// <summary>
        /// Returns the stable logic player instances keyed by their network seat index.
        /// </summary>
        IReadOnlyDictionary<int, TienLenPlayer> GetSeatedPlayers();

        /// <summary>
        /// Resolves the logic player sitting in the seat owned by the player, or null.
        /// </summary>
        TienLenPlayer GetLogicPlayer( PlayerRef player );
    }
}
