using Assets.Script.Data.Models;
using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : NetworkBehaviour {
        [Header("Dependencies")]
        private CardSpawner cardSpawner;
        private CardCombinationEvaluator cardCombinationEvaluator;
        private LocalPlayerService localPlayerService;
        private TurnManager turnManager;
        private ISeatQueryService seatQueryService;
        private TienLenRuleValidator validator;
        private IPlayerProfileService profileService;
        [SerializeField] private UiManager uiManager;
        [SerializeField] private TableVisualLayoutManager tableVisualLayoutManager;

        private const int totalSeats = 4;
        [SerializeField] private int minPlayersToStart = 2;

        [Networked, OnChangedRender(nameof(OnIsGameStartedChanged))] public NetworkBool IsGameStarted { get; set; }

        /// <summary>Network seat index whose turn it is. -1 = not started. Mirrored by the host from TurnManager.</summary>
        [Networked] public int CurrentTurnSeat { get; set; }

        /// <summary>Network seat index of the winner. -1 = no winner yet.</summary>
        [Networked] public int WinnerSeat { get; set; }

        /// <summary>Network seat index of the last accepted pass. -1 = none/cleared by a newer play.</summary>
        [Networked] public int LastPassSeat { get; set; }

        /// <summary>Networked array of the cards currently played on the table.</summary>
        [Networked, Capacity(13)] public NetworkArray<NetworkCard> CurrentTableCards => default;
        [Networked] public int CurrentTableCardCount { get; set; }

        [Header("Turn Configuration")]
        [SerializeField] private float turnDuration = 10f;
        public float TurnDuration => turnDuration;

        [Networked] public TickTimer TurnTimer { get; set; }

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

        [SerializeField] private CardHolder tableCenterPosition;
        [SerializeField] private TienLenSO tienLenSO;

        private Dictionary<(CardSuit, CardRank), Sprite> sprites = new();
        private Sprite cardBack;

        // Hands that arrived before the local seat model knew the player.
        private readonly Dictionary<PlayerRef, List<Card>> pendingHands = new();

        [Inject]
        void Construct(
            CardSpawner cardSpawner,
            LocalPlayerService localPlayerService,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            ISeatQueryService seatQueryService,
            TienLenRuleValidator validator,
            TableVisualLayoutManager tableVisualLayoutManager,
            IPlayerProfileService profileService = null ) {
            this.cardSpawner = cardSpawner;
            this.localPlayerService = localPlayerService;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.seatQueryService = seatQueryService;
            this.validator = validator;
            this.profileService = profileService;
            if ( this.tableVisualLayoutManager == null ) this.tableVisualLayoutManager = tableVisualLayoutManager;
        }

        public override void Spawned() {
            base.Spawned();

            if ( Object.HasStateAuthority ) {
                if ( Runner.IsResume ) {
                    ResumeFromMigration();
                }
                else {
                    CurrentTurnSeat = -1;
                    WinnerSeat = -1;
                    LastPassSeat = -1;
                    TurnTimer = default;
                    CurrentTableCardCount = 0;
                }
            }
        }

        public void ResumeFromMigration() {
            if ( !Object.HasStateAuthority ) return;
            Debug.Log("[TienLenGameController] Resuming game state after Host Migration...");

            if ( !IsGameStarted ) {
                Debug.Log("[TienLenGameController] Game has not started yet. Lobby state preserved.");
                return;
            }

            if ( seatQueryService == null ) return;

            List<TienLenPlayer> ordered = seatQueryService.GetSeatedPlayers()
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return;

            if ( ordered.Count == 1 ) {
                TriggerGameOver(SeatOfPlayer(ordered[0]));
                return;
            }

            turnManager.Initialize(ordered);

            // Reconstruct validator table combination from CurrentTableCards
            if ( CurrentTableCardCount > 0 ) {
                List<Card> tableCards = new();
                for ( int i = 0; i < CurrentTableCardCount; i++ ) {
                    tableCards.Add(CurrentTableCards[i].ToCard());
                }

                if ( cardCombinationEvaluator != null && cardCombinationEvaluator.TryEvaluate(tableCards, out CardCombination combo) ) {
                    validator?.SetCurrentCombination(combo);
                    Debug.Log($"[TienLenGameController] Restored table combination: {combo.Type} ({combo.Cards.Count} cards).");
                }
            }
            else {
                validator?.Reset();
            }

            // Restore active turn player
            if ( CurrentTurnSeat != -1 ) {
                var seated = seatQueryService.GetSeatedPlayers();
                if ( seated.TryGetValue(CurrentTurnSeat, out var turnPlayer) && turnPlayer != null ) {
                    turnManager.SetCurrentPlayer(turnPlayer);
                }
                else {
                    // Player left -> advance turn
                    turnManager.ForceAdvanceTurn();
                    MirrorTurnSeat();
                }
            }

            // Give a grace buffer on the turn timer
            TurnTimer = TickTimer.CreateFromSeconds(Runner, turnDuration + 5f);

            // Ask all connected clients to report their current hands to the new Host
            RPCRequestHandReport();
        }

        public void HandlePlayerLeftMidGame( PlayerRef playerRef ) {
            if ( !Object.HasStateAuthority ) return;
            if ( !IsGameStarted || WinnerSeat != -1 ) return;

            Debug.Log($"[TienLenGameController] Player {playerRef.PlayerId} left mid-game.");

            var seated = seatQueryService?.GetSeatedPlayers();
            if ( seated == null ) return;

            int remainingCount = seated.Count;
            if ( remainingCount <= 1 ) {
                if ( remainingCount == 1 ) {
                    var winnerKvp = seated.First();
                    TriggerGameOver(winnerKvp.Key);
                }
                return;
            }

            if ( turnManager?.CurrentPlayer != null && seatQueryService != null ) {
                var leavingLogic = seatQueryService.GetLogicPlayer(playerRef);
                if ( ReferenceEquals(turnManager.CurrentPlayer, leavingLogic) ) {
                    Debug.Log("[TienLenGameController] Active turn player left. Advancing turn...");
                    turnManager.ForceAdvanceTurn();
                    MirrorTurnSeat();
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
                player.Hand.Clear();
                player.Hand.AddCard(cards.Select(c => c.ToCard()));
                Debug.Log($"[TienLenGameController] Restored hand ({cards.Length} cards) for player {player.Id} ({sender.PlayerId}) on new Host.");
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
            if ( !Object.HasStateAuthority ) return;
            if ( !IsGameStarted || WinnerSeat != -1 ) return;
            if ( turnManager == null || turnManager.CurrentPlayer == null || seatQueryService == null ) return;

            var currentPlayer = turnManager.CurrentPlayer;
            var networkMap = seatQueryService.GetNetworkPlayerMap();
            PlayerRef currentRef = default;
            foreach ( var kvp in networkMap ) {
                if ( seatQueryService.GetLogicPlayer(kvp.Value.PlayerRef) == currentPlayer ) {
                    currentRef = kvp.Value.PlayerRef;
                    break;
                }
            }

            Debug.Log($"[TienLenGameController] Turn timer expired for player {currentPlayer.Id} (Seat {CurrentTurnSeat}). Auto-acting...");

            // If there is an active table combination, timeout counts as a PASS
            if ( validator != null && validator.CurrentCombination != null ) {
                if ( currentRef.IsValid ) {
                    HandlePassRequest(currentRef);
                }
                else {
                    if ( turnManager.TryPass(currentPlayer) ) {
                        MirrorTurnSeat();
                    }
                    else {
                        turnManager.ForceAdvanceTurn();
                        MirrorTurnSeat();
                    }
                }
            }
            else {
                // Leading a new round: cannot pass, so auto-play lowest valid single card
                var hand = currentPlayer.Hand?.Cards;
                bool played = false;
                if ( hand != null && hand.Count > 0 ) {
                    var sortedHand = hand.OrderBy(c => c.Rank).ThenBy(c => (int)c.Suit).ToList();
                    foreach ( var card in sortedHand ) {
                        var singleList = new List<Card> { card };
                        if ( cardCombinationEvaluator != null && cardCombinationEvaluator.TryEvaluate(singleList, out var combo) && (validator == null || validator.CanPlay(combo)) ) {
                            if ( turnManager.TryPlay(currentPlayer, combo) ) {
                                if ( currentRef.IsValid ) {
                                    HandleAcceptedPlay(currentRef, currentPlayer, singleList);
                                }
                                else {
                                    currentPlayer.TryRemoveCards(singleList);
                                    RPCPlayAccepted(PlayerRef.None, singleList.Select(c => new NetworkCard(c)).ToArray());
                                    CurrentTableCardCount = 1;
                                    MirrorTurnSeat();
                                }
                                played = true;
                                break;
                            }
                        }
                    }
                }

                if ( !played ) {
                    Debug.LogWarning($"[TienLenGameController] Auto-play could not find valid play for player {currentPlayer.Id}. Force advancing turn.");
                    turnManager.ForceAdvanceTurn();
                    MirrorTurnSeat();
                }
            }

            // Always guarantee TurnTimer is reset for the next turn
            if ( Object.HasStateAuthority && (!TurnTimer.IsRunning || TurnTimer.Expired(Runner)) ) {
                TurnTimer = TickTimer.CreateFromSeconds(Runner, turnDuration);
            }
        }

        private void Start() {
            if ( tienLenSO != null ) {
                sprites = tienLenSO.GetLookUpTable();
                cardBack = tienLenSO.GetCardBackSprite();
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

        private Deck CreateDeck() {
            Deck deck = new Deck();
            deck.CreateDeck();
            return deck;
        }

        public void StartGame() {
            if ( !Object.HasStateAuthority ) return;
            if ( !CanStartGame() ) return;
            if ( IsGameStarted ) return;

            IsGameStarted = true;
            WinnerSeat = -1;
            LastPassSeat = -1;

            Deck deck = CreateDeck();
            DealCard(deck);
            InitializeTurns();
        }

        private void TriggerGameOver( int winnerSeat ) {
            WinnerSeat = winnerSeat;
            Debug.Log($"[Game] Player at seat {winnerSeat} wins!");

            ReportMatchOutcome(winnerSeat);

            if ( Object.HasStateAuthority ) {
                WaitAndStartNextGame(winnerSeat).Forget();
            }
        }

        private void ReportMatchOutcome( int winnerSeat ) {
            if ( profileService == null ) return;

            string myId = localPlayerService?.GetID();
            if ( string.IsNullOrEmpty(myId) ) return;

            int mySeat = localPlayerService?.Player != null ? SeatOfPlayer(localPlayerService.Player) : -1;
            bool isWinner = (mySeat == winnerSeat);

            var report = new MatchResultReportRequest {
                matchId = Guid.NewGuid().ToString("N"),
                results = new List<MatchPlayerSummary> {
                    new MatchPlayerSummary {
                        userId = myId,
                        displayName = localPlayerService.GetName(),
                        avatarId = localPlayerService.GetAvatarId(),
                        rank = isWinner ? 1 : 2,
                        moneyEarned = isWinner ? 1000 : -300,
                        expEarned = isWinner ? 50 : 15
                    }
                }
            };

            profileService.RecordMatchResultAsync(report).Forget();
        }

        private async UniTaskVoid WaitAndStartNextGame( int previousWinnerSeat ) {
            float celebrationDuration = 3.5f;
            var ct = this.GetCancellationTokenOnDestroy();

            try {
                await UniTask.Delay(TimeSpan.FromSeconds(celebrationDuration), cancellationToken: ct);
            }
            catch ( OperationCanceledException ) {
                return;
            }

            if ( !Object.HasStateAuthority ) return;

            if ( !HasEnoughPlayersToPlay ) {
                Debug.LogWarning("[TienLenGameController] Cannot continue game: not enough players.");
                IsGameStarted = false;
                WinnerSeat = -1;
                CurrentTurnSeat = -1;
                CurrentTableCardCount = 0;
                return;
            }

            StartNextRound(previousWinnerSeat);
        }

        private void StartNextRound( int previousWinnerSeat ) {
            if ( !Object.HasStateAuthority || seatQueryService == null ) return;

            Debug.Log("[TienLenGameController] Starting next game round...");

            // 1. Reset round & table state
            validator?.Reset();
            CurrentTableCardCount = 0;
            LastPassSeat = -1;
            WinnerSeat = -1;

            // 2. Clear all logic hands
            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            foreach ( var p in seatedPlayers.Values ) {
                p.Hand?.Clear();
            }

            // 3. Create fresh deck and deal to all current players
            Deck deck = CreateDeck();
            DealCard(deck);

            // 4. Initialize turns - in Tien Len, previous round winner leads the next game!
            List<TienLenPlayer> ordered = seatedPlayers
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return;

            turnManager.Initialize(ordered);

            int startingIndex = -1;
            if ( seatedPlayers.TryGetValue(previousWinnerSeat, out var winnerPlayer) && winnerPlayer != null ) {
                startingIndex = ordered.IndexOf(winnerPlayer);
            }

            if ( startingIndex >= 0 ) {
                turnManager.SetStartingPlayer(startingIndex);
            }
            else {
                turnManager.SetStartingPlayer(FindStartingSeatOrderIndex(ordered));
            }

            MirrorTurnSeat(isDealingStart: true);
        }

        private void OnIsGameStartedChanged() {
            // Handled via change detector in UiManager
        }

        private void InitializeTurns() {
            if ( !Object.HasStateAuthority || seatQueryService == null ) return;

            List<TienLenPlayer> ordered = seatQueryService.GetSeatedPlayers()
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return;

            turnManager.Initialize(ordered);
            turnManager.SetStartingPlayer(FindStartingSeatOrderIndex(ordered));
            MirrorTurnSeat(isDealingStart: true);
        }

        private int FindStartingSeatOrderIndex( List<TienLenPlayer> ordered ) {
            for ( int i = 0; i < ordered.Count; i++ ) {
                var cards = ordered[i]?.Hand?.Cards;
                if ( cards == null ) continue;
                foreach ( var card in cards ) {
                    if ( card.Rank == CardRank.Three && card.Suit == CardSuit.Spades ) {
                        return i;
                    }
                }
            }
            return 0;
        }

        private int SeatOfPlayer( TienLenPlayer player ) {
            if ( seatQueryService == null || player == null ) return -1;
            foreach ( var kvp in seatQueryService.GetSeatedPlayers() ) {
                if ( ReferenceEquals(kvp.Value, player) ) return kvp.Key;
            }
            return -1;
        }

        private void MirrorTurnSeat( bool isDealingStart = false ) {
            int seat = SeatOfPlayer(turnManager.CurrentPlayer);
            if ( seat != -1 ) {
                CurrentTurnSeat = seat;
                if ( Object.HasStateAuthority ) {
                    float duration = isDealingStart ? (turnDuration + 3.5f) : turnDuration;
                    TurnTimer = TickTimer.CreateFromSeconds(Runner, duration);
                }
            }
        }

        #region Deal Cards

        private void DealCard( Deck deck ) {
            if ( seatQueryService == null ) return;

            var tempDeck = deck;
            tempDeck.Shuffle();

            var seatedPlayers = seatQueryService.GetSeatedPlayers();

            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in seatedPlayers.Values ) {
                    var drawnCard = tempDeck.DrawCard();
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

            // Cache hand immediately so dealing routine can access it even before seat resolution completes
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
            AnimateDealingRoutineAsync(destroyCancellationToken).Forget();
        }

        private async UniTask AnimateDealingRoutineAsync( CancellationToken ct ) {
            const int delayMs = 60;
            const int cardsPerPlayer = 13;
            const int totalDeckSize = 52;

            if ( seatQueryService == null ) return;

            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            var networkPlayerMap = seatQueryService.GetNetworkPlayerMap();

            Vector3 centerDeckPos = tableCenterPosition != null
                ? tableCenterPosition.transform.position
                : Vector3.zero;

            Queue<CardView> deckStack = new Queue<CardView>(totalDeckSize);

            var orderedSeats = seatedPlayers.OrderBy(kvp => kvp.Key).ToList();

            // Clear any leftover visual cards from table and all seats before dealing new cards
            if ( tableCenterPosition != null ) {
                tableCenterPosition.Clear();
            }
            if ( tableVisualLayoutManager != null ) {
                tableVisualLayoutManager.RefreshLayout();
                var slots = tableVisualLayoutManager.GetAllVisualSlots();
                if ( slots != null ) {
                    foreach ( var slot in slots ) {
                        slot?.cardHolder?.Clear();
                    }
                }
            }

            try {
                // 1. Spawn deck stack
                for ( int i = 0; i < totalDeckSize; i++ ) {
                    ct.ThrowIfCancellationRequested();

                    CardView backView = cardSpawner.SpawnCardBack(cardBack);
                    backView.transform.position = centerDeckPos;
                    backView.transform.SetParent(transform, worldPositionStays: true);
                    deckStack.Enqueue(backView);
                }

                // 2. Deal cards round-by-round
                for ( int round = 0; round < cardsPerPlayer; round++ ) {
                    for ( int p = 0; p < orderedSeats.Count; p++ ) {
                        int seatIndex = orderedSeats[p].Key;
                        ct.ThrowIfCancellationRequested();

                        if ( deckStack.Count == 0 ) break;

                        CardView cardView = deckStack.Dequeue();

                        PlayerRef seatOwner = default;
                        foreach ( var kvp in networkPlayerMap ) {
                            if ( kvp.Key == seatIndex && kvp.Value != null ) {
                                seatOwner = kvp.Value.PlayerRef;
                                break;
                            }
                        }

                        CardHolder targetCardHolder = tableVisualLayoutManager != null
                            ? tableVisualLayoutManager.GetCardHolderForSeat(seatIndex)
                            : null;

                        if ( targetCardHolder == null ) {
                            tableVisualLayoutManager?.RefreshLayout();
                            targetCardHolder = tableVisualLayoutManager?.GetCardHolderForSeat(seatIndex);
                        }

                        if ( targetCardHolder == null ) {
                            Debug.LogError($"[Deal] CardHolder is null for seat {seatIndex}. Destroying visual card.");
                            Destroy(cardView.gameObject);
                            continue;
                        }

                        bool isLocal = (seatOwner.IsValid && seatOwner == Runner.LocalPlayer)
                                    || (!seatOwner.IsValid && seatIndex == 0 && Object.HasStateAuthority);

                        if ( isLocal ) {
                            const int handWaitMs = 5000;
                            int waitedMs = 0;
                            List<Card> localCards = null;

                            while ( waitedMs < handWaitMs ) {
                                // Check pendingHands cache first
                                if ( pendingHands.TryGetValue(seatOwner, out var buffered) && buffered != null && buffered.Count > round ) {
                                    TryApplyHand(seatOwner, buffered);
                                    localCards = buffered;
                                    break;
                                }

                                // Check logic player
                                var livePlayer = seatQueryService?.GetLogicPlayer(seatOwner) ?? localPlayerService?.GetLocalLogicPlayer();
                                if ( livePlayer?.Hand?.Cards != null && livePlayer.Hand.Cards.Count > round ) {
                                    localCards = livePlayer.Hand.Cards.ToList();
                                    break;
                                }

                                await UniTask.Delay(50, cancellationToken: ct);
                                waitedMs += 50;
                            }

                            if ( localCards == null || localCards.Count <= round ) {
                                Debug.LogError($"[Deal] Hand missing card at index {round} for local player.");
                                Destroy(cardView.gameObject);
                                continue;
                            }

                            Card cardData = localCards[round];
                            Sprite cardSprite = null;
                            if ( sprites != null && sprites.TryGetValue((cardData.Suit, cardData.Rank), out Sprite sp) ) {
                                cardSprite = sp;
                            }
                            else if ( tienLenSO != null ) {
                                cardSprite = tienLenSO.GetCardSprite(cardData.Suit, cardData.Rank);
                            }

                            CardView faceCardView = cardSpawner.SpawnCard(cardData, cardSprite);
                            faceCardView.transform.position = cardView.transform.position;
                            faceCardView.transform.rotation = cardView.transform.rotation;
                            faceCardView.transform.SetParent(targetCardHolder.transform, worldPositionStays: true);

                            Destroy(cardView.gameObject);
                            targetCardHolder.AddCard(faceCardView, animate: true);
                        }
                        else {
                            cardView.transform.SetParent(targetCardHolder.transform, worldPositionStays: true);
                            targetCardHolder.AddCard(cardView, animate: true);
                        }
                    }

                    await UniTask.Delay(delayMs, cancellationToken: ct);
                }

                // 3. Sort and fan local player hand
                CardHolder localHand = tableVisualLayoutManager != null
                    ? tableVisualLayoutManager.GetLocalCardHolder()
                    : null;

                if ( localHand != null ) {
                    localHand.SortCards();
                    localHand.ArrangeCards(animate: true);
                }

                if ( uiManager != null ) {
                    uiManager.Refresh(LobbyChangeReason.TurnChanged);
                }
            }
            finally {
                while ( deckStack.Count > 0 ) {
                    CardView leftover = deckStack.Dequeue();
                    if ( leftover != null ) {
                        Destroy(leftover.gameObject);
                    }
                }
            }
        }

        private TienLenPlayer GetPlayer( PlayerRef sender ) {
            if ( !sender.IsValid || seatQueryService == null ) return null;
            return seatQueryService.GetLogicPlayer(sender);
        }

        #endregion

        #region Pass Handling

        public void HandlePassRequest( PlayerRef sender ) {
            if ( WinnerSeat != -1 ) return;
            TienLenPlayer player = GetPlayer(sender);
            if ( player == null ) {
                Debug.LogWarning($"[HandlePassRequest] Cannot find player for {sender.PlayerId}.");
                return;
            }

            if ( !turnManager.TryPass(player) ) {
                Debug.LogWarning($"[HandlePassRequest] Player {player.Id} cannot pass.");
                return;
            }

            if ( validator != null && validator.CurrentCombination == null ) {
                CurrentTableCardCount = 0;
            }

            MirrorTurnSeat();
            LastPassSeat = SeatOfPlayer(player);
            RPCPassAccepted(sender);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPassAccepted( PlayerRef playerRef ) {
            TienLenPlayer player = GetPlayer(playerRef);
            if ( player != null ) {
                Debug.Log($"[RPCPassAccepted] Player {player.Id} passed.");
            }
        }

        #endregion

        #region Play Handling

        public void HandleAcceptedPlay( PlayerRef sender, TienLenPlayer player, List<Card> playedCards ) {
            if ( player == null ) return;

            if ( !player.TryRemoveCards(playedCards) ) {
                Debug.LogError($"Failed to remove played cards from player {player.Id}.");
                return;
            }

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

            if ( player.Hand.Count == 0 ) {
                TriggerGameOver(SeatOfPlayer(player));
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPlayAccepted( PlayerRef playerRef, NetworkCard[] cards ) {
            List<Card> playedCards = cards.Select(card => card.ToCard()).ToList();

            if ( !Object.HasStateAuthority ) {
                GetPlayer(playerRef)?.TryRemoveCards(playedCards);
            }

            HandlePlayPresentation(playerRef, playedCards);
        }

        private void HandlePlayPresentation( PlayerRef playerRef, List<Card> playedCards ) {
            if ( tableCenterPosition != null ) {
                tableCenterPosition.Clear();
            }

            TienLenPlayer player = GetPlayer(playerRef);
            if ( player == null ) return;

            PlayCardsAnimation(player, playedCards);
        }

        private void PlayCardsAnimation( TienLenPlayer player, List<Card> playedCards ) {
            int seatIndex = SeatOfPlayer(player);
            CardHolder hand = tableVisualLayoutManager != null
                ? tableVisualLayoutManager.GetCardHolderForSeat(seatIndex)
                : null;
            CardHolder table = tableCenterPosition;

            StartCoroutine(AnimatePlayedCards(hand, table, playedCards));
        }

        private IEnumerator AnimatePlayedCards( CardHolder cardHolder, CardHolder table, List<Card> playedCards ) {
            if ( cardHolder == null || table == null || playedCards == null || playedCards.Count == 0 ) {
                yield break;
            }

            List<CardView> views = cardHolder.FindCards(playedCards);
            if ( views == null || views.Count == 0 ) {
                yield break;
            }

            // Remove cards from player's hand immediately so remaining cards animate into place
            cardHolder.RemoveCards(views, animate: true);

            float duration = 0.35f;

            // Space cards evenly so suits and ranks are clearly legible
            float baseSpacing = 0.6f;
            float maxTotalWidth = 6f;
            float cardSpacing = views.Count > 1
                ? Mathf.Min(baseSpacing, maxTotalWidth / (views.Count - 1))
                : baseSpacing;

            float totalWidth = (views.Count - 1) * cardSpacing;
            float startX = -totalWidth / 2f;

            // Small random offset and tilt for the hand so consecutive played hands have natural variety
            Vector3 handRandomOffset = new Vector3(
                UnityEngine.Random.Range(-0.25f, 0.25f),
                UnityEngine.Random.Range(-0.15f, 0.15f),
                0f
            );
            float handBaseAngle = UnityEngine.Random.Range(-3f, 3f);

            for ( int i = 0; i < views.Count; i++ ) {
                CardView view = views[i];
                if ( view == null ) continue;

                if ( i < playedCards.Count && playedCards[i] != null ) {
                    Card played = playedCards[i];
                    view.Card = played;
                    if ( sprites != null && sprites.TryGetValue((played.Suit, played.Rank), out Sprite faceSprite)
                        && faceSprite != null ) {
                        view.ChangeSprite(faceSprite);
                    }
                }

                Transform cardTransform = view.transform;
                cardTransform.SetParent(table.transform, worldPositionStays: true);

                float cardX = startX + i * cardSpacing;
                float cardYJitter = UnityEngine.Random.Range(-0.03f, 0.03f);
                float cardZOffset = -i * 0.01f; // Layering depth to prevent z-fighting

                Vector3 targetLocalPos = handRandomOffset + new Vector3(cardX, cardYJitter, cardZOffset);
                float randomAngle = handBaseAngle + UnityEngine.Random.Range(-2f, 2f);

                cardTransform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(cardTransform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad));
                seq.Join(cardTransform.DOLocalRotate(new Vector3(0, 0, randomAngle), duration));
                seq.Join(cardTransform.DOScale(Vector3.one * 0.9f, duration));

                table.AddCard(view, autoArrange: false);
            }

            yield return null;
        }

        public void HandlePlayRequest( PlayerRef sender, NetworkCard[] cards ) {
            if ( WinnerSeat != -1 ) return;

            TienLenPlayer player = GetPlayer(sender);
            if ( player == null ) return;

            List<Card> requestedCards = cards.Select(card => card.ToCard()).ToList();

            if ( !player.HasCards(requestedCards) ) {
                Debug.LogWarning($"Player {player.Id} tried to play cards they don't own.");
                return;
            }

            if ( !cardCombinationEvaluator.TryEvaluate(requestedCards, out CardCombination combination) ) {
                Debug.LogWarning($"Player {player.Id} submitted an invalid combination.");
                return;
            }

            if ( !turnManager.TryPlay(player, combination) ) {
                Debug.LogWarning($"Player {player.Id} cannot play this combination.");
                return;
            }

            HandleAcceptedPlay(sender, player, requestedCards);
        }

        #endregion
    }
}
