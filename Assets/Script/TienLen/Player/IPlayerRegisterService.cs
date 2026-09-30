using Assets.Script.NetWorkScript;
using Fusion;

namespace Assets.Script.TienLen.Player {
    /// <summary>
    /// Mutating service for registering and unregistering players into seats.
    /// Used strictly by authoritative session and lobby controllers.
    /// </summary>
    public interface IPlayerRegisterService {
        void RegisterPlayer( TienLenNetWorkPlayer player );
        void UnregisterPlayer( PlayerRef player );
    }
}
