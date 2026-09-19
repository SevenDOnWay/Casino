using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.Player {
    public class NetworkObjectFactory : NetworkObjectProviderDefault {
        private readonly IObjectResolver container;

        public NetworkObjectFactory( IObjectResolver container ) {
            this.container = container;
        }

        protected NetworkObject InstantiatePrefab( GameObject prefab, Vector3 position, Quaternion rotation, PlayerRef owner ) {
            return container.Resolve<NetworkObject>( new object[] { prefab, position, rotation, owner } );
        }

    }
}