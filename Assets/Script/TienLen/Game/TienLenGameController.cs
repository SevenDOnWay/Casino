using Assets.Script.Data.Models;
using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Game.Visual;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : NetworkBehaviour {
        [Header("Scene Dependencies")]
        [SerializeField] private UiManager uiManager;
        [SerializeField] private TableVisualLayoutManager tableVisualLayoutManager;
        [SerializeField] private CardHolder tableCenterPosition;
        [SerializeField] private TienLenSO tienLenSO;
        [SerializeField] private TienLenDealingAnimator dealingAnimator;
        [SerializeField] private TienLenTableAnimator tableAnimator;

        [Header("Game Configuration")]
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private float turnDuration = 10f;
        public float TurnDuration => turnDuration;

        private const int totalSeats = 4;

        [Header("Dependency")]
        private CardSpawner cardSpawner;
        private CardCombinationEvaluator cardCombinationEvaluator;
        private LocalPlayerService localPlayerService;
        private TurnManager turnManager;
        private ISeatQueryService seatQueryService;
        private TienLenRuleValidator validator;
        private IPlayerProfileService profileService;

        [Header("Domain Services")]
        private TienLenMatchOutcomeReporter outcomeReporter;
        private TienLenActionHandler actionHandler;
        private TienLenTurnHandler turnHandler;
        private TienLenMigrationHandler migrationHandler;

        // Visual / State caches
        private Dictionary<(CardSuit, CardRank), Sprite> sprites = new();
        private Sprite cardBack;
        private readonly Dictionary<PlayerRef, List<Card>> pendingHands = new();
        private int explicitBetStake = 0;

        #region Networked Properties

        [Networked, OnChangedRender(nameof(OnIsGameStartedChanged))] public NetworkBool IsGameStarted { get; set; }
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
            if ( !HasEnoughPlayersToPlay ) return false;
            if ( IsGameStarted ) return false;
            return true;
        }

        [Inject]
        public void Construct(
            CardSpawner cardSpawner,
            LocalPlayerService localPlayerService,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            ISeatQueryService seatQueryService,
            TienLenRuleValidator validator,
            TableVisualLayoutManager tableVisualLayoutManager,
            IPlayerProfileService profileService = null,
            TienLenMatchOutcomeReporter outcomeReporter = null,
            TienLenActionHandler actionHandler = null,
            TienLenTurnHandler turnHandler = null,
            TienLenMigrationHandler migrationHandler = null ) {
            this.cardSpawner = cardSpawner;
            this.localPlayerService = localPlayerService;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.seatQueryService = seatQueryService;
            this.validator = validator;
            this.profileService = profileService;
            if ( this.tableVisualLayoutManager == null ) this.tableVisualLayoutManager = tableVisualLayoutManager;

            this.outcomeReporter = outcomeReporter ?? new TienLenMatchOutcomeReporter(seatQueryService, localPlayerService, profileService);
            this.actionHandler = actionHandler ?? new TienLenActionHandler(cardCombinationEvaluator, turnManager, validator);
            this.turnHandler = turnHandler ?? new TienLenTurnHandler(cardCombinationEvaluator, turnManager, validator);
            this.migrationHandler = migrationHandler ?? new TienLenMigrationHandler(seatQueryService, turnManager, cardCombinationEvaluator, validator);
        }

        public override void Spawned() {
            base.Spawned();

            EnsureAnimators();

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

            if ( tienLenSO != null ) {
                cardBack = tienLenSO.GetCardBackSprite();
            }
            else {
                Debug.LogWarning("[TienLenGameController] tienLenSO is not assigned.");
            }
        }

        private void EnsureAnimators() {
            if ( dealingAnimator == null ) {
                dealingAnimator = GetComponent<TienLenDealingAnimator>() ?? gameObject.AddComponent<TienLenDealingAnimator>();
                if ( tableCenterPosition != null ) dealingAnimator.TableCenterPosition = tableCenterPosition;
            }
            if ( tableAnimator == null ) {
                tableAnimator = GetComponent<TienLenTableAnimator>() ?? gameObject.AddComponent<TienLenTableAnimator>();
                if ( tableCenterPosition != null ) tableAnimator.TableCenterPosition = tableCenterPosition;
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
                HandleTurnTimeout();
            }
        }

        private void HandleTurnTimeout() {
            if ( !Object.HasStateAuthority || !IsGameStarted || WinnerSeat != -1 ) return;
            if ( turnManager == null || turnManager.CurrentPlayer == null || seatQueryService == null ) return;

            Debug.Log($"[TienLenGameController] Turn timer expired for player {turnManager.CurrentPlayer.Id} (Seat {CurrentTurnSeat}). Auto-acting...");
            AutoActForCurrentPlayer();
        }

        private void AutoActForCurrentPlayer() {
            if ( !Object.HasStateAuthority || !IsGameStarted || WinnerSeat != -1 ) return;
            if ( turnManager == null || turnManager.CurrentPlayer == null || seatQueryService == null ) return;

            var currentPlayer = turnManager.CurrentPlayer;
            PlayerRef currentRef = FindPlayerRef(currentPlayer);

            var decision = turnHandler.DecideAutoAction(currentPlayer, actionHandler.IsFirstPlayOfGame);
            switch ( decision.ActionType ) {
                case AutoActionResultType.Pass:
                    if ( currentRef.IsValid ) {
                        HandlePassRequest(currentRef);
                    }
                    else {
                        if ( actionHandler.TryExecutePass(currentPlayer, out bool clearCenter, out _) ) {
                            if ( clearCenter ) {
                                CurrentTableCardCount = 0;
                                RPCClearTableCenter();
                            }
                            MirrorTurnSeat();
                        }
                        else {
                            turnManager.ForceAdvanceTurn();
                            MirrorTurnSeat();
                        }
                    }
                    break;

                case AutoActionResultType.PlayCards:
                    if ( turnManager.TryPlay(currentPlayer, decision.Combination) ) {
                        actionHandler.IsFirstPlayOfGame = false;
                        if ( currentRef.IsValid ) {
                            HandleAcceptedPlay(currentRef, currentPlayer, decision.CardsToPlay);
                        }
                        else {
                            currentPlayer.TryRemoveCards(decision.CardsToPlay);
                            RPCPlayAccepted(PlayerRef.None, decision.CardsToPlay.Select(c => new NetworkCard(c)).ToArray());
                            CurrentTableCardCount = decision.CardsToPlay.Count;
                            MirrorTurnSeat();
                        }
                    }
                    else {
                        turnManager.ForceAdvanceTurn();
                        MirrorTurnSeat();
                    }
                    break;

                case AutoActionResultType.ForceAdvance:
                    turnManager.ForceAdvanceTurn();
                    MirrorTurnSeat();
                    break;
            }

            if ( Object.HasStateAuthority && (!TurnTimer.IsRunning || TurnTimer.Expired(Runner)) ) {
                TurnTimer = TickTimer.CreateFromSeconds(Runner, turnDuration);
            }
        }

        private PlayerRef FindPlayerRef( TienLenPlayer player ) {
            if ( seatQueryService == null || player == null ) return default;
            var networkMap = seatQueryService.GetNetworkPlayerMap();
            foreach ( var kvp in networkMap ) {
                if ( seatQueryService.GetLogicPlayer(kvp.Value.PlayerRef) == player ) {
                    return kvp.Value.PlayerRef;
                }
            }
            return default;
        }

        #region Game Lifecycle

        public void StartGame() {
            if ( !Object.HasStateAuthority || !CanStartGame() || IsGameStarted ) return;

            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            int bet = CurrentBetStake;

            foreach ( var p in seatedPlayers.Values ) {
                if ( p != null && p.Money < bet ) {
                    p.IsInactive = true;
                    Debug.LogWarning($"[Game] Player {p.Id} has insufficient funds ({p.Money} < {bet}). Marked inactive.");
                }
            }

            int solventCount = seatedPlayers.Values.Count(p => p != null && !p.IsInactive);
            if ( solventCount < minPlayersToStart ) {
                Debug.LogWarning("[Game] Not enough players with sufficient balance to start.");
                CleanUpInactivePlayers();
                return;
            }

            IsGameStarted = true;
            WinnerSeat = -1;
            LastPassSeat = -1;

            Deck deck = new Deck();
            deck.CreateDeck();
            DealCard(deck);
            InitializeTurns();
        }

        private void TriggerGameOver( int winnerSeat ) {
            WinnerSeat = winnerSeat;
            Debug.Log($"[Game] Player at seat {winnerSeat} wins!");

            if ( Object.HasStateAuthority ) {
                ReportMatchOutcome(winnerSeat).Forget();
                WaitAndStartNextGame(winnerSeat).Forget();
            }
        }

        private async UniTaskVoid ReportMatchOutcome( int winnerSeat ) {
            if ( !Object.HasStateAuthority ) return;

            bool success = await outcomeReporter.ReportMatchOutcomeAsync(winnerSeat, CurrentBetStake);
            if ( success ) {
                RPCNotifyMatchReported();
            }
        }

        private async UniTaskVoid WaitAndStartNextGame( int previousWinnerSeat ) {
            const float celebrationDuration = 3.5f;
            var ct = this.GetCancellationTokenOnDestroy();

            try {
                await UniTask.Delay(TimeSpan.FromSeconds(celebrationDuration), cancellationToken: ct);
            }
            catch ( OperationCanceledException ) {
                return;
            }

            if ( !Object.HasStateAuthority ) return;

            CleanUpInactivePlayers();

            if ( !HasEnoughPlayersToPlay ) {
                Debug.LogWarning("[TienLenGameController] Cannot continue game: not enough players.");
                IsGameStarted = false;
                WinnerSeat = -1;
                CurrentTurnSeat = -1;
                CurrentTableCardCount = 0;
                actionHandler.IsFirstPlayOfGame = true;
                RPCClearTableCenter();
                return;
            }

            StartNextRound(previousWinnerSeat);
        }

        private void CleanUpInactivePlayers() {
            if ( seatQueryService == null || !Object.HasStateAuthority ) return;
            var seated = seatQueryService.GetSeatedPlayers();
            var netMap = seatQueryService.GetNetworkPlayerMap();
            var toRemove = new List<PlayerRef>();

            foreach ( var kvp in seated ) {
                if ( kvp.Value != null && kvp.Value.IsInactive ) {
                    if ( netMap != null && netMap.TryGetValue(kvp.Key, out var netPlayer) && netPlayer != null ) {
                        toRemove.Add(netPlayer.PlayerRef);
                    }
                }
            }

            var sessionCtrl = GetComponent<SessionController>() ?? FindAnyObjectByType<SessionController>();
            foreach ( var pRef in toRemove ) {
                sessionCtrl?.RemovePlayer(pRef);
            }
        }

        private void StartNextRound( int previousWinnerSeat ) {
            if ( !Object.HasStateAuthority || seatQueryService == null ) return;

            Debug.Log("[TienLenGameController] Starting next game round...");

            validator?.Reset();
            CurrentTableCardCount = 0;
            LastPassSeat = -1;
            WinnerSeat = -1;
            actionHandler.IsFirstPlayOfGame = false;
            RPCClearTableCenter();

            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            foreach ( var p in seatedPlayers.Values ) {
                p.Hand?.Clear();
            }

            Deck deck = new Deck();
            deck.CreateDeck();
            DealCard(deck);

            List<TienLenPlayer> ordered = seatedPlayers
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return;

            turnManager.Initialize(ordered);

            int startingIndex = -1;
            if ( seatedPlayers.TryGetValue(previousWinnerSeat, out var winnerPlayer) && winnerPlayer != null && !winnerPlayer.IsInactive ) {
                startingIndex = ordered.IndexOf(winnerPlayer);
            }

            turnManager.SetStartingPlayer(startingIndex >= 0 ? startingIndex : 0);
            MirrorTurnSeat(isDealingStart: true);
        }

        private void InitializeTurns() {
            if ( !Object.HasStateAuthority || seatQueryService == null ) return;

            List<TienLenPlayer> ordered = seatQueryService.GetSeatedPlayers()
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return;

            turnManager.Initialize(ordered);

            int holderIndex = turnHandler.FindStartingSeatOrderIndex(ordered);
            if ( holderIndex >= 0 ) {
                turnManager.SetStartingPlayer(holderIndex);
                actionHandler.IsFirstPlayOfGame = true;
                Debug.Log($"[TienLenGameController] Player at order index {holderIndex} holds 3 of Spades and must lead the match.");
            }
            else {
                turnManager.SetStartingPlayer(0);
                actionHandler.IsFirstPlayOfGame = false;
                Debug.Log("[TienLenGameController] 3 of Spades is not held by any player. Host starts freely.");
            }

            MirrorTurnSeat(isDealingStart: true);
        }

        private void MirrorTurnSeat( bool isDealingStart = false ) {
            int seat = SeatOfPlayer(turnManager.CurrentPlayer);
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
                AutoActForCurrentPlayer();
            }
        }

        private int SeatOfPlayer( TienLenPlayer player ) {
            if ( seatQueryService == null || player == null ) return -1;
            foreach ( var kvp in seatQueryService.GetSeatedPlayers() ) {
                if ( ReferenceEquals(kvp.Value, player) ) return kvp.Key;
            }
            return -1;
        }

        private void OnIsGameStartedChanged() { }

        #endregion

        #region Dealing & Cards

        private void DealCard( Deck deck ) {
            if ( seatQueryService == null ) return;

            deck.Shuffle();
            var seatedPlayers = seatQueryService.GetSeatedPlayers();

            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in seatedPlayers.Values ) {
                    var drawnCard = deck.DrawCard();
                    player.Hand.AddCard(drawnCard);
                }
            }

            BroadcastHands();
            RPCPlayDealAnimation();
        }

        private void BroadcastHands() {
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

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPCPlayDealAnimation( RpcInfo info = default ) {
            EnsureAnimators();
            dealingAnimator.AnimateDealingRoutineAsync(
                seatQueryService,
                localPlayerService,
                tableVisualLayoutManager,
                cardSpawner,
                tienLenSO,
                cardBack,
                uiManager,
                pendingHands,
                (seatOwner, seatIndex) => (seatOwner.IsValid && seatOwner == Runner.LocalPlayer)
                                       || (!seatOwner.IsValid && seatIndex == 0 && Object.HasStateAuthority),
                destroyCancellationToken,
                sprites
            ).Forget();
        }

        #endregion

        #region Play & Pass Handling

        public void HandlePlayRequest( PlayerRef sender, NetworkCard[] cards ) {
            if ( WinnerSeat != -1 ) return;

            TienLenPlayer player = GetPlayer(sender);
            if ( player == null || cards == null ) return;

            List<Card> requestedCards = cards.Select(card => card.ToCard()).ToList();
            if ( actionHandler.TryValidateAndExecutePlay(player, requestedCards, out _, out _) ) {
                HandleAcceptedPlay(sender, player, requestedCards);
            }
        }

        public void HandleAcceptedPlay( PlayerRef sender, TienLenPlayer player, List<Card> playedCards ) {
            if ( player == null ) return;

            RPCPlayAccepted(
                sender,
                playedCards.Select(card => new NetworkCard(card)).ToArray()
            );

            CurrentTableCardCount = playedCards.Count;
            for ( int i = 0; i < playedCards.Count && i < 13; i++ ) {
                CurrentTableCards.Set(i, new NetworkCard(playedCards[i]));
            }

            MirrorTurnSeat();
            LastPassSeat = -1;

            if ( actionHandler.CheckPlayerWin(player) ) {
                TriggerGameOver(SeatOfPlayer(player));
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPlayAccepted( PlayerRef playerRef, NetworkCard[] cards ) {
            List<Card> playedCards = cards.Select(card => card.ToCard()).ToList();

            if ( !Object.HasStateAuthority ) {
                GetPlayer(playerRef)?.TryRemoveCards(playedCards);

                if ( cardCombinationEvaluator != null && cardCombinationEvaluator.TryEvaluate(playedCards, out CardCombination combo) ) {
                    validator?.SetCurrentCombination(combo);
                }
                var actionPanel = FindAnyObjectByType<ActionPanel>();
                actionPanel?.RefreshActionState();
            }

            HandlePlayPresentation(playerRef, playedCards);
        }

        private void HandlePlayPresentation( PlayerRef playerRef, List<Card> playedCards ) {
            EnsureAnimators();
            tableAnimator.ClearTableCenter();

            TienLenPlayer player = GetPlayer(playerRef);
            if ( player == null ) return;

            int seatIndex = SeatOfPlayer(player);
            CardHolder hand = tableVisualLayoutManager != null
                ? tableVisualLayoutManager.GetCardHolderForSeat(seatIndex)
                : null;

            tableAnimator.AnimateCards(hand, playedCards, cardSpawner, tienLenSO, sprites);
        }

        public void HandlePassRequest( PlayerRef sender ) {
            if ( WinnerSeat != -1 ) return;
            TienLenPlayer player = GetPlayer(sender);
            if ( player == null ) return;

            if ( actionHandler.TryExecutePass(player, out bool shouldClearCenter, out _) ) {
                if ( shouldClearCenter ) {
                    CurrentTableCardCount = 0;
                    RPCClearTableCenter();
                }

                MirrorTurnSeat();
                LastPassSeat = SeatOfPlayer(player);
                RPCPassAccepted(sender);
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPassAccepted( PlayerRef playerRef ) {
            TienLenPlayer player = GetPlayer(playerRef);
            if ( player != null ) {
                Debug.Log($"[RPCPassAccepted] Player {player.Id} passed.");
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCClearTableCenter() {
            EnsureAnimators();
            tableAnimator.ClearTableCenter();
            validator?.Reset();
            var actionPanel = FindAnyObjectByType<ActionPanel>();
            actionPanel?.RefreshActionState();
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCNotifyMatchReported() {
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
                    AutoActForCurrentPlayer();
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
