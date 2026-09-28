using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI.Strategy;
using Fusion;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public enum LobbyChangeReason { Initialized, PlayerJoined, PlayerLeft, SeatsChanged, GameStarted, TurnChanged }

    /// <summary>
    /// The single hub between network state and dumb UI views.
    /// Concrete views (StartGameUI, SessionDisplayUI, TableVisualLayoutManager)
    /// know nothing about seats, authority or lobby rules: UiManager detects
    /// replicated changes in Render() via Fusion change detectors, resolves
    /// plain data, and pushes it down through Render*() functions.
    /// </summary>
    public class UiManager : NetworkBehaviour, IUiStrategyHost {
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

        private NetworkBehaviour.ChangeDetector seatDetector;
        private NetworkBehaviour.ChangeDetector gameDetector;

        private ILocalPlayerService localPlayerService;

        [Inject]
        void Construct( ILocalPlayerService localPlayerService ) {
            this.localPlayerService = localPlayerService;
        }

        // Last state actually pushed to the views. Keyed to the seat MODEL's
        // revision (not the replicated dict count): the views render model
        // data, so only a model change guarantees a re-render is worthwhile,
        // and a late model catch-up always triggers one.
        private int lastRenderedSeatRevision = -1;
        private bool lastRenderedStarted;

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
            //_ = Context;
        }

        private void OnEnable() {
            //ResolveReferences();

            //SubscribeEvents();

            // DISABLED: UI strategy pipeline commented out (freeze investigation).
            // A peer that joins after the match started must not flash the
            // lobby UI first, so pick the strategy from the replicated flag.
            //SwitchTo( Context.IsGameRunning
            //    ? new InGameUiStrategy()
            //    : new LobbyUiStrategy() );

            //RefreshLobby( LobbyChangeReason.Initialized );
        }

        private void OnDisable() {
            // DISABLED: UI strategy pipeline commented out (freeze investigation).
            //strategy?.OnExit();
            //strategy = null;
        }

        public override void Spawned() {
            ResolveReferences();

            if ( startGameUI != null && startGameUI.StartButton != null ) {
                startGameUI.StartButton.onClick.AddListener(HandleStartClicked);
            }

            // Change detectors only report deltas, so paint current state once.
            RenderAll();
        }

        public override void Despawned( NetworkRunner runner, bool hasState ) {
            if ( startGameUI != null && startGameUI.StartButton != null ) {
                startGameUI.StartButton.onClick.RemoveListener(HandleStartClicked);
            }
        }

        public override void Render() {
            if ( seatDetector == null ) {
                seatDetector = TryCreateDetector(seatManager);
                if ( seatDetector != null ) {
                    // Paint once: anything that changed between Spawned and
                    // detector creation is already baked into its snapshot.
                    RenderSeats();
                    RenderStartButton();
                }
            }

            if ( gameDetector == null ) {
                gameDetector = TryCreateDetector(tienLenGameController);
                if ( gameDetector != null ) {
                    RenderStartButton();
                    RenderGamePhase();
                }
            }

            if ( seatDetector != null && seatManager != null ) {
                foreach ( var propertyName in seatDetector.DetectChanges(seatManager) ) {
                    switch ( propertyName ) {
                        case nameof(SeatManager.networkOccupiedSeats): {
                                RenderSeats();
                                RenderStartButton();
                                break;
                            }
                    }
                }
            }

            if ( gameDetector != null && tienLenGameController != null ) {
                foreach ( var propertyName in gameDetector.DetectChanges(tienLenGameController) ) {
                    switch ( propertyName ) {
                        case nameof(TienLenGameController.IsGameStarted): {
                                RenderStartButton();
                                RenderGamePhase();
                                break;
                            }
                        case nameof(TienLenGameController.CurrentTurnSeat): {
                                RenderTurn();
                                break;
                            }
                        case nameof(TienLenGameController.WinnerSeat): {
                                RenderGameOver();
                                break;
                            }
                        case nameof(TienLenGameController.LastPassSeat): {
                                RenderPassNote();
                                break;
                            }
                    }
                }
            }

            // Re-render whenever the pushed state no longer matches what the
            // views show. Revision-based: fires exactly when the model the
            // views read has caught up, regardless of callback ordering.
            int seatRevision = seatManager != null ? seatManager.SeatRevision : -1;
            bool started = IsGameLogicReady() && tienLenGameController.IsGameStarted;

            if ( seatRevision != lastRenderedSeatRevision || started != lastRenderedStarted ) {
                lastRenderedSeatRevision = seatRevision;
                lastRenderedStarted = started;
                RenderSeats();
                RenderStartButton();
                RenderGamePhase();
            }
        }

        #region dumb-view renderers (data pushed down, no logic in views)

        private void RenderAll() {
            RenderSession();
            RenderSeats();
            RenderStartButton();
            RenderGamePhase();
            RenderTurn();
            RenderPassNote();

            lastRenderedSeatRevision = seatManager != null ? seatManager.SeatRevision : -1;
            lastRenderedStarted = IsGameLogicReady() && tienLenGameController.IsGameStarted;
        }

        private void RenderSession() {
            if ( sessionDisplayUI == null ) return;

            string sessionName = Runner != null && Runner.SessionInfo.IsValid
                ? Runner.SessionInfo.Name
                : null;

            sessionDisplayUI.RenderSession(sessionName);
        }

        private void RenderSeats() {
            if ( tableVisualLayoutManager == null || seatManager == null ) return;
            if ( seatManager.Object == null || !seatManager.Object.IsValid ) return;

            tableVisualLayoutManager.RenderSeats(
                seatManager.GetSeatedPlayers(),
                seatManager.GetNetworkPlayerMap(),
                Runner.LocalPlayer);

            // Publish the local logic player once its seat resolves, so
            // deal fan-out and game logic stop seeing a null local player.
            if ( localPlayerService != null ) {
                TienLenPlayer logic = seatManager.GetLogicPlayer(Runner.LocalPlayer);
                if ( logic != null
                    && !ReferenceEquals(localPlayerService.GetLocalLogicPlayer(), logic) ) {
                    localPlayerService.SetLocalLogicPlayer(logic);
                }
            }
        }

        private bool IsGameLogicReady() {
            return tienLenGameController != null
                && tienLenGameController.Object != null
                && tienLenGameController.Object.IsValid;
        }

        private void RenderStartButton() {
            if ( startGameUI == null || !IsGameLogicReady() ) return;

            int playerCount = seatManager != null
                ? seatManager.GetNetworkPlayer().Count
                : 0;

            startGameUI.RenderStartButton(
                playerCount,
                Object.HasStateAuthority,
                tienLenGameController.CanStartGame(),
                tienLenGameController.IsGameStarted);
        }

        private void RenderGamePhase() {
            if ( actionPanel == null || !IsGameLogicReady() ) return;

            if ( tienLenGameController.IsGameStarted ) {
                actionPanel.EnterGame();
                // Turn seat may equal its default with no detectable delta
                // (e.g. starter is seat 0), so push explicitly.
                RenderTurn();
            }
            else {
                actionPanel.ReturnToLobby();
            }
        }

        private void RenderTurn() {
            if ( actionPanel == null || !IsGameLogicReady() ) return;
            actionPanel.RenderTurnState(IsLocalTurn());
        }

        private bool IsLocalTurn() {
            if ( !IsGameLogicReady() ) return false;
            if ( tienLenGameController.WinnerSeat != -1 ) return false;
            int localSeat = ResolveLocalSeat();
            return localSeat != -1 && localSeat == tienLenGameController.CurrentTurnSeat;
        }

        private int ResolveLocalSeat() {
            if ( seatManager == null ) return -1;
            foreach ( var kvp in seatManager.GetNetworkPlayerMap() ) {
                var netPlayer = kvp.Value;
                if ( netPlayer == null ) continue;
                if ( netPlayer.Object != null && netPlayer.Object.IsValid
                    && netPlayer.Object.InputAuthority == Runner.LocalPlayer ) {
                    return kvp.Key;
                }
                if ( netPlayer.PlayerRef == Runner.LocalPlayer ) {
                    return kvp.Key;
                }
            }
            return -1;
        }

        private string SeatDisplayName( int seat ) {
            string name = $"Seat {seat}";
            if ( seatManager != null ) {
                var map = seatManager.GetNetworkPlayerMap();
                if ( map.TryGetValue(seat, out var netPlayer) && netPlayer != null ) {
                    name = (string)netPlayer.PlayerName;
                }
            }
            return name;
        }

        private void RenderPassNote() {
            if ( !IsGameLogicReady() ) return;
            int seat = tienLenGameController.LastPassSeat;
            if ( sessionDisplayUI == null ) return;

            // -1 clears the note (a newer play superseded the pass).
            sessionDisplayUI.RenderAnnouncement(
                seat == -1 ? string.Empty : $"{SeatDisplayName(seat)} passed");
        }

        private void RenderGameOver() {
            if ( !IsGameLogicReady() ) return;
            int seat = tienLenGameController.WinnerSeat;
            if ( seat == -1 ) return;

            string name = $"Seat {seat}";
            if ( seatManager != null ) {
                var map = seatManager.GetNetworkPlayerMap();
                if ( map.TryGetValue(seat, out var netPlayer) && netPlayer != null ) {
                    name = (string)netPlayer.PlayerName;
                }
            }

            sessionDisplayUI?.RenderAnnouncement($"{name} wins!");
            actionPanel?.RenderTurnState(false);
            actionPanel?.ReturnToLobby();
        }

        private void HandleStartClicked() {
            // Only the host (state authority) may start, and only when
            // enough players have joined.
            if ( !Object.HasStateAuthority ) return;
            if ( tienLenGameController == null ) return;
            if ( tienLenGameController.IsGameStarted ) return;
            if ( !tienLenGameController.CanStartGame() ) return;

            tienLenGameController.StartGame();
        }

        private static NetworkBehaviour.ChangeDetector TryCreateDetector( NetworkBehaviour source ) {
            if ( source == null || source.Object == null || !source.Object.IsValid ) return null;
            return source.GetChangeDetector(NetworkBehaviour.ChangeDetector.Source.SimulationState);
        }

        #endregion

        /// <summary>
        /// The one hub every subsystem calls when something on the table
        /// changed. Safe to call from any peer; the active strategy owns
        /// the resulting presentation.
        /// </summary>
        public void RefreshLobby( LobbyChangeReason reason ) {
            // DISABLED: UI strategy pipeline commented out (freeze investigation).
            //Debug.Log( $"[UiManager] RefreshLobby reason={reason}" );

            //if ( strategy == null ) {
            //    Debug.LogWarning( "[UiManager] RefreshLobby called with no active strategy, ignoring." );
            //    return;
            //}

            //strategy.OnRefresh( reason, Context );
        }

        public void SwitchTo( IUiStrategy next ) {
            // DISABLED: UI strategy pipeline commented out (freeze investigation).
            //if ( next == null || ReferenceEquals( next, strategy ) ) return;

            //Debug.Log( $"[UiManager] Strategy {strategy?.GetType().Name ?? "None"} -> {next.GetType().Name}" );

            //strategy?.OnExit();
            //strategy = next;
            //strategy.OnEnter( Context );
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
                tableVisualLayoutManager);
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
