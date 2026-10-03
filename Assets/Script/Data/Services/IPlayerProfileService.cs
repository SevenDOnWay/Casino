using Assets.Script.Data.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Assets.Script.Data.Services {
    public interface IPlayerProfileService {
        PlayerProfileData CachedProfile { get; }

        event Action<long> OnMoneyChanged;
        event Action<int, int> OnLevelOrExpChanged;
        event Action<int> OnAvatarChanged;
        event Action<string> OnDisplayNameChanged;

        UniTask<PlayerProfileData> RefreshProfileAsync();
        UniTask<bool> UpdateProfileAsync( string displayName, int avatarId );
        UniTask<bool> RecordMatchResultAsync( MatchResultReportRequest matchResult );
        UniTask<List<PlayerProfileData>> LoadFriendsAsync();
        UniTask<bool> AddFriendAsync( string friendEmailOrName );
    }
}
