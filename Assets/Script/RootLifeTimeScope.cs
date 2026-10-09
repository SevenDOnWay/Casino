using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using Assets.Script.NetWorkScript;
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
        public static RootLifeTimeScope Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string apiBaseUrl = "http://localhost:5000/api";
        [SerializeField] private bool useMockRepository = false;

        [Header("Shared Databases")]
        [SerializeField] private AvatarDatabaseSO avatarDatabase;

        /// <summary>
        /// Guarantees a root container exists even when play mode starts in a scene that
        /// has no RootLifeTimeScope of its own (the one in SignInScene is only reached by
        /// loading that scene). Returns the persistent scope.
        /// </summary>
        public static RootLifeTimeScope Ensure() {
            if ( Instance != null ) return Instance;

            var go = new GameObject("RootLifeTimeScope");
            return go.AddComponent<RootLifeTimeScope>();
        }

        protected override void Awake() {
            // Only the first instance owns the persistent container.
            // (Re-entering the bootstrap scene must not build a second root.)
            if ( Instance != null && Instance != this ) {
                Destroy(gameObject);
                return;
            }

            // Fall back to the bundled database when the scope was created at runtime
            // instead of being configured in a scene.
            if ( avatarDatabase == null ) {
                avatarDatabase = Resources.Load<AvatarDatabaseSO>("SO/AvatarDatabase");
            }

            base.Awake();

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected override void OnDestroy() {
            if ( Instance == this ) {
                Instance = null;
            }

            base.OnDestroy();
        }

        protected override void Configure( IContainerBuilder builder ) {
            // Shared ScriptableObject databases
            if ( avatarDatabase != null ) {
                builder.RegisterInstance(avatarDatabase).AsSelf();
            }

            // Data layer (persistent singletons)
            if ( useMockRepository ) {
                builder.Register<MockPlayerRepository>(Lifetime.Singleton)
                       .As<IPlayerRepository>()
                       .As<IDataRepository<PlayerProfileData>>();
            }
            else {
                builder.Register<RestPlayerRepository>(Lifetime.Singleton)
                       .WithParameter("baseUrl", apiBaseUrl)
                       .As<IPlayerRepository>()
                       .As<IDataRepository<PlayerProfileData>>();
            }

            builder.Register<AuthService>(Lifetime.Singleton).As<IAuthService>();
            builder.Register<PlayerProfileService>(Lifetime.Singleton).As<IPlayerProfileService>();
            builder.Register<LocalPlayerService>(Lifetime.Singleton)
                   .As<ILocalPlayerService>()
                   .AsSelf();
        }

        /// <summary>
        /// Scene-local scopes use this to attach themselves to the persistent root.
        /// </summary>
        protected override LifetimeScope FindParent() => Instance;

        /// <summary>
        /// Injects into the [Inject] members of a GameObject living in a scene that has no
        /// LifetimeScope of its own (e.g. MainMenuScene). Call it from Awake/Start.
        /// </summary>
        public void InjectGameObject( GameObject target ) {
            if ( target == null ) return;
            Container?.InjectGameObject(target);
        }

        /// <summary>
        /// Resolves a service from the persistent container, or false when unavailable.
        /// </summary>
        public bool TryResolve<T>( out T instance ) {
            if ( Container != null && Container.TryResolve(out instance) ) {
                return true;
            }

            instance = default;
            return false;
        }
    }
}
