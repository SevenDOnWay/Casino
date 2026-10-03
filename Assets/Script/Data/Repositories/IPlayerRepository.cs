using Assets.Script.Data.Models;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace Assets.Script.Data.Repositories {
    public interface IPlayerRepository : IDataRepository<PlayerProfileData> {
        UniTask<AuthResponse> LoginAsync( LoginRequest request );
        UniTask<AuthResponse> RegisterAsync( RegisterRequest request );
        UniTask<PlayerProfileData> GetProfileAsync( string token );
        UniTask<PlayerProfileData> UpdateProfileAsync( string token, UpdateProfileRequest request );
        UniTask<List<PlayerProfileData>> GetFriendsAsync( string token );
        UniTask<bool> AddFriendAsync( string token, string friendEmailOrName );
        UniTask<bool> RecordMatchResultAsync( string token, MatchResultReportRequest request );
    }
}
