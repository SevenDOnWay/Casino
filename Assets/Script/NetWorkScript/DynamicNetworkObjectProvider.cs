using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.NetWorkScript {
    public class DynamicNetworkObjectProvider : NetworkObjectProviderDefault {
        protected override NetworkObject InstantiatePrefab( NetworkRunner runner, NetworkObject prefab ) {
            var instance = base.InstantiatePrefab(runner, prefab);
            if ( instance != null ) {
                InjectInstance(runner, instance);
            }
            return instance;
        }

        private void InjectInstance( NetworkRunner runner, NetworkObject instance ) {
            Scene targetScene = default;

            if ( runner != null && runner.SimulationUnityScene.IsValid() ) {
                targetScene = runner.SimulationUnityScene;
            }
            else if ( instance.gameObject.scene.IsValid() ) {
                targetScene = instance.gameObject.scene;
            }
            else {
                targetScene = SceneManager.GetActiveScene();
            }

            var scope = LifetimeScope.Find<LifetimeScope>(targetScene);
            if ( scope == null && SceneManager.GetActiveScene() != targetScene ) {
                scope = LifetimeScope.Find<LifetimeScope>(SceneManager.GetActiveScene());
            }

            if ( scope == null || scope.Container == null ) {
                throw new InvalidOperationException(
                    $"[DynamicNetworkObjectProvider] No LifetimeScope found in scene '{targetScene.name}' for spawned object '{instance.name}'.");
            }

            scope.Container.InjectGameObject(instance.gameObject);
        }
    }
}
