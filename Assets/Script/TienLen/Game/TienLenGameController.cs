using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using Fusion.Sockets;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : NetworkBehaviour {

        //TODO: reduce this DI
        [Header("Dependencies")]
        CardSpawner cardSpawner;
        CardCombinationEvaluator cardCombinationEvaluator;
        LocalPlayerService localPlayerService;
        TienLenGame game;
        TurnManager turnManager;
        IPlayerRegisterService playerRegisterService;
        TienLenRuleValidator validator;
        [SerializeField] UiManager uiManager;

        private const int totalSeats = 4;
        private const int minPlayersToStart = 2;

        [Networked, OnChangedRender(nameof(OnIsGameStartedChanged))] public NetworkBool IsGameStarted { get; set; }

        /// <summary>Network seat index whose turn it is. -1 = not started. Mirrored by the host from TurnManager.</summary>
        [Networked] public int CurrentTurnSeat { get; set; }

        /// <summary>Network seat index of the winner. -1 = no winner yet.</summary>
        [Networked] public int WinnerSeat { get; set; }

        /// <summary>Network seat index of the last accepted pass. -1 = none/cleared by a newer play.</summary>
        [Networked] public int LastPassSeat { get; set; }

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

        /// <summary>
        /// Returns true when enough registered network players (each carries
        /// its PlayerRef) have joined and the game hasn't started yet.
        /// Uses the replicated seat dictionary, so host + clients agree.
        /// Runner.ActivePlayers is NOT used — it is unreliable before
        /// spawn / during host migration.
        /// </summary>
        public int RegisteredPlayerCount =>
            playerRegisterService != null ? playerRegisterService.GetNetworkPlayer().Count : 0;

        public bool CanStartGame() {
            if ( Object == null || !Object.IsValid ) return false;
            if ( IsGameStarted ) return false;
            if ( playerRegisterService == null ) return false;

            return RegisteredPlayerCount >= minPlayersToStart;
        }


        [SerializeField] private CardHolder tableCenterPosition; //TODO: change the name for better understanding
        [SerializeField] TienLenSO tienLenSO;


        //Dictionary<PlayerRef, TienLenNetWorkPlayer> NetworkPlayerMap;
        //Dictionary<int, TienLenPlayer> playerMap;
        IReadOnlyDictionary<int, TienLenNetWorkPlayer> occupiedSeats;


        private bool lastRenderedGameStarted;
        Dictionary<(CardSuit, CardRank), Sprite> sprites = new Dictionary<(CardSuit, CardRank), Sprite>();
        Sprite cardBack;

        //public event Action OnRoundStarted;

        [Inject]
        void Construct( CardSpawner cardSpawner,
            LocalPlayerService localPlayerService,
            TienLenGame game,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            IPlayerRegisterService playerRegisterService,
            TienLenRuleValidator validator ) {
            this.cardSpawner = cardSpawner;
            this.localPlayerService = localPlayerService;
            this.game = game;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.playerRegisterService = playerRegisterService;
            this.validator = validator;
        }

        public override void Spawned() {
            base.Spawned();

            // Networked ints default to 0; -1 is our real "unset" sentinel
            // (seat 0 is a valid seat). Host publishes, all peers replicate.
            if ( Object.HasStateAuthority ) {
                CurrentTurnSeat = -1;
                WinnerSeat = -1;
                LastPassSeat = -1;
                TurnTimer = default;
            }

            if ( uiManager != null ) {
                //uiManager.Init();
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
            if ( turnManager == null || turnManager.CurrentPlayer == null ) return;

            var networkMap = playerRegisterService.GetNetworkPlayerMap();
            PlayerRef currentRef = default;
            foreach ( var kvp in networkMap ) {
                if ( playerRegisterService.GetLogicPlayer(kvp.Value.PlayerRef) == turnManager.CurrentPlayer ) {
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
            sprites = tienLenSO.GetLookUpTable();
            cardBack = tienLenSO.GetCardBackSprite();
        }

        private Deck CreateDeck() {
            Debug.Log($"[CreateDeck] Retrieved sprites lookup table with {sprites.Count} items.");

            Deck deck = new Deck();
            deck.CreateDeck();

            return deck;
        }

        public void StartGame() {
            if ( !Object.HasStateAuthority ) return;
            if ( !CanStartGame() ) return;
            if ( IsGameStarted ) return;

            IsGameStarted = true;

            occupiedSeats = playerRegisterService.GetNetworkPlayerMap();

            Deck deck = CreateDeck();

            DealCard(deck);

            InitializeTurns();

            Initialize();
            //OnRoundStarted?.Invoke();
        }

        private void OnIsGameStartedChanged() {
            // DISABLED: UI strategy pipeline commented out (freeze investigation).
            //if ( !IsGameStarted ) return;

            //uiManager?.RefreshLobby(LobbyChangeReason.GameStarted);


        }

        private void Initialize() {
            //game.OnTurnChanged += HandleTurnChanged;
            //game.OnCardsPlayed += HandleCardsPlayed;
            //game.OnPlayerWon += HandlePlayerWon;




        }

        /// <summary>
        /// Host only. Seeds turn order from seats ascending and gives the
        /// lead to the holder of the 3 of Spades (lowest seat on fallback).
        /// Open lead: any valid combination may open (must-lead-3♠ is a
        /// future rule tightening).
        /// </summary>
        private void InitializeTurns() {
            if ( !Object.HasStateAuthority ) return;

            List<TienLenPlayer> ordered = playerRegisterService.GetSeatedPlayers()
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
            foreach ( var kvp in playerRegisterService.GetSeatedPlayers() ) {
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
            var tempDeck = deck;
            tempDeck.Shuffle();

            var seatedPlayers = playerRegisterService.GetSeatedPlayers();

            // Deal 13 cards to each player's data hand
            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in seatedPlayers.Values ) {
                    var drawnCard = tempDeck.DrawCard();
                    player.Hand.AddCard(drawnCard);
                }
            }

            // Hands are plain local objects: only the host has them.
            // Push each hand out before triggering the deal animation.
            BroadcastHands();

            RPCPlayDealAnimation();
        }

        /// <summary>
        /// Host only. Sends every seated player's hand to all peers.
        /// Reliable RPCs from one sender stay ordered, so hands always land
        /// before <see cref="RPCPlayDealAnimation"/>.
        /// </summary>
        private void BroadcastHands() {
            if ( !Object.HasStateAuthority ) return;

            var networkMap = playerRegisterService.GetNetworkPlayerMap();
            foreach ( var kvp in networkMap ) {
                TienLenPlayer logicPlayer = playerRegisterService.GetLogicPlayer(kvp.Value.PlayerRef);
                if ( logicPlayer?.Hand == null ) continue;

                NetworkCard[] netCards = logicPlayer.Hand.Cards
                    .Select(card => new NetworkCard(card))
                    .ToArray();

                RPCReceiveHand(kvp.Value.PlayerRef, netCards);
            }
        }

        // Hands that arrived before the local seat model knew the player.
        // Flushed once seats catch up (see Update).
        private readonly Dictionary<PlayerRef, List<Card>> pendingHands = new();
        private int lastSeenSeatRevision = -1;

        private void Update() {
            if ( pendingHands.Count == 0 ) return;
            if ( playerRegisterService == null ) return;
            if ( Object == null || !Object.IsValid ) return;

            int revision = playerRegisterService.SeatRevision;
            if ( revision == lastSeenSeatRevision ) return;
            lastSeenSeatRevision = revision;

            var owners = new List<PlayerRef>(pendingHands.Keys);
            foreach ( var owner in owners ) {
                if ( TryApplyHand(owner, pendingHands[owner]) ) {
                    pendingHands.Remove(owner);
                }
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCReceiveHand( PlayerRef owner, NetworkCard[] cards ) {
            // Hidden info: clients only ever learn their own hand.
            if ( !Object.HasStateAuthority && owner != Runner.LocalPlayer ) return;

            List<Card> hand = cards.Select(card => card.ToCard()).ToList();

            if ( !TryApplyHand(owner, hand) ) {
                // Seat model not ready yet; retry when its revision advances.
                pendingHands[owner] = hand;
                Debug.Log($"[Deal] Buffered hand for {owner} until seats are ready.");
            }
        }

        private bool TryApplyHand( PlayerRef owner, List<Card> hand ) {
            TienLenPlayer player = playerRegisterService?.GetLogicPlayer(owner);
            if ( player?.Hand == null ) return false;

            player.Hand.Clear();
            player.Hand.AddCard(hand);
            return true;
        }


        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPCPlayDealAnimation( RpcInfo info = default ) {
            AnimateDealingRoutineAsync(destroyCancellationToken).Forget();

            Debug.Log($"[RPCPlayDealAnimation] Dealing animation started on all clients. Runner: {info.Source}");
        }

        private async UniTask AnimateDealingRoutineAsync( CancellationToken ct ) {
            const int delayMs = 60;
            const int cardsPerPlayer = 13;
            const int totalDeckSize = 52;

            TienLenPlayer localPlayer = localPlayerService.Player;
            var seatedPlayers = playerRegisterService.GetSeatedPlayers();
            var networkPlayerMap = playerRegisterService.GetNetworkPlayerMap();

            Vector3 centerDeckPos = tableCenterPosition != null
                                ? tableCenterPosition.transform.position
                                : Vector3.zero;

            Queue<CardView> deckStack = new Queue<CardView>(totalDeckSize);

            // Order the seated players by network seat index for consistent dealing order.
            var orderedPlayers = seatedPlayers
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

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
                    for ( int playerIdx = 0; playerIdx < orderedPlayers.Count; playerIdx++ ) {
                        var player = orderedPlayers[playerIdx];
                        ct.ThrowIfCancellationRequested();

                        if ( deckStack.Count == 0 ) {
                            Debug.LogWarning("[AnimateDealingRoutineAsync] Deck stack exhausted early!");
                            break;
                        }

                        CardView cardView = deckStack.Dequeue();

                        // Resolve the owner of this player via the network player map.
                        // We find the network player that corresponds to this logic player.
                        PlayerRef seatOwner = default;
                        int seatIndex = -1;
                        foreach ( var kvp in networkPlayerMap ) {
                            var netPlayer = kvp.Value;
                            if ( netPlayer != null ) {
                                // The logic player was created from this network player
                                // (or was reused from a previous bind of the same network player).
                                // Compare by PlayerRef since that's the stable identity.
                                if ( playerRegisterService.GetLogicPlayer(netPlayer.PlayerRef) == player ) {
                                    seatOwner = netPlayer.PlayerRef;
                                    seatIndex = kvp.Key;
                                    break;
                                }
                            }
                        }

                        //if ( player == null || player.CardHolder == null ) {
                        //    Debug.LogWarning($"[AnimateDealingRoutineAsync] Player or card holder is null for player {player?.Id ?? -1}");
                        //    Destroy(cardView.gameObject);
                        //    continue;
                        //}

                        if ( player == null ) {
                            Debug.LogError(
                                $"[Deal] Player is NULL. " +
                                $"Seat={seatIndex}"
                            );

                            Destroy(cardView.gameObject);
                            continue;
                        }

                        if ( player.CardHolder == null ) {
                            Debug.LogError(
                                $"[Deal] CardHolder is NULL. " +
                                $"PlayerId={player.Id}, " +
                                $"Seat={seatIndex}"
                            );

                            Destroy(cardView.gameObject);
                            continue;
                        }

                        bool isLocal = (seatOwner == Runner.LocalPlayer);

                        Debug.Log($"[AnimateDealingRoutineAsync] Dealing card to player {player.Id} (local={isLocal}) at round {round}, " +
                            $"runner: {Runner.LocalPlayer}. player {seatOwner}");

                        if ( isLocal ) {
                            // The hand RPC is ordered before the animation RPC,
                            // but the seat model may still be catching up:
                            // wait briefly instead of failing immediately.
                            const int handWaitMs = 5000;
                            int waitedMs = 0;
                            while ( (player.Hand?.Cards == null || player.Hand.Cards.Count <= round)
                                && waitedMs < handWaitMs ) {
                                await UniTask.Delay(50, cancellationToken: ct);
                                waitedMs += 50;
                            }

                            if ( player.Hand?.Cards == null || player.Hand.Cards.Count <= round ) {
                                Debug.LogError($"[AnimateDealingRoutineAsync] Hand still missing card at index {round} for local player after {handWaitMs}ms.");
                                Destroy(cardView.gameObject);
                                continue;
                            }

                            Card cardData = player.Hand.Cards[round];
                            Sprite cardSprite = sprites[(cardData.Suit, cardData.Rank)];

                            // Reuse the existing view instance instead of Destroy + Spawn if your CardView supports it,
                            // otherwise spawn face card and destroy the back placeholder:
                            CardView faceCardView = cardSpawner.SpawnCard(cardData, cardSprite);
                            faceCardView.transform.position = cardView.transform.position;
                            faceCardView.transform.rotation = cardView.transform.rotation;
                            faceCardView.transform.SetParent(player.CardHolder.transform, worldPositionStays: true);

                            Destroy(cardView.gameObject);

                            player.CardHolder.AddCard(faceCardView, animate: true);

                            Debug.Log($"[AnimateDealingRoutineAsync] Dealt card {cardData.Rank} of {cardData.Suit} to local player.");
                        }
                        else {
                            cardView.transform.SetParent(player.CardHolder.transform, worldPositionStays: true);
                            player.CardHolder.AddCard(cardView, animate: true);
                        }
                    }

                    await UniTask.Delay(delayMs, cancellationToken: ct);
                }

                // 3. Sort and fan local player hand
                if ( localPlayer?.CardHolder != null ) {
                    localPlayer.CardHolder.SortCards();
                    localPlayer.CardHolder.ArrangeCards(animate: true);
                }
            }
            finally {
                // Destroy leftover deck backs (e.g. 2-player game deals 26
                // of 52). Without this they sit on the table forever.
                while ( deckStack.Count > 0 ) {
                    CardView leftover = deckStack.Dequeue();
                    if ( leftover != null ) {
                        Destroy(leftover.gameObject);
                    }
                }
            }
        }


        private TienLenPlayer GetPlayer( PlayerRef sender ) {
            if ( !sender.IsValid ) {
                Debug.LogWarning($"[GetPlayer] Sender is invalid. sender={sender}, senderId={sender.PlayerId}");
                return null;
            }

            // TienLenPlayer carries no network identity, so resolve the sender
            // through the seat map: seat index -> network player (has PlayerRef).
            TienLenPlayer player = playerRegisterService.GetLogicPlayer( sender );

            if ( player == null ) {
                Debug.LogWarning($"[GetPlayer] No playerSeat found for sender={sender}, senderId={sender.PlayerId}]");
            }

            return player;
        }



        #endregion       

        #region Pass handle
        //TODO: make it support as the new round begin, all the card from last round clear or turn down.
        public void HandlePassRequest( PlayerRef sender ) {
            Debug.Log($"[HandlePassRequest] sender={sender}, senderId={sender.PlayerId}");
            if ( WinnerSeat != -1 ) return;
            TienLenPlayer player = GetPlayer(sender);
            if ( player == null ) {
                Debug.LogWarning($"Cannot find localPlayer for {sender.PlayerId}.");
                return;
            }
            if ( !turnManager.TryPass(player) ) {
                Debug.LogWarning($"Player {player.Id} cannot pass at this time.");
                return;
            }
            MirrorTurnSeat();
            LastPassSeat = SeatOfPlayer(player);
            // Notify all clients that this pass was accepted.
            RPCPassAccepted(sender);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPassAccepted( PlayerRef playerRef ) {
            TienLenPlayer player = GetPlayer(playerRef);
            if ( player == null ) {
                Debug.LogWarning($"Cannot find localPlayer for {playerRef.PlayerId}.");
                return;
            }
            Debug.Log($"[RPCPassAccepted] Player {player.Id} has passed their turn.");
        }
        #endregion

        #region Play handle
        public void HandleAcceptedPlay( PlayerRef sender, TienLenPlayer player, List<Card> playedCards ) {
            if ( player == null ) {
                Debug.LogError("Cannot handle accepted play: localPlayer is null.");
                return;
            }

            // Remove cards from the authoritative hand.
            if ( !player.TryRemoveCards(playedCards) ) {
                Debug.LogError(
                    $"Failed to remove played cardsViews from localPlayer {player.Id}.");
                return;
            }

            // Notify all clients that this play was accepted.
            RPCPlayAccepted(
                sender,
                playedCards.Select(card => new NetworkCard(card)).ToArray()
            );

            MirrorTurnSeat();

            // A newer play supersedes any pass note.
            LastPassSeat = -1;

            if ( player.Hand.Count == 0 ) {
                WinnerSeat = SeatOfPlayer(player);
                Debug.Log($"[Game] Player {player.Id} wins!");
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPlayAccepted( PlayerRef playerRef, NetworkCard[] cards ) {
            List<Card> playedCards = cards
            .Select(card => card.ToCard())
            .ToList();

            // Host already removed these in HandleAcceptedPlay; every other
            // peer removes them from its viewing hand so counts and local
            // validation stay consistent.
            if ( !Object.HasStateAuthority ) {
                GetPlayer(playerRef)?.TryRemoveCards(playedCards);
            }

            HandlePlayPresentation(playerRef, playedCards);
        }

        private void HandlePlayPresentation( PlayerRef playerRef, List<Card> playedCards ) {
            // A new accepted play replaces whatever was on the table.
            if ( tableCenterPosition != null ) {
                tableCenterPosition.Clear();
            }

            TienLenPlayer player = GetPlayer(playerRef);

            if ( player == null )
                return;

            PlayCardsAnimation(player, playedCards);
        }

        private void PlayCardsAnimation( TienLenPlayer player, List<Card> playedCards ) {
            // Move cards to table
            // Flip cards
            // Play sound
            // etc.

            CardHolder hand = player.CardHolder;
            CardHolder table = tableCenterPosition;

            StartCoroutine(AnimatePlayedCards(hand, table, playedCards));

        }

        private IEnumerator AnimatePlayedCards( CardHolder cardHolder, CardHolder table, List<Card> playedCards ) {
            Debug.Log(
                $"[AnimatePlayedCards] Starting animation. " +
                $"playedCards={playedCards?.Count ?? 0}, " +
                $"cardHolder={(cardHolder != null ? cardHolder.name : "NULL")}, " +
                $"table={(table != null ? table.name : "NULL")}");

            if ( cardHolder == null ) {
                Debug.LogError("[AnimatePlayedCards] FAILED: 'cardHolder' is NULL!");
                yield break;
            }

            if ( table == null ) {
                Debug.LogError("[AnimatePlayedCards] FAILED: 'table' is NULL!");
                yield break;
            }

            if ( playedCards == null || playedCards.Count == 0 ) {
                Debug.LogWarning("[AnimatePlayedCards] playedCards is NULL or EMPTY.");
                yield break;
            }

            List<CardView> views = cardHolder.FindCards(playedCards);
            if ( views == null || views.Count == 0 ) {
                Debug.LogWarning("[AnimatePlayedCards] No matching CardView objects found in cardHolder.");
                yield break;
            }

            float duration = 0.35f;
            float cardSpacing = 0.2f;
            Vector3 centerPos = Vector3.zero;

            Debug.Log($"[AnimatePlayedCards] Animating {views.Count} card views.");

            for ( int i = 0; i < views.Count; i++ ) {
                CardView view = views[i];

                if ( view == null ) {
                    Debug.LogError($"[AnimatePlayedCards] CardView at index {i} is NULL!");
                    continue;
                }

                // Reveal the face on every peer. The actor already holds
                // face-up views, but viewers animated card backs — stamp the
                // authoritative played data + sprite so the table shows faces.
                if ( i < playedCards.Count && playedCards[i] != null ) {
                    Card played = playedCards[i];
                    view.Card = played;
                    if ( sprites != null && sprites.TryGetValue((played.Suit, played.Rank), out Sprite faceSprite)
                        && faceSprite != null ) {
                        view.ChangeSprite(faceSprite);
                    }
                }

                Debug.Log($"[AnimatePlayedCards] Animating card '{view.name}' at index {i}.");

                Transform cardTransform = view.transform;

                // Detach from the hand layout group so it doesn't fight the layout system.
                cardTransform.SetParent(cardTransform.root, worldPositionStays: true);

                float offset = (i - (views.Count - 1) / 2f) * cardSpacing;
                Vector3 targetPos = centerPos + new Vector3(offset, 0f, 0f);
                float randomAngle = UnityEngine.Random.Range(-5f, 5f);

                cardTransform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(cardTransform.DOMove(targetPos, duration).SetEase(Ease.OutQuad));
                seq.Join(cardTransform.DORotate(new Vector3(0, 0, randomAngle), duration));
                seq.Join(cardTransform.DOScale(Vector3.one * 0.9f, duration));

                seq.OnComplete(() => {
                    Debug.Log($"[AnimatePlayedCards] Animation complete for '{view.name}'. Adding to table.");
                });

                table.AddCard(view);
            }

            // Views now live on the table: drop them from the hand list so a
            // later hand ArrangeCards does not pull them back.
            cardHolder.RemoveCards(views, animate: false);

            Debug.Log("[AnimatePlayedCards] Animation setup completed.");
            yield return null;
        }


        public void HandlePlayRequest( PlayerRef sender, NetworkCard[] cards ) {
            Debug.Log($"[HandlePlayRequest] sender={sender}, senderId={sender.PlayerId}, cards={cards?.Length ?? 0}");

            if ( WinnerSeat != -1 ) return;

            TienLenPlayer player = GetPlayer(sender);

            if ( player == null ) {
                Debug.LogWarning($"Cannot find localPlayer for {sender.PlayerId}.");
                return;
            }

            List<Card> requestedCards = cards
            .Select(card => card.ToCard())
            .ToList();

            // Verify ownership.
            if ( !player.HasCards(requestedCards) ) {
                Debug.LogWarning($"Player {player.Id} tried to play cardsViews they don't own.");
                return;
            }

            // Evaluate combination.
            if ( !cardCombinationEvaluator.TryEvaluate(
                    requestedCards,
                    out CardCombination combination) ) {
                Debug.LogWarning(
                    $"Player {player.Id} submitted an invalid combination.");
                return;
            }

            // Validate turn + game rules.
            if ( !turnManager.TryPlay(player, combination) ) {
                Debug.LogWarning(
                    $"Player {player.Id} cannot play this combination.");
                return;
            }

            // Accepted.
            HandleAcceptedPlay(sender, player, requestedCards);
        }

        #endregion


        private void HandleTurnChanged( TienLenPlayer player ) {
            Debug.Log($"Turn: {player.PlayerName}");
        }

        private void HandleCardsPlayed( CardCombination combination ) {
            Debug.Log($"Played {combination.Type}");
        }

        private void HandlePlayerWon( TienLenPlayer player ) {
            Debug.Log($"{player.PlayerName} wins!");
        }

        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }

        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }

        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) { }

        public void OnDisconnectedFromServer( NetworkRunner runner, NetDisconnectReason reason ) { }

        public void OnConnectRequest( NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token ) { }

        public void OnConnectFailed( NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason ) { }

        public void OnReliableDataReceived( NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data ) { }

        public void OnReliableDataProgress( NetworkRunner runner, PlayerRef player, ReliableKey key, float progress ) { }

        public void OnInput( NetworkRunner runner, NetworkInput input ) { }

        public void OnInputMissing( NetworkRunner runner, PlayerRef player, NetworkInput input ) { }

        public void OnConnectedToServer( NetworkRunner runner ) { }

        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) { }

        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) { }

        public void OnHostMigration( NetworkRunner runner, HostMigrationToken hostMigrationToken ) { }

        public void OnSceneLoadDone( NetworkRunner runner ) { }

        public void OnSceneLoadStart( NetworkRunner runner ) { }


    }
}