using Assets.Script.TienLen.Player;
using Fusion;
using System.Collections;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    public interface ILocalPlayerService {

        public TienLenNetWorkPlayer GetLocalNetworkPlayer();
        public TienLenPlayer GetLocalLogicPlayer();
        public string GetID();
        public string GetName();


    }
}