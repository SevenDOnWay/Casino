using Assets.Script.TienLen.Player;
using Fusion;
using System;
using System.Collections;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    public interface ILocalPlayerService {
        /// <summary>
        /// Raised whenever the local logic player is (re)bound, so UI that
        /// depends on the local hand can hook in without polling.
        /// </summary>
        public event Action<TienLenPlayer> OnLocalPlayerSet;

        public TienLenNetWorkPlayer GetLocalNetworkPlayer();
        public TienLenPlayer GetLocalLogicPlayer();
        public void SetLocalNetworkPlayer( TienLenNetWorkPlayer networkPlayer );
        public void SetLocalLogicPlayer( TienLenPlayer player );
        public string GetID();
        public string GetName();


    }
}