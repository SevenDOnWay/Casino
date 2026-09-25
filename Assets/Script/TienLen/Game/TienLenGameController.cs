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
        SeatProvider seatProvider;

        private const int totalSeats = 4;

        private IReadOnlyList<PlayerSeat> playerSeats;
        [SerializeField] private PlayerSeat tableCenterPosition; //TODO: change the name for better understanding
        [SerializeField] TienLenSO tienLenSO;


        //Dictionary<PlayerRef, TienLenNetWorkPlayer> NetworkPlayerMap;
        NetworkDictionary<int, NetworkObject> occupiedSeats;
        //Dictionary<int, TienLenPlayer> playerMap;


        private bool lastRenderedGameStarted;
        Dictionary<(CardSuit, CardRank), Sprite> sprites = new Dictionary<(CardSuit, CardRank), Sprite>();
        Sprite cardBack;

        public event Action OnRoundStarted;


        [Inject]
        void Construct(CardSpawner cardSpawner,
            LocalPlayerService localPlayerService,
            TienLenGame game,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            SeatProvider seatProvider ) {
            this.cardSpawner = cardSpawner;
            this.localPlayerService = localPlayerService;
            this.game = game;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.seatProvider = seatProvider;
        }

        public void OnEnable() {
            //lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoined;
            //lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
            //lobbySessionController.OnGameStartedEvent += HandleGameStarted;
        }

        public void OnDisable() {
            //lobbySessionController.OnPlayerJoinedEvent -= HandlePlayerJoined;
            //lobbySessionController.OnPlayerLeftEvent -= HandlePlayerLeft;
            //lobbySessionController.OnGameStartedEvent -= HandleGameStarted;
        }

        private void Start() {
            sprites = tienLenSO.GetLookUpTable();
            cardBack = tienLenSO.GetCardBackSprite();
        }

        private void HandlePlayerJoined( NetworkRunner runner ) {
            //CheckPlayer();
            //UpdateStartButtonUI();
        }

        private void HandlePlayerLeft( NetworkRunner runner ) {
            //CheckPlayer();
            //UpdateStartButtonUI();
        }




        private Deck CreateDeck() {
            Debug.Log("[CreateDeck] Starting deck creation...", this);

            // 1. Check CardSpawner injection state
            if ( cardSpawner == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'cardSpawner' is NULL! (Construct might not have run yet, or execution order called CreateDeck too early in Awake)", this);
                return null;
            }

            // 2. Check ScriptableObject reference
            if ( tienLenSO == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'tienLenSO' field is NULL! (Assign it in the Inspector)", this);
                return null;
            }

            // 3. Check Lookup Table
            

            if ( sprites == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'sprites' dictionary returned from tienLenSO.GetLookUpTable() is NULL! (Did you call Initialize() inside the SO?)", this);
                return null;
            }
            if ( cardBack == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'cardBack' sprite returned from tienLenSO.GetCardBackSprite() is NULL! (Did you assign a card back sprite in the SO?)", this);
                return null;
            }

            Debug.Log($"[CreateDeck] Retrieved sprites lookup table with {sprites.Count} items.");

            Deck deck = new Deck();
            deck.CreateDeck();

            return deck;



            //game.Initialize();
            //game.StartGame();
            //turnManager.Initialize(game.Players);

        }

        public void StartGame() {
            if ( !Object.HasStateAuthority ) return;

            occupiedSeats = seatProvider.OccupiedSeats;
            PlayerSeat[] occupiedPlayerSeats = seatProvider.GetAllOccupiedSeats();

            List<TienLenNetWorkPlayer> networkPlayers = new();
            List<TienLenPlayer> logicPlayers = new();


            foreach (var kvp in occupiedSeats ) {
                NetworkObject netObj = kvp.Value;
                TienLenNetWorkPlayer networkPlayer = netObj.GetComponent<TienLenNetWorkPlayer>();

                if ( networkPlayer != null )  networkPlayers.Add(networkPlayer);
            }

            



            //TODO: get network players 

            Deck deck = CreateDeck();

            DealCard(deck, occupiedPlayerSeats);

            Initialize();
            OnRoundStarted?.Invoke();
        }

        private void Initialize() {
            //game.OnTurnChanged += HandleTurnChanged;
            //game.OnCardsPlayed += HandleCardsPlayed;
            //game.OnPlayerWon += HandlePlayerWon;

            

           

        }


        #region Deal Cards

        private void DealCard(Deck deck, PlayerSeat[] occupiedPlayerSeats ) {
            var tempDeck = deck;
            tempDeck.Shuffle();

            List<TienLenPlayer> logicPlayers = new List<TienLenPlayer>();

            foreach ( var seat in occupiedPlayerSeats ) {
                TienLenPlayer logicPlayer = seat.tienLenPlayer;
                if ( logicPlayer != null ) logicPlayers.Add(logicPlayer);
            }

            // Deal 13 cards to each player's data hand
            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in logicPlayers ) {
                    var drawnCard = tempDeck.DrawCard();
                    player.Hand.AddCard(drawnCard);
                }
            }


            RPCPlayDealAnimation();
        }


        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPCPlayDealAnimation(RpcInfo info = default ) {
            AnimateDealingRoutineAsync(destroyCancellationToken).Forget();

            Debug.Log($"[RPCPlayDealAnimation] Dealing animation started on all clients. Runner: {info.Source}");
        }

        private async UniTask AnimateDealingRoutineAsync(CancellationToken ct ) {
            const int delayMs = 60;
            const int cardsPerPlayer = 13;
            const int totalDeckSize = 52;

            TienLenPlayer localPlayer = localPlayerService.Player;
            PlayerSeat[] occupiedPlayerSeats = seatProvider.GetAllOccupiedSeats();

            Vector3 centerDeckPos = tableCenterPosition != null
                                ? tableCenterPosition.cardHolder.transform.position
                                : Vector3.zero;

            Queue<CardView> deckStack = new Queue<CardView>(totalDeckSize);

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
                    foreach ( var playerSeat in occupiedPlayerSeats ) {
                        ct.ThrowIfCancellationRequested();

                        if ( deckStack.Count == 0 ) {
                            Debug.LogWarning("[AnimateDealingRoutineAsync] Deck stack exhausted early!");
                            break;
                        }

                        CardView cardView = deckStack.Dequeue();
                        TienLenPlayer player = playerSeat.tienLenPlayer;

                        //if ( player == null || player.CardHolder == null ) {
                        //    Debug.LogWarning($"[AnimateDealingRoutineAsync] Player or card holder is null for player {player?.Id ?? -1}");
                        //    Destroy(cardView.gameObject);
                        //    continue;
                        //}

                        if ( player == null ) {
                            Debug.LogError(
                                $"[Deal] Player is NULL. " +
                                $"Seat={playerSeat?.GetSeatIndex() ?? -1}"
                            );

                            Destroy(cardView.gameObject);
                            continue;
                        }

                        if ( player.CardHolder == null ) {
                            Debug.LogError(
                                $"[Deal] CardHolder is NULL. " +
                                $"PlayerId={player.Id}, " +
                                $"PlayerRef={player.PlayerRef}"
                            );

                            Destroy(cardView.gameObject);
                            continue;
                        }

                        bool isLocal = (player.PlayerRef == Runner.LocalPlayer);

                        Debug.Log($"[AnimateDealingRoutineAsync] Dealing card to player {player.Id} (local={isLocal}) at round {round}, " +
                            $"runner: {Runner.LocalPlayer}. player {player.PlayerRef}");

                        if ( isLocal ) {
                            if ( player.Hand?.Cards == null || player.Hand.Cards.Count <= round ) {
                                Debug.LogError($"[AnimateDealingRoutineAsync] Missing card at index {round} for local player.");
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
                // Guaranteed cleanup for remaining unused deck cards even if canceled
                //while ( deckStack.Count > 0 ) {
                //    CardView leftover = deckStack.Dequeue();
                //    if ( leftover != null ) {
                //        Destroy(leftover.gameObject);
                //    }
                //}
            }
        }


        private TienLenPlayer GetPlayer( PlayerRef sender ) {
            if ( !sender.IsValid ) {
                Debug.LogWarning($"[GetPlayer] Sender is invalid. sender={sender}, senderId={sender.PlayerId}");
                return null;
            }

            foreach ( var player in seatProvider.GetAllOccupiedSeats() ) {
                if ( player.tienLenPlayer.PlayerRef == sender ) {
                    return player.tienLenPlayer;
                }
            }

            Debug.LogWarning($"[GetPlayer] No playerSeat found for sender={sender}, senderId={sender.PlayerId}]");

            return null;
        }



        #endregion       

        #region Pass handle
        //TODO: make it support as the new round begin, all the card from last round clear or turn down.
        public void HandlePassRequest( PlayerRef sender ) {
            Debug.Log($"[HandlePassRequest] sender={sender}, senderId={sender.PlayerId}");
            TienLenPlayer player = GetPlayer(sender);
            if ( player == null ) {
                Debug.LogWarning($"Cannot find localPlayer for {sender.PlayerId}.");
                return;
            }
            if ( !turnManager.TryPass(player) ) {
                Debug.LogWarning($"Player {player.Id} cannot pass at this time.");
                return;
            }
            // Notify all clients that this pass was accepted.
            RPCPassAccepted(player.PlayerRef);
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
        public void HandleAcceptedPlay( TienLenPlayer player, List<Card> playedCards ) {
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
                player.PlayerRef,
                playedCards.Select(card => new NetworkCard(card)).ToArray()
            );
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPlayAccepted( PlayerRef playerRef, NetworkCard[] cards ) {
            List<Card> playedCards = cards
            .Select(card => card.ToCard())
            .ToList();

            HandlePlayPresentation(playerRef, playedCards);
        }

        private void HandlePlayPresentation( PlayerRef playerRef, List<Card> playedCards ) {
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
            CardHolder table = tableCenterPosition.cardHolder;

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

            Debug.Log("[AnimatePlayedCards] Animation setup completed.");
            yield return null;
        }


        public void HandlePlayRequest( PlayerRef sender, NetworkCard[] cards ) {
            Debug.Log($"[HandlePlayRequest] sender={sender}, senderId={sender.PlayerId}, cards={cards?.Length ?? 0}");

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
            HandleAcceptedPlay(player, requestedCards);
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