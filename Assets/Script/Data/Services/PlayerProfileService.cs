using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Assets.Script.Data.Services {
    public class PlayerProfileService : IPlayerProfileService {
        private readonly IPlayerRepository playerRepository;
        private readonly IAuthService authService;

        public PlayerProfileData CachedProfile => authService.CurrentUser;

        public event Action<long> OnMoneyChanged;
        public event Action<int, int> OnLevelOrExpChanged;
        public event Action<int> OnAvatarChanged;
        public event Action<string> OnDisplayNameChanged;

        public PlayerProfileService( IPlayerRepository playerRepository, IAuthService authService ) {
            this.playerRepository = playerRepository;
            this.authService = authService;
        }

        public async UniTask<PlayerProfileData> RefreshProfileAsync() {
            if ( !authService.IsAuthenticated ) return null;

            var profile = await playerRepository.GetProfileAsync(authService.AuthToken);
            if ( profile != null ) {
                UpdateLocalCache(profile);
            }

            return profile;
        }

        public async UniTask<bool> UpdateProfileAsync( string displayName, int avatarId ) {
            if ( !authService.IsAuthenticated ) return false;

            var updated = await playerRepository.UpdateProfileAsync(authService.AuthToken, new UpdateProfileRequest {
                displayName = displayName,
                avatarId = avatarId
            });

            if ( updated != null ) {
                UpdateLocalCache(updated);
                return true;
            }

            return false;
        }

        public async UniTask<bool> RecordMatchResultAsync( MatchResultReportRequest matchResult ) {
            if ( !authService.IsAuthenticated ) return false;

            bool success = await playerRepository.RecordMatchResultAsync(authService.AuthToken, matchResult);
            if ( success ) {
                await RefreshProfileAsync();
            }

            return success;
        }

        public async UniTask<List<PlayerProfileData>> LoadFriendsAsync() {
            if ( !authService.IsAuthenticated ) return new List<PlayerProfileData>();
            return await playerRepository.GetFriendsAsync(authService.AuthToken);
        }

        public async UniTask<bool> AddFriendAsync( string friendEmailOrName ) {
            if ( !authService.IsAuthenticated ) return false;
            return await playerRepository.AddFriendAsync(authService.AuthToken, friendEmailOrName);
        }

        private void UpdateLocalCache( PlayerProfileData updated ) {
            if ( CachedProfile == null || updated == null ) return;

            long oldMoney = CachedProfile.money;
            int oldLevel = CachedProfile.level;
            int oldExp = CachedProfile.exp;
            int oldAvatar = CachedProfile.avatarId;
            string oldName = CachedProfile.displayName;

            CachedProfile.displayName = updated.displayName;
            CachedProfile.avatarId = updated.avatarId;
            CachedProfile.money = updated.money;
            CachedProfile.level = updated.level;
            CachedProfile.exp = updated.exp;
            CachedProfile.friendIds = updated.friendIds;
            CachedProfile.matchHistory = updated.matchHistory;

            if ( oldMoney != updated.money ) OnMoneyChanged?.Invoke(updated.money);
            if ( oldLevel != updated.level || oldExp != updated.exp ) OnLevelOrExpChanged?.Invoke(updated.level, updated.exp);
            if ( oldAvatar != updated.avatarId ) OnAvatarChanged?.Invoke(updated.avatarId);
            if ( oldName != updated.displayName ) OnDisplayNameChanged?.Invoke(updated.displayName);
        }
    }
}
