using Assets.Script.Data.Models;
using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game.Controller;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : NetworkBehaviour {
        [Header("Scene Dependencies")]
        [SerializeField] private UiManager uiManager;

        [Header("Game Configuration")]
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private float turnDuration = 10f;
        public float TurnDuration => turnDuration;

        private const int totalSeats = 4;

        [Header("Dependencies")]
        [Inject] private MatchController matchController;
        private LocalPlayerService localPlayerService;
        private TurnManager turnManager;
        private ISeatQueryService seatQueryService;
        private TienLenRuleValidator validator;
        private IPlayerProfileService profileService;

        [Header("Domain Services")]
        private TienLenMatchOutcomeReporter outcomeReporter;
        private TienLenTurnHandler turnHandler;
        private TienLenMigrationHandler migrationHandler;

        private readonly Dictionary<PlayerRef, List<Card>> pendingHands = new();
        private int explicitBetStake = 0;

        #region Networked Properties

        [Networked] public NetworkBool IsGameStarted { get; set; }
        [Networked] public int CurrentTurnSeat { get; set; }
        [Networked] public int WinnerSeat { get; set; }
        [Networked] public int LastPassSeat { get; set; }
        [Networked] public int BetStake { get; set; }
        [Networked, Capacity(13)] public NetworkArray<NetworkCard> CurrentTableCards => default;
        [Networked] public int CurrentTableCardCount { get; set; }
        [Networked] public TickTimer TurnTimer { get; set; }

        #endregion

        public int CurrentBetStake {
            get {
                if ( Object != null && Object.IsValid ) {
                    try {
                        return BetStake > 0 ? BetStake : (explicitBetStake > 0 ? explicitBetStake : 5);
                    }
                    catch ( InvalidOperationException ) { }
                }
                return explicitBetStake > 0 ? explicitBetStake : 5;
            }
        }

        public float RemainingTurnTimeNormalized {
            get {
                if ( !IsGameStarted || CurrentTurnSeat == -1 || turnDuration <= 0f ) return 0f;
                if ( TurnTimer.IsRunning ) {
                    float? remaining = TurnTimer.RemainingTime(Runner);
                    if ( remaining.HasValue ) {
                        return Mathf.Clamp01(remaining.Value / turnDuration);
                    }
                }
                return 0f;
            }
        }

        public int RegisteredPlayerCount =>
            seatQueryService != null ? seatQueryService.GetNetworkPlayer().Count : 0;

        public bool HasEnoughPlayersToPlay =>
            Object != null && Object.IsValid && seatQueryService != null && RegisteredPlayerCount >= minPlayersToStart;

        public bool CanStartGame() {
            if ( matchController != null ) return matchController.CanStartGame();
            if ( !HasEnoughPlayersToPlay ) return false;
            if ( IsGameStarted ) return false;
            return true;
        }

        public void StartGame() {
            if ( matchController != null ) {
                matchController.StartGame();
            }
        }

        public void TriggerGameOver( int winnerSeat ) {
            if ( matchController != null ) {
                matchController.EndGame(winnerSeat);
            }
            else {
                WinnerSeat = winnerSeat;
            }
        }

        [Inject]
        public void Construct(
            LocalPlayerService localPlayerService,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            ISeatQueryService seatQueryService,
            TienLenRuleValidator validator,
            IPlayerProfileService profileService = null,
            TienLenMatchOutcomeReporter outcomeReporter = null,
            TienLenMigrationHandler migrationHandler = null,
            MatchController matchController = null ) {
            this.localPlayerService = localPlayerService;
            this.turnManager = turnManager;
            this.seatQueryService = seatQueryService;
            this.validator = validator;
            this.profileService = profileService;
            if ( matchController != null ) this.matchController = matchController;

            this.outcomeReporter = outcomeReporter ?? new TienLenMatchOutcomeReporter(seatQueryService, localPlayerService, profileService);
            this.turnHandler = new TienLenTurnHandler(cardCombinationEvaluator, turnManager, validator);
            this.migrationHandler = migrationHandler ?? new TienLenMigrationHandler(seatQueryService, turnManager, cardCombinationEvaluator, validator);
        }

        public override void Spawned() {
            base.Spawned();

            if ( Object.HasStateAuthority ) {
                int stake = 5;
                if ( Runner != null && Runner.SessionInfo != null && Runner.SessionInfo.Properties != null ) {
                    if ( Runner.SessionInfo.Properties.TryGetValue("BetStake", out var prop) ) {
                        int.TryParse(prop.PropertyValue.ToString(), out stake);
                    }
                }
                BetStake = stake > 0 ? stake : 5;

                if ( Runner.IsResume ) {
                    ResumeFromMigration();
                }
            }
        }

        private void Update() {
            if ( pendingHands.Count == 0 ) return;
            if ( Object == null || !Object.IsValid ) return;

            var owners = new List<PlayerRef>(pendingHands.Keys);
            foreach ( var owner in owners ) {
                if ( TryApplyHand(owner, pendingHands[owner]) ) {
                    pendingHands.Remove(owner);
                }
            }
        }

        public override void FixedUpdateNetwork() {
            if ( !Object.HasStateAuthority ) return;
            if ( !IsGameStarted || WinnerSeat != -1 ) return;

            if ( TurnTimer.Expired(Runner) ) {
                turnManager?.HandleTurnTimeout();
            }
        }

        public int SeatOfPlayer( TienLenPlayer player ) {
            if ( seatQueryService == null || player == null ) return -1;
            foreach ( var kvp in seatQueryService.GetSeatedPlayers() ) {
                if ( ReferenceEquals(kvp.Value, player) ) return kvp.Key;
            }
            return -1;
        }

        public void MirrorTurnSeat( bool isDealingStart = false ) {
            int seat = SeatOfPlayer(turnManager?.CurrentPlayer);
            if ( seat != -1 ) {
                CurrentTurnSeat = seat;
                if ( Object.HasStateAuthority ) {
                    float duration = isDealingStart ? (turnDuration + 3.5f) : turnDuration;
                    TurnTimer = TickTimer.CreateFromSeconds(Runner, duration);

                    if ( turnManager.CurrentPlayer != null && turnManager.CurrentPlayer.IsInactive ) {
                        AutoActInactivePlayerDelayed().Forget();
                    }
                }
            }
        }

        private async UniTaskVoid AutoActInactivePlayerDelayed() {
            await UniTask.Delay(600, cancellationToken: this.GetCancellationTokenOnDestroy());
            if ( !Object.HasStateAuthority || WinnerSeat != -1 || !IsGameStarted ) return;
            if ( turnManager?.CurrentPlayer != null && turnManager.CurrentPlayer.IsInactive ) {
                turnManager.AutoActForCurrentPlayer();
            }
        }

        #region Card Distribution & Hands

        public void BroadcastHands() {
            if ( !Object.HasStateAuthority || seatQueryService == null ) return;

            var networkMap = seatQueryService.GetNetworkPlayerMap();
            foreach ( var kvp in networkMap ) {
                if ( kvp.Value == null || !kvp.Value.PlayerRef.IsValid ) continue;
                PlayerRef targetRef = kvp.Value.PlayerRef;

                TienLenPlayer logicPlayer = seatQueryService.GetLogicPlayer(targetRef);
                if ( logicPlayer?.Hand == null || logicPlayer.Hand.Count == 0 ) continue;

                NetworkCard[] netCards = logicPlayer.Hand.Cards
                    .Select(card => new NetworkCard(card))
                    .ToArray();

                RPCReceiveHand(targetRef, netCards);
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCReceiveHand( PlayerRef owner, NetworkCard[] cards ) {
            if ( cards == null || cards.Length == 0 ) return;
            if ( !Object.HasStateAuthority && owner != Runner.LocalPlayer ) return;

            List<Card> hand = cards.Select(card => card.ToCard()).ToList();
            pendingHands[owner] = hand;
            TryApplyHand(owner, hand);
        }

        private bool TryApplyHand( PlayerRef owner, List<Card> hand ) {
            if ( hand == null || hand.Count == 0 ) return false;
            TienLenPlayer player = seatQueryService?.GetLogicPlayer(owner) ?? localPlayerService?.GetLocalLogicPlayer();
            if ( player?.Hand == null ) return false;

            player.Hand.Clear();
            player.Hand.AddCard(hand);
            return true;
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPCNotifyMatchReported() {
            Debug.Log("[TienLenGameController] Match outcome recorded in backend. Refreshing local player profile...");
            profileService?.RefreshProfileAsync().Forget();
        }

        #endregion

        #region Host Migration

        public void ResumeFromMigration() {
            if ( !Object.HasStateAuthority ) return;
            Debug.Log("[TienLenGameController] Resuming game state after Host Migration...");

            if ( !IsGameStarted ) {
                Debug.Log("[TienLenGameController] Game has not started yet. Lobby state preserved.");
                return;
            }

            List<Card> tableCards = new();
            for ( int i = 0; i < CurrentTableCardCount; i++ ) {
                tableCards.Add(CurrentTableCards[i].ToCard());
            }

            if ( migrationHandler.TryResumeState(CurrentTableCardCount, tableCards, CurrentTurnSeat, out int singleWinnerSeat, out bool turnAdvanced) ) {
                if ( singleWinnerSeat != -1 ) {
                    TriggerGameOver(singleWinnerSeat);
                    return;
                }

                if ( turnAdvanced ) {
                    MirrorTurnSeat();
                }

                TurnTimer = TickTimer.CreateFromSeconds(Runner, turnDuration + 5f);
                RPCRequestHandReport();
            }
        }

        public void HandlePlayerLeftMidGame( PlayerRef playerRef ) {
            if ( !Object.HasStateAuthority || !IsGameStarted || WinnerSeat != -1 ) return;

            if ( migrationHandler.ProcessPlayerLeftMidGame(playerRef, out int winnerSeat, out bool shouldAutoAct) ) {
                if ( winnerSeat != -1 ) {
                    TriggerGameOver(winnerSeat);
                    return;
                }

                if ( shouldAutoAct ) {
                    turnManager?.AutoActForCurrentPlayer();
                }
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPCRequestHandReport() {
            if ( localPlayerService != null ) {
                var localLogic = localPlayerService.GetLocalLogicPlayer();
                var localNet = localPlayerService.GetLocalNetworkPlayer();
                if ( localLogic?.Hand?.Cards != null && localNet != null ) {
                    NetworkCard[] netCards = localLogic.Hand.Cards
                        .Select(c => new NetworkCard(c))
                        .ToArray();
                    localNet.RPCReportHandState(netCards);
                }
            }
        }

        public void HandleReportedHand( PlayerRef sender, NetworkCard[] cards ) {
            if ( !Object.HasStateAuthority || cards == null ) return;
            TienLenPlayer player = GetPlayer(sender);
            if ( player != null ) {
                migrationHandler.RestoreReportedHand(player, cards.Select(c => c.ToCard()));
            }
        }

        #endregion

        #region Test & Reporting Helpers

        public void SetDependenciesForTest( ISeatQueryService seatQuery, LocalPlayerService localPlayer, IPlayerProfileService profile, int stake = 5 ) {
            this.seatQueryService = seatQuery;
            this.localPlayerService = localPlayer;
            this.profileService = profile;
            this.explicitBetStake = stake;

            if ( outcomeReporter == null ) {
                outcomeReporter = new TienLenMatchOutcomeReporter(seatQuery, localPlayer, profile);
            }
            else {
                outcomeReporter.SetDependencies(seatQuery, localPlayer, profile);
            }

            if ( Object != null && Object.IsValid ) {
                try {
                    this.BetStake = stake;
                }
                catch ( InvalidOperationException ) { }
            }
        }

        public MatchResultReportRequest BuildMatchReport( int winnerSeat ) {
            if ( outcomeReporter == null ) {
                outcomeReporter = new TienLenMatchOutcomeReporter(seatQueryService, localPlayerService, profileService);
            }
            return outcomeReporter.BuildMatchReport(winnerSeat, CurrentBetStake);
        }

        private TienLenPlayer GetPlayer( PlayerRef sender ) {
            if ( !sender.IsValid || seatQueryService == null ) return null;
            return seatQueryService.GetLogicPlayer(sender);
        }

        #endregion
    }
}
