using Assets.Script.TienLen.Player;
using System;
using System.Collections;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    public class LocalPlayerService : IPlayer {
        public TienLenPlayer Player { get; private set; }


        public event Action<TienLenPlayer> OnLocalPlayerSet;
        public void SetLocalPlayer( TienLenPlayer player ) {
            Player = player;
            OnLocalPlayerSet?.Invoke(player);
        }

        public string GetID() {
            return Player?.Id.ToString();
        }

        public string GetName() {
            return Player?.PlayerName;
        }



    }
}