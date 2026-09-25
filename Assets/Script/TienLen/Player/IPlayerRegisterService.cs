using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.UI;
using System.Collections.Generic;

namespace Assets.Script.TienLen.Player {
    public interface IPlayerRegisterService {
        public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer();
        public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap();
        public IReadOnlyList<PlayerSeat> GetPlayerSeats();
    }
}