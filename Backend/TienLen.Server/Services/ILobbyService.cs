using System.Collections.Generic;
using System.Threading.Tasks;
using TienLen.Server.DTOs;

namespace TienLen.Server.Services {
    public interface ILobbyService {
        Task<ChannelQueryResponse> GetChannelsAsync( int betStake );
        Task<List<LobbySessionDto>> GetSessionsAsync( int betStake, int channel );
        Task<bool> RegisterSessionAsync( string hostUserId, RegisterSessionRequest request );
        Task<bool> HeartbeatAsync( string sessionName, int playerCount );
        Task<bool> UnregisterSessionAsync( string sessionName );
        Task<QuickJoinResponse> QuickJoinAsync( int betStake );
    }
}
