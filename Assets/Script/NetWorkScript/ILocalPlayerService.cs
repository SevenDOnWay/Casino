using Assets.Script.Data.Models;
using Assets.Script.TienLen.Player;
using Fusion;
using System;

namespace Assets.Script.NetWorkScript {
    public interface ILocalPlayerService {
        /// <summary>
        /// Raised whenever the local logic player is (re)bound, so UI that
        /// depends on the local hand can hook in without polling.
        /// </summary>
        event Action<TienLenPlayer> OnLocalPlayerSet;
        event Action<PlayerProfileData> OnProfileUpdated;

        PlayerProfileData Profile { get; }
        TienLenNetWorkPlayer GetLocalNetworkPlayer();
        TienLenPlayer GetLocalLogicPlayer();
        void SetLocalNetworkPlayer( TienLenNetWorkPlayer networkPlayer );
        void SetLocalLogicPlayer( TienLenPlayer player );
        void SetProfile( PlayerProfileData profile );
        string GetID();
        string GetName();
        int GetAvatarId();
        long GetMoney();
        int GetLevel();
    }
}
