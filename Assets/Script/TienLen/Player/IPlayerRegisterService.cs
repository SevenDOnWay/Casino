using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.UI;
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
        public IReadOnlyList<PlayerSeat> GetOccupiedPlayerSeats();
    }
}