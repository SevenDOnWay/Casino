using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using Assets.Script.NetWorkScript;
using System;
using Unity.Netcode.Transports.SinglePlayer;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script {
    /// <summary>
    /// Application-wide root container.
    ///
    /// It survives scene loads (<see cref="Object.DontDestroyOnLoad"/>) so that session
    /// state (IAuthService, IPlayerProfileService, ILocalPlayerService, ...) stays alive
    /// for the whole play session, instead of being recreated (and losing the logged in
    /// user) on every scene change.
    ///
    /// Scene-local scopes such as <see cref="TienLen.LifeTimeScope.TienLenLifeTimeScope"/>
    /// automatically attach to this scope as their parent (see <see cref="FindParent"/>),
    /// so they resolve the persistent singletons from here and only register per-scene
    /// services themselves.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public class RootLifeTimeScope : LifetimeScope {

        public string apiBaseUrl = "http://localhost:5000/api"; //TODO: Make this configurable in the Unity Editor or via a config file.

        [SerializeField] private AvatarDatabaseSO avatarDatabaseSO;
        protected override void Configure( IContainerBuilder builder ) {
            builder.RegisterInstance(avatarDatabaseSO).As<AvatarDatabaseSO>();

            builder.Register<RestPlayerRepository>(Lifetime.Singleton)
                   .WithParameter("baseUrl", apiBaseUrl)
                   .As<IPlayerRepository>()
                   .As<IDataRepository<PlayerProfileData>>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Development & Editor builds: unencrypted PlayerPrefs storage for easy debugging
            builder.Register<PlayerPrefsTokenStorage>(Lifetime.Singleton).As<ITokenStorage>();
#else
            // Production Release builds: AES-encrypted secure storage
            builder.Register<SecureTokenStorage>(Lifetime.Singleton).As<ITokenStorage>();
#endif
            builder.Register<AuthService>(Lifetime.Singleton).As<IAuthService>();
            builder.Register<PlayerProfileService>(Lifetime.Singleton).As<IPlayerProfileService>();
            builder.Register<LocalPlayerService>(Lifetime.Singleton)
                   .As<ILocalPlayerService>()
                   .AsSelf();

            builder.Register<RestLobbyService>(Lifetime.Singleton)
                   .WithParameter("baseUrl", apiBaseUrl)
                   .As<ILobbyRestService>()
                   .AsSelf();
        }


    }
}
