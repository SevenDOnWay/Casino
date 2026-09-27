using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.UI.Strategy;
using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Script.TienLen.UI {
    public enum LobbyChangeReason { Initialized, PlayerJoined, PlayerLeft, SeatsChanged, GameStarted, TurnChanged }

    /// <summary>
    /// Single entry point for every UI refresh. It resolves dependencies,
    /// subscribes to the sources of truth (seat + lobby events) and forwards
    /// each change to the currently installed <see cref="IUiStrategy"/>,
    /// which decides what the screen should look like.
    /// </summary>
    public class UiManager : MonoBehaviour, IUiStrategyHost {
        [Header("Dependencies")]
        [SerializeField] private TienLenGameController tienLenGameController;
        [SerializeField] private LobbySessionController lobbySessionController;
        [SerializeField] private SeatManager seatManager;

        [Space(5)]
        [Header("UI Elements")]
        [SerializeField] private SessionDisplayUI sessionDisplayUI;
        [SerializeField] private StartGameUI startGameUI;
        [SerializeField] private ActionPanel actionPanel;
        [FormerlySerializedAs("TableVisualLayoutManager")]
        [SerializeField] private TableVisualLayoutManager tableVisualLayoutManager;

        private UiStrategyContext context;
        private IUiStrategy strategy;

        private UiStrategyContext Context {
            get {
                context ??= BuildContext();
                return context;
            }
        }

        private void Awake() {
            // Warm the context so the first refresh never races resolution.
            _ = Context;
        }

        private void OnEnable() {
            ResolveReferences();

            SubscribeEvents();

            // A peer that joins after the match started must not flash the
            // lobby UI first, so pick the strategy from the replicated flag.
            SwitchTo( Context.IsGameRunning
                ? new InGameUiStrategy()
                : new LobbyUiStrategy() );

            RefreshLobby( LobbyChangeReason.Initialized );
        }

        private void OnDisable() {
            UnsubscribeEvents();

            strategy?.OnExit();
            strategy = null;
        }

        #region events

        private void SubscribeEvents() {
            if ( seatManager != null ) {
                seatManager.OnSeatsChanged += HandleSeatsChanged;
            }

            if ( lobbySessionController != null ) {
                lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoined;
                lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
            }
        }

        private void UnsubscribeEvents() {
            if ( seatManager != null ) {
                seatManager.OnSeatsChanged -= HandleSeatsChanged;
            }

            if ( lobbySessionController != null ) {
                lobbySessionController.OnPlayerJoinedEvent -= HandlePlayerJoined;
                lobbySessionController.OnPlayerLeftEvent -= HandlePlayerLeft;
            }
        }

        private void HandleSeatsChanged() {
            RefreshLobby( LobbyChangeReason.SeatsChanged );
        }

        private void HandlePlayerJoined( NetworkRunner runner ) {
            RefreshLobby( LobbyChangeReason.PlayerJoined );
        }

        private void HandlePlayerLeft( NetworkRunner runner ) {
            RefreshLobby( LobbyChangeReason.PlayerLeft );
        }

        #endregion

        /// <summary>
        /// The one hub every subsystem calls when something on the table
        /// changed. Safe to call from any peer; the active strategy owns
        /// the resulting presentation.
        /// </summary>
        public void RefreshLobby( LobbyChangeReason reason ) {
            Debug.Log( $"[UiManager] RefreshLobby reason={reason}" );

            if ( strategy == null ) {
                Debug.LogWarning( "[UiManager] RefreshLobby called with no active strategy, ignoring." );
                return;
            }

            strategy.OnRefresh( reason, Context );
        }

        public void SwitchTo( IUiStrategy next ) {
            if ( next == null || ReferenceEquals( next, strategy ) ) return;

            Debug.Log( $"[UiManager] Strategy {strategy?.GetType().Name ?? "None"} -> {next.GetType().Name}" );

            strategy?.OnExit();
            strategy = next;
            strategy.OnEnter( Context );
        }

        #region wiring

        private UiStrategyContext BuildContext() {
            ResolveReferences();

            return new UiStrategyContext(
                this,
                tienLenGameController,
                sessionDisplayUI,
                startGameUI,
                actionPanel,
                tableVisualLayoutManager );
        }

        /// <summary>
        /// Serialized references are the source of truth, but the scene may be
        /// assembled by hand, so fall back to a scene lookup instead of NREing
        /// during a refresh.
        /// </summary>
        private void ResolveReferences() {
            if ( tienLenGameController == null ) {
                tienLenGameController = FindFirstObjectByType<TienLenGameController>();
            }

            if ( lobbySessionController == null ) {
                lobbySessionController = FindFirstObjectByType<LobbySessionController>();
            }

            if ( seatManager == null ) {
                seatManager = FindFirstObjectByType<SeatManager>();
            }
        }

        #endregion
    }
}
