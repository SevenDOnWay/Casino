using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Effects;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public enum LobbyChangeReason {
        Initialized,
        SeatsChanged,
        GameStarted,
        TurnChanged,
        PassOccurred,
        GameOver
    }

    /// <summary>
    /// Central UI presenter bridging network state to concrete dumb UI views.
    /// Pushes plain presentation data down on change events or change detector deltas.
    /// Contains NO game logic mutations or scene search fallbacks.
    /// </summary>
    public class UiManager : NetworkBehaviour {
        [Header("Dependencies")]
        [SerializeField] private TienLenGameController tienLenGameController;
        [SerializeField] private SessionController lobbySessionController;
        [SerializeField] private SeatManager seatManager;

        [Header("UI Views")]
        [SerializeField] private SessionDisplayUI sessionDisplayUI;
        [SerializeField] private StartGameUI startGameUI;
        [SerializeField] private ActionPanel actionPanel;
        [SerializeField] private TableVisualLayoutManager tableVisualLayoutManager;
        [SerializeField] private PlayerWinEffectController winEffectController;

        private ISeatQueryService seatQueryService;
        private ILocalPlayerService localPlayerService;

        private NetworkBehaviour.ChangeDetector seatDetector;
        private NetworkBehaviour.ChangeDetector gameDetector;

        private int lastRenderedSeatRevision = -1;
        private bool lastRenderedStarted;

        [Inject]
        void Construct(
            ISeatQueryService seatQueryService,
            ILocalPlayerService localPlayerService,
            TienLenGameController tienLenGameController,
            TableVisualLayoutManager tableVisualLayoutManager,
            PlayerWinEffectController winEffectController = null ) {
            this.seatQueryService = seatQueryService;
            this.localPlayerService = localPlayerService;
            if ( this.tienLenGameController == null ) this.tienLenGameController = tienLenGameController;
            if ( this.tableVisualLayoutManager == null ) this.tableVisualLayoutManager = tableVisualLayoutManager;
            if ( this.winEffectController == null && winEffectController != null ) this.winEffectController = winEffectController;
        }

        private ISeatQueryService SeatQuery => seatQueryService ?? seatManager;

        private void Awake() {
            EnsureWinEffectController();
        }

        private void EnsureWinEffectController() {
            if ( winEffectController == null ) {
                winEffectController = GetComponentInChildren<PlayerWinEffectController>(true) ?? FindAnyObjectByType<PlayerWinEffectController>();
                if ( winEffectController == null ) {
                    winEffectController = gameObject.AddComponent<PlayerWinEffectController>();
                }
            }
        }

        public override void Spawned() {
            EnsureWinEffectController();
            if ( startGameUI != null && startGameUI.StartButton != null ) {
                startGameUI.StartButton.onClick.AddListener(HandleStartClicked);
            }

            RenderAll();
        }

        public override void Despawned( NetworkRunner runner, bool hasState ) {
            if ( startGameUI != null && startGameUI.StartButton != null ) {
                startGameUI.StartButton.onClick.RemoveListener(HandleStartClicked);
            }
        }

        public override void Render() {
            if ( seatDetector == null && seatManager != null ) {
                seatDetector = TryCreateDetector(seatManager);
                if ( seatDetector != null ) {
                    RenderSeats();
                    RenderStartButton();
                }
            }

            if ( gameDetector == null && tienLenGameController != null ) {
                gameDetector = TryCreateDetector(tienLenGameController);
                if ( gameDetector != null ) {
                    RenderStartButton();
                    RenderGamePhase();
                }
            }

            if ( seatDetector != null && seatManager != null ) {
                foreach ( var propertyName in seatDetector.DetectChanges(seatManager) ) {
                    if ( propertyName == nameof(SeatManager.networkOccupiedSeats) ) {
                        Refresh(LobbyChangeReason.SeatsChanged);
                    }
                }
            }

            if ( gameDetector != null && tienLenGameController != null ) {
                foreach ( var propertyName in gameDetector.DetectChanges(tienLenGameController) ) {
                    switch ( propertyName ) {
                        case nameof(TienLenGameController.IsGameStarted):
                            Refresh(LobbyChangeReason.GameStarted);
                            break;
                        case nameof(TienLenGameController.CurrentTurnSeat):
                            Refresh(LobbyChangeReason.TurnChanged);
                            break;
                        case nameof(TienLenGameController.WinnerSeat):
                            Refresh(LobbyChangeReason.GameOver);
                            break;
                        case nameof(TienLenGameController.LastPassSeat):
                            Refresh(LobbyChangeReason.PassOccurred);
                            break;
                    }
                }
            }

            // Catch-up if model revision advanced without direct network dict change
            int seatRevision = SeatQuery != null ? SeatQuery.SeatRevision : -1;
            bool started = IsGameLogicReady() && tienLenGameController.IsGameStarted;

            if ( seatRevision != lastRenderedSeatRevision || started != lastRenderedStarted ) {
                lastRenderedSeatRevision = seatRevision;
                lastRenderedStarted = started;
                RenderSeats();
                RenderStartButton();
                RenderGamePhase();
            }

            UpdateTurnTimers();
        }

        /// <summary>
        /// Explicit targeted rendering based on the reason for change.
        /// </summary>
        public void Refresh( LobbyChangeReason reason ) {
            switch ( reason ) {
                case LobbyChangeReason.Initialized:
                    RenderAll();
                    break;
                case LobbyChangeReason.SeatsChanged:
                    RenderSeats();
                    RenderStartButton();
                    break;
                case LobbyChangeReason.GameStarted:
                    RenderStartButton();
                    RenderGamePhase();
                    break;
                case LobbyChangeReason.TurnChanged:
                    RenderTurn();
                    break;
                case LobbyChangeReason.PassOccurred:
                    RenderPassNote();
                    break;
                case LobbyChangeReason.GameOver:
                    RenderGameOver();
                    break;
            }
        }

        #region View Renderers

        private void RenderAll() {
            RenderSession();
            RenderSeats();
            RenderStartButton();
            RenderGamePhase();
            RenderTurn();
            RenderPassNote();
            UpdateTurnTimers();

            lastRenderedSeatRevision = SeatQuery != null ? SeatQuery.SeatRevision : -1;
            lastRenderedStarted = IsGameLogicReady() && tienLenGameController.IsGameStarted;
        }

        public void ShowAnnouncement( string message ) {
            sessionDisplayUI?.RenderAnnouncement(message);
        }

        private void RenderSession() {
            if ( sessionDisplayUI == null ) return;

            string sessionName = Runner != null && Runner.SessionInfo.IsValid
                ? Runner.SessionInfo.Name
                : null;

            sessionDisplayUI.RenderSession(sessionName);
        }

        private void RenderSeats() {
            if ( tableVisualLayoutManager == null || SeatQuery == null ) return;

            tableVisualLayoutManager.RenderSeats(
                SeatQuery.GetSeatedPlayers(),
                SeatQuery.GetNetworkPlayerMap(),
                Runner != null ? Runner.LocalPlayer : default
            );
        }

        private bool IsGameLogicReady() {
            return tienLenGameController != null
                && tienLenGameController.Object != null
                && tienLenGameController.Object.IsValid;
        }

        private void RenderStartButton() {
            if ( startGameUI == null || !IsGameLogicReady() ) return;

            int playerCount = SeatQuery != null
                ? SeatQuery.GetNetworkPlayer().Count
                : 0;

            startGameUI.RenderStartButton(
                playerCount,
                Object.HasStateAuthority,
                tienLenGameController.CanStartGame(),
                tienLenGameController.IsGameStarted
            );
        }

        private void RenderGamePhase() {
            if ( actionPanel == null || !IsGameLogicReady() ) return;

            if ( tienLenGameController.IsGameStarted ) {
                if ( tienLenGameController.WinnerSeat == -1 ) {
                    winEffectController?.StopWinEffect();
                }
                actionPanel.EnterGame();
                RenderTurn();
            }
            else {
                winEffectController?.StopWinEffect();
                actionPanel.ReturnToLobby();
            }
        }

        private void RenderTurn() {
            if ( actionPanel == null || !IsGameLogicReady() ) return;
            if ( tienLenGameController.WinnerSeat == -1 ) {
                winEffectController?.StopWinEffect();
                sessionDisplayUI?.RenderAnnouncement(string.Empty);
            }
            actionPanel.RenderTurnState(IsLocalTurn());
        }

        private bool IsLocalTurn() {
            if ( !IsGameLogicReady() ) return false;
            if ( tienLenGameController.WinnerSeat != -1 ) return false;
            int localSeat = ResolveLocalSeat();
            return localSeat != -1 && localSeat == tienLenGameController.CurrentTurnSeat;
        }

        private int ResolveLocalSeat() {
            if ( SeatQuery == null || Runner == null || !Runner.LocalPlayer.IsValid ) return -1;
            if ( SeatQuery.TryGetSeat(Runner.LocalPlayer, out int seat) ) {
                return seat;
            }
            return -1;
        }

        private string SeatDisplayName( int seat ) {
            string name = $"Seat {seat}";
            if ( SeatQuery != null ) {
                var map = SeatQuery.GetNetworkPlayerMap();
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

            sessionDisplayUI.RenderAnnouncement(
                seat == -1 ? string.Empty : $"{SeatDisplayName(seat)} passed"
            );
        }

        private void RenderGameOver() {
            if ( !IsGameLogicReady() ) return;
            int seat = tienLenGameController.WinnerSeat;
            if ( seat == -1 ) {
                winEffectController?.StopWinEffect();
                sessionDisplayUI?.RenderAnnouncement(string.Empty);
                RenderStartButton();
                RenderGamePhase();
                return;
            }

            string name = SeatDisplayName(seat);
            sessionDisplayUI?.RenderAnnouncement($"{name} wins! Next round starting soon...");
            actionPanel?.RenderTurnState(false);

            Vector3 winPos = Vector3.zero;
            var winnerSlot = tableVisualLayoutManager?.GetSeatByNetworkIndex(seat);
            if ( winnerSlot != null ) {
                winPos = winnerSlot.transform.position;
            }
            winEffectController?.PlayWinEffect(seat, winPos);

            UpdateTurnTimers();
        }

        private void UpdateTurnTimers() {
            if ( tableVisualLayoutManager == null || !IsGameLogicReady() ) return;

            int currentTurnSeat = tienLenGameController.CurrentTurnSeat;
            bool isGameActive = tienLenGameController.IsGameStarted && tienLenGameController.WinnerSeat == -1;

            var allSlots = tableVisualLayoutManager.GetAllVisualSlots();
            if ( allSlots == null ) return;

            float progress = tienLenGameController.RemainingTurnTimeNormalized;

            for ( int i = 0; i < allSlots.Count; i++ ) {
                var slot = allSlots[i];
                if ( slot == null ) continue;

                if ( isGameActive && currentTurnSeat != -1 && slot.BoundSeatIndex == currentTurnSeat ) {
                    slot.UpdateTurnTimer(progress);
                }
                else {
                    slot.StopTurnTimer();
                }
            }
        }

        private void HandleStartClicked() {
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
    }
}
