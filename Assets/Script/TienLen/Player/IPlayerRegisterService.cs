using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.UI;
using System.Collections.Generic;

namespace Assets.Script.TienLen.Player {
    public interface IPlayerRegisterService {

        public void RegisterPlayer( TienLenNetWorkPlayer player );

        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer();
        public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap();
        public IReadOnlyList<PlayerSeat> GetOccupiedPlayerSeats();
    }
}