using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Fusion;
using System;
using System.Collections.Generic;

namespace Assets.Script.TienLen.Player {
    public interface IPlayerRegisterService {

        public event Action OnSeatsChanged;

        public void RegisterPlayer( TienLenNetWorkPlayer player );
        public void UnregisterPlayer( PlayerRef player );
        public bool TryGetSeat( PlayerRef player, out int seatIndex );

        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer();
        public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap();

        /// <summary>
        /// Returns the stable logic player instances keyed by their **network seat
        /// index**. This is the single source of truth for "who sits where" in
        /// network terms. Presenters (e.g. TableVisualLayoutManager) read this and
        /// apply their own visual re-indexing.
        /// </summary>
        public IReadOnlyDictionary<int, TienLenPlayer> GetSeatedPlayers();

        /// <summary>
        /// Resolves the logic player sitting in the seat owned by
        /// <paramref name="player"/>, or null when that seat has not been
        /// bound yet. TienLenPlayer carries no network identity, so this is
        /// the only way to go from a PlayerRef to its hand.
        /// </summary>
        public TienLenPlayer GetLogicPlayer( PlayerRef player );
    }
}