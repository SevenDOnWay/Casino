using Assets.Script.TienLen.Player;
using Fusion;
using System;
using System.Collections;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    public class LocalPlayerService : ILocalPlayerService {
        public TienLenPlayer Player { get; private set; }
        public TienLenNetWorkPlayer NetworkPlayer { get; private set; }

        public void SetLocalNetworkPlayer( TienLenNetWorkPlayer networkPlayer ) {
            NetworkPlayer = networkPlayer;
        }

        public event Action<TienLenPlayer> OnLocalPlayerSet;
        public void SetLocalLogicPlayer( TienLenPlayer player ) {
            Player = player;
            OnLocalPlayerSet?.Invoke(player);
        }

        public string GetID() {
            return Player?.Id.ToString();
        }

        public string GetName() {
            return Player?.PlayerName;
        }

        public TienLenNetWorkPlayer GetLocalNetworkPlayer() {
            return NetworkPlayer;
        }

        public TienLenPlayer GetLocalLogicPlayer() {
            return Player;
        }
    }
}