using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.Player {
    public class NetworkObjectFactory : NetworkObjectProviderDefault {
        private readonly IObjectResolver resolver;

        public NetworkObjectFactory( IObjectResolver resolver ) {
            this.resolver = resolver;
        }

        protected NetworkObject InstantiatePrefab( GameObject prefab, PlayerRef owner ) {
            Debug.Log($"[NetworkObjectFactory] Instantiating prefab: {prefab.name} for owner: {owner}");
            return resolver.Resolve<NetworkObject>( new object[] { prefab, owner } );
        }

    }
}