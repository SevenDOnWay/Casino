using Assets.Script.Data.Models;
using Assets.Script.Data.Services;
using Assets.Script.TienLen.Player;
using System;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    public class LocalPlayerService : ILocalPlayerService {
        private readonly IPlayerProfileService profileService;

        public TienLenPlayer Player { get; private set; }
        public TienLenNetWorkPlayer NetworkPlayer { get; private set; }
        public PlayerProfileData Profile => profileService?.CachedProfile ?? explicitProfile;

        private PlayerProfileData explicitProfile;

        public event Action<TienLenPlayer> OnLocalPlayerSet;
        public event Action<PlayerProfileData> OnProfileUpdated;

        public LocalPlayerService() { }

        public LocalPlayerService( IPlayerProfileService profileService ) {
            this.profileService = profileService;
            if ( this.profileService != null ) {
                this.profileService.OnMoneyChanged += _ => OnProfileUpdated?.Invoke(Profile);
                this.profileService.OnLevelOrExpChanged += (_, _) => OnProfileUpdated?.Invoke(Profile);
                this.profileService.OnAvatarChanged += _ => OnProfileUpdated?.Invoke(Profile);
                this.profileService.OnDisplayNameChanged += _ => OnProfileUpdated?.Invoke(Profile);
            }
        }

        public void SetLocalNetworkPlayer( TienLenNetWorkPlayer networkPlayer ) {
            NetworkPlayer = networkPlayer;
        }

        public void SetLocalLogicPlayer( TienLenPlayer player ) {
            Player = player;
            OnLocalPlayerSet?.Invoke(player);
        }

        public void SetProfile( PlayerProfileData profile ) {
            explicitProfile = profile;
            OnProfileUpdated?.Invoke(profile);
        }

        public string GetID() {
            return Profile?.id ?? Player?.Id.ToString();
        }

        public string GetName() {
            return Profile?.displayName ?? Player?.PlayerName;
        }

        public int GetAvatarId() {
            return Profile?.avatarId ?? 0;
        }

        public long GetMoney() {
            return Profile?.money ?? 0;
        }

        public int GetLevel() {
            return Profile?.level ?? 1;
        }

        public TienLenNetWorkPlayer GetLocalNetworkPlayer() {
            return NetworkPlayer;
        }

        public TienLenPlayer GetLocalLogicPlayer() {
            return Player;
        }
    }
}
