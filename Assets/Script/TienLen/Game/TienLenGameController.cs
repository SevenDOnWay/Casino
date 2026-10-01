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
        [SerializeField] private UiManager uiManager;
        [SerializeField] private TableVisualLayoutManager tableVisualLayoutManager;

        private const int totalSeats = 4;
        private const int minPlayersToStart = 2;

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
        [SerializeField] private float turnDuration = 15f;
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

        public bool CanStartGame() {
            if ( Object == null || !Object.IsValid ) return false;
            if ( IsGameStarted ) return false;
            if ( seatQueryService == null ) return false;

            return RegisteredPlayerCount >= minPlayersToStart;
        }

        [SerializeField] private CardHolder tableCenterPosition;
        [SerializeField] private TienLenSO tienLenSO;

        private Dictionary<(CardSuit, CardRank), Sprite> sprites = new();
        private Sprite cardBack;

        // Hands that arrived before the local seat model knew the player.
        private readonly Dictionary<PlayerRef, List<Card>> pendingHands = new();
        private int lastSeenSeatRevision = -1;

        [Inject]
        void Construct(
            CardSpawner cardSpawner,
            LocalPlayerService localPlayerService,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            ISeatQueryService seatQueryService,
            TienLenRuleValidator validator,
            TableVisualLayoutManager tableVisualLayoutManager ) {
            this.cardSpawner = cardSpawner;
            this.localPlayerService = localPlayerService;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.seatQueryService = seatQueryService;
            this.validator = validator;
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
                WinnerSeat = SeatOfPlayer(ordered[0]);
                Debug.Log($"[TienLenGameController] Single player remaining after migration. Player {ordered[0].Id} wins!");
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
                    WinnerSeat = winnerKvp.Key;
                    Debug.Log($"[TienLenGameController] Player {winnerKvp.Value.Id} wins because other players left!");
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
            if ( turnManager == null || turnManager.CurrentPlayer == null || seatQueryService == null ) return;

            var networkMap = seatQueryService.GetNetworkPlayerMap();
            PlayerRef currentRef = default;
            foreach ( var kvp in networkMap ) {
                if ( seatQueryService.GetLogicPlayer(kvp.Value.PlayerRef) == turnManager.CurrentPlayer ) {
                    currentRef = kvp.Value.PlayerRef;
                    break;
                }
            }

            if ( !currentRef.IsValid ) return;

            Debug.Log($"[TienLenGameController] Turn timer expired for player {turnManager.CurrentPlayer.Id} (Seat {CurrentTurnSeat}). Auto-acting...");

            if ( validator != null && validator.CurrentCombination != null ) {
                HandlePassRequest(currentRef);
            }
            else {
                var hand = turnManager.CurrentPlayer.Hand?.Cards;
                if ( hand != null && hand.Count > 0 ) {
                    Card lowestCard = hand[0];
                    var singleList = new List<Card> { lowestCard };
                    if ( cardCombinationEvaluator.TryEvaluate(singleList, out var combo) && turnManager.TryPlay(turnManager.CurrentPlayer, combo) ) {
                        HandleAcceptedPlay(currentRef, turnManager.CurrentPlayer, singleList);
                    }
                }
                else {
                    HandlePassRequest(currentRef);
                }
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
            if ( seatQueryService == null ) return;
            if ( Object == null || !Object.IsValid ) return;

            int revision = seatQueryService.SeatRevision;
            if ( revision == lastSeenSeatRevision ) return;
            lastSeenSeatRevision = revision;

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

            Deck deck = CreateDeck();
            DealCard(deck);
            InitializeTurns();
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
            MirrorTurnSeat();
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

        private void MirrorTurnSeat() {
            int seat = SeatOfPlayer(turnManager.CurrentPlayer);
            if ( seat != -1 ) {
                CurrentTurnSeat = seat;
                if ( Object.HasStateAuthority ) {
                    TurnTimer = TickTimer.CreateFromSeconds(Runner, turnDuration);
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
                TienLenPlayer logicPlayer = seatQueryService.GetLogicPlayer(kvp.Value.PlayerRef);
                if ( logicPlayer?.Hand == null ) continue;

                NetworkCard[] netCards = logicPlayer.Hand.Cards
                    .Select(card => new NetworkCard(card))
                    .ToArray();

                RPCReceiveHand(kvp.Value.PlayerRef, netCards);
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCReceiveHand( PlayerRef owner, NetworkCard[] cards ) {
            if ( !Object.HasStateAuthority && owner != Runner.LocalPlayer ) return;

            List<Card> hand = cards.Select(card => card.ToCard()).ToList();

            if ( !TryApplyHand(owner, hand) ) {
                pendingHands[owner] = hand;
                Debug.Log($"[Deal] Buffered hand for {owner} until seats are ready.");
            }
        }

        private bool TryApplyHand( PlayerRef owner, List<Card> hand ) {
            TienLenPlayer player = seatQueryService?.GetLogicPlayer(owner);
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
                        var player = orderedSeats[p].Value;
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
                            Debug.LogError($"[Deal] CardHolder is null for seat {seatIndex}. Destroying visual card.");
                            Destroy(cardView.gameObject);
                            continue;
                        }

                        bool isLocal = (seatOwner == Runner.LocalPlayer);

                        if ( isLocal ) {
                            const int handWaitMs = 5000;
                            int waitedMs = 0;
                            while ( (player.Hand?.Cards == null || player.Hand.Cards.Count <= round)
                                && waitedMs < handWaitMs ) {
                                await UniTask.Delay(50, cancellationToken: ct);
                                waitedMs += 50;
                            }

                            if ( player.Hand?.Cards == null || player.Hand.Cards.Count <= round ) {
                                Debug.LogError($"[Deal] Hand missing card at index {round} for local player.");
                                Destroy(cardView.gameObject);
                                continue;
                            }

                            Card cardData = player.Hand.Cards[round];
                            Sprite cardSprite = sprites[(cardData.Suit, cardData.Rank)];

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
                WinnerSeat = SeatOfPlayer(player);
                Debug.Log($"[Game] Player {player.Id} wins!");
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

            float duration = 0.35f;
            float cardSpacing = 0.2f;
            Vector3 centerPos = Vector3.zero;

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
                cardTransform.SetParent(cardTransform.root, worldPositionStays: true);

                float offset = (i - (views.Count - 1) / 2f) * cardSpacing;
                Vector3 targetPos = centerPos + new Vector3(offset, 0f, 0f);
                float randomAngle = UnityEngine.Random.Range(-5f, 5f);

                cardTransform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(cardTransform.DOMove(targetPos, duration).SetEase(Ease.OutQuad));
                seq.Join(cardTransform.DORotate(new Vector3(0, 0, randomAngle), duration));
                seq.Join(cardTransform.DOScale(Vector3.one * 0.9f, duration));

                table.AddCard(view);
            }

            cardHolder.RemoveCards(views, animate: false);
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
