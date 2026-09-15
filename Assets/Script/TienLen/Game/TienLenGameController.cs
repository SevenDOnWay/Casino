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
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : NetworkBehaviour, INetworkRunnerCallbacks {
        [Header("Dependencies")]
        CardSpawner cardSpawner;
        CardCombinationEvaluator cardCombinationEvaluator;
        LocalPlayerService localPlayerService;
        TienLenGame game;
        TurnManager turnManager;


        [Space(5)]
        [Header("Start Game Button")]
        [SerializeField] private Button startGameBtn;
        [SerializeField] private TMP_Text startBtnText;
        private const int minPlayerToStart = 1; //TODO: Change to 2 or more for actual gameplay
        private const int maxPlayerToStart = 4;

        [Networked] 
        public NetworkBool IsGameStarted { get; set; }

        [SerializeField] private GameObject tienLenNetworkPlayerPrefab;
        [SerializeField] private PlayerHandPosition[] playerHandPositions = new PlayerHandPosition[4];
        [SerializeField] private PlayerHandPosition tableCenterPosition = new();
        public bool isGameStartable { get; set; }
        private bool lastRenderedGameStarted;

        Dictionary<PlayerRef, TienLenPlayer> playerMap = new();
        Dictionary<PlayerRef, TienLenNetWorkPlayer> networkPlayerMap = new();

        [SerializeField] TienLenSO tienLenSO;

        Dictionary<(CardSuit, CardRank), Sprite> sprites = new Dictionary<(CardSuit, CardRank), Sprite>();
        Sprite cardBack;

        public event Action OnRoundStarted;


        [Inject]
        void Construct( CardSpawner cardSpawner,
            LocalPlayerService localPlayerService,
            TienLenGame game,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator ) {
            this.cardSpawner = cardSpawner;
            this.localPlayerService = localPlayerService;
            this.game = game;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
        }

        public override void Spawned() {
            if ( startGameBtn != null ) startGameBtn.onClick.AddListener(OnStartGameButtonClicked);

            if ( Object.HasStateAuthority ) {
                Initialize();
                RegisterExistingPlayers();
            }
            // Sync initial state
            CheckPlayer();
            UpdateStartButtonUI();
        }


        private void Initialize() {
            //game.OnTurnChanged += HandleTurnChanged;
            //game.OnCardsPlayed += HandleCardsPlayed;
            game.OnPlayerWon += HandlePlayerWon;
        }

        private void RegisterExistingPlayers() {
            foreach ( PlayerRef player in Runner.ActivePlayers ) {
                SpawnNetworkPlayer(player);
            }
        }

        private void SpawnNetworkPlayer( PlayerRef player ) {
            if ( !Object.HasStateAuthority ) {
                Debug.LogError("[SpawnNetworkPlayer] Only the host can spawn network players.");
                return;
            }

            if ( tienLenNetworkPlayerPrefab == null ) {
                Debug.LogError("[SpawnNetworkPlayer] tienLenNetworkPlayerPrefab is not assigned in the inspector.");
                return;
            }

            // Prevent duplicate spawning
            if ( networkPlayerMap.ContainsKey(player) ) {
                Debug.LogWarning($"[SpawnNetworkPlayer] Player {player.PlayerId} already has a network localPlayer spawned.");
                return;
            }

            int playerId = FindAvailablePlayerId();
            if ( playerId == -1 ) {
                Debug.LogError($"No available Tiến Lên slot for {player}.");
                return;
            }

            // Spawn the TienLenNetWorkPlayer prefab
            NetworkObject spawnedObject = Runner.Spawn(
                    tienLenNetworkPlayerPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    inputAuthority: player,
                    onBeforeSpawned: ( runner, obj ) => {
                        TienLenNetWorkPlayer networkPlayer = obj.GetComponent<TienLenNetWorkPlayer>();
                        if ( networkPlayer == null ) {
                            Debug.LogError("[SpawnNetworkPlayer] onBeforeSpawned could not find TienLenNetWorkPlayer component.");
                            return;
                        }

                        networkPlayer.PlayerRef = player;
                        networkPlayer.PlayerId = playerId;
                        networkPlayer.PlayerName = new NetworkString<_16>($"Player {playerId + 1}");
                        networkPlayer.Controller = this;
                    }
                );

            TienLenNetWorkPlayer networkPlayer = spawnedObject.GetComponent<TienLenNetWorkPlayer>();
            if ( networkPlayer == null ) {
                Debug.LogError($"[SpawnNetworkPlayer] Spawned object does not have TienLenNetWorkPlayer component.");
                Runner.Despawn(spawnedObject);
                return;
            }

            networkPlayerMap[player] = networkPlayer;

            Debug.Log($"[SpawnNetworkPlayer] Spawned network localPlayer for {player} with ID {networkPlayer.PlayerId}");
        }

        public void RegisterPlayer( TienLenNetWorkPlayer networkPlayer ) {
            if ( game == null ) {
                Debug.LogError("Cannot register localPlayer: TienLenGame is not initialized.");
                return;
            }

            PlayerRef player = networkPlayer.PlayerRef;

            Debug.Log(
                $"[RegisterPlayer] PlayerRef={player}, PlayerId={networkPlayer.PlayerId}, " +
                $"IsValid={player.IsValid}, CurrentPlayerMapCount={playerMap.Count}, " +
                $"CurrentNetworkPlayerMapCount={networkPlayerMap.Count}"
            );

            // Prevent duplicate registration
            if ( playerMap.ContainsKey(player) || game.Players.Any(p => p.PlayerRef == player) ) {
                Debug.LogWarning($"[RegisterPlayer] Duplicate registration skipped for {player}. " +
                                 $"playerMapHasKey={playerMap.ContainsKey(player)}, " +
                                 $"gameHasPlayer={game.Players.Any(p => p.PlayerRef == player)}");
                return;
            }

            if ( networkPlayer == null ) {
                Debug.LogError($"Cannot register localPlayer for {player}: networkPlayer is null.");
                return;
            }

            networkPlayerMap[player] = networkPlayer;

            int playerId = networkPlayer.PlayerId;
            if ( playerId < 0 || playerId >= 4 ) {
                Debug.LogError($"Invalid playerId: {playerId}");
                return;
            }

            PlayerHandPosition position = playerHandPositions[playerId];

            TienLenPlayer tienLenPlayer = new TienLenPlayer(
                        playerId,
                        player,
                        networkPlayer.PlayerName.ToString(),
                        this
                    );

            tienLenPlayer.SetCardHolder(position.cardHolder);
            bool isLocalPlayer = (player == Runner.LocalPlayer);
            if ( isLocalPlayer ) {
                localPlayerService.SetNetworkPlayer(networkPlayer);
                localPlayerService.SetLocalPlayer(tienLenPlayer);
            }

            position.cardHolder.SetInteractable(isLocalPlayer);

            playerMap[player] = tienLenPlayer;
            game.AddPlayer(tienLenPlayer);


            Debug.Log($"[TienLen] Registered {player} as Player {playerId}. playerMapCount={playerMap.Count}");
        }

        public override void Despawned( NetworkRunner runner, bool hasState ) {
            if ( startGameBtn != null ) {
                startGameBtn.onClick.RemoveListener(OnStartGameButtonClicked);
            }
        }

        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            Debug.Log($"[TienLen] Player joined: {player.PlayerId}");

            if ( Object.HasStateAuthority ) {
                SpawnNetworkPlayer(player);
            }

            CheckPlayer();
            UpdateStartButtonUI();
        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            Debug.Log($"[TienLen] Player left: {player.PlayerId}");

            //TODO: unregister play and support quick reconnect.
            Debug.Log($"[TienLen] Player left: {player.PlayerId}");

            if ( networkPlayerMap.TryGetValue(player, out var networkPlayer) ) {
                Runner.Despawn(networkPlayer.Object);
                networkPlayerMap.Remove(player);
            }

            playerMap.Remove(player);
            game.RemovePlayer(player);

            CheckPlayer();
            UpdateStartButtonUI();
        }


        private int FindAvailablePlayerId() {
            for ( int i = 0; i < 4; i++ ) {
                if ( !game.Players.Any(p => p.Id == i) ) return i;
            }

            return -1;
        }

        private void CreateDeck() {
            Debug.Log("[CreateDeck] Starting deck creation...", this);

            // 1. Check CardSpawner injection state
            if ( cardSpawner == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'cardSpawner' is NULL! (Construct might not have run yet, or execution order called CreateDeck too early in Awake)", this);
                return;
            }

            // 2. Check ScriptableObject reference
            if ( tienLenSO == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'tienLenSO' field is NULL! (Assign it in the Inspector)", this); 
                return;
            }

            // 3. Check Lookup Table
            sprites = tienLenSO.GetLookUpTable();
            cardBack = tienLenSO.GetCardBackSprite();
            if ( sprites == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'sprites' dictionary returned from tienLenSO.GetLookUpTable() is NULL! (Did you call Initialize() inside the SO?)", this);
                return;
            }
            if ( cardBack == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'cardBack' sprite returned from tienLenSO.GetCardBackSprite() is NULL! (Did you assign a card back sprite in the SO?)", this);
                return;
            }

            Debug.Log($"[CreateDeck] Retrieved sprites lookup table with {sprites.Count} items.");

            Deck deck = new Deck();
            deck.CreateDeck();
            game.SetDeck(deck);
        }

        private void CheckPlayer() {
            int currentConnectedPlayers = Runner.ActivePlayers.Count();

            if ( currentConnectedPlayers >= minPlayerToStart ) {
                Debug.Log($"[TienLen] Player threshold reached ({currentConnectedPlayers}/{minPlayerToStart}). Starting game...");
                isGameStartable = true;
            }
            else {
                Debug.Log($"[TienLen] Waiting for more players... ({currentConnectedPlayers}/{minPlayerToStart})");
                isGameStartable = false;
            }
        }


        //private void CreateNetworkedPlayers() {
        //    // Assign card holders dynamically based on real connected players
        //    int seatIndex = 0;
        //    foreach ( PlayerRef pRef in Runner.ActivePlayers ) {
        //        bool isLocalPlayer = (pRef == Runner.LocalPlayer);
        //        string playerName = isLocalPlayer ? $"Player (You - {pRef.PlayerId})" : $"Player {pRef.PlayerId}";

        //        TienLenPlayer player = new(pRef.PlayerId, playerName, isLocalPlayer);

        //        if ( seatIndex < cardHolders.Length ) {
        //            player.SetCardHolder(cardHolders[seatIndex]);
        //        }

        //        game.AddPlayer(player);
        //        seatIndex++;
        //    }

        //    // Optional: Fill remaining empty slots up to 4 with bots if needed
        //    /*
        //    while (seatIndex < 4) 
        //    {
        //        TienLenPlayer bot = new(seatIndex, $"Bot {seatIndex}", false);
        //        bot.SetCardHolder(cardHolders[seatIndex]);
        //        game.AddPlayer(bot);
        //        seatIndex++;
        //    }
        //    */
        //}


        //[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        //private void RPC_NotifyGameStarted() {
        //    UpdateStartButtonUI();
        //}

        #region Deal Cards

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPlayDealAnimation() {
            AnimateDealingRoutineAsync(destroyCancellationToken).Forget();
        }

        private async UniTask AnimateDealingRoutineAsync( CancellationToken ct ) {
            int delayMs = 60;
            TienLenPlayer localPlayer = localPlayerService.Player;
            ct.ThrowIfCancellationRequested();

            Debug.Log($"[AnimateDealingRoutineAsync] Start. localPlayer={(localPlayer != null ? localPlayer.PlayerName : "NULL")}, players={game.Players.Count()}, delayMs={delayMs}", this);

            // 1. Spawn 52 card backs in the center table position (deck stack)
            Vector3 centerDeckPos = tableCenterPosition != null
        ? tableCenterPosition.cardHolder.transform.position
        : Vector3.zero;

            Debug.Log($"[AnimateDealingRoutineAsync] centerDeckPos={centerDeckPos}", this);

            Queue<CardView> deckStack = new Queue<CardView>(52);
            for ( int i = 0; i < 52; i++ ) {
                CardView backView = cardSpawner.SpawnCardBack(cardBack);
                backView.transform.position = centerDeckPos;
                // Keep inside controller's local hierarchy for Multi-Peer isolation
                backView.transform.SetParent(transform, worldPositionStays: true);
                deckStack.Enqueue(backView);
            }

            // 2. Deal 13 rounds to all players
            for ( int i = 0; i < 13; i++ ) {
                Debug.Log($"[AnimateDealingRoutineAsync] Dealing round {i + 1}/13. deckStackRemaining={deckStack.Count}", this);

                foreach ( var player in game.Players ) {
                    ct.ThrowIfCancellationRequested();

                    if ( deckStack.Count == 0 ) {
                        Debug.LogWarning("[AnimateDealingRoutineAsync] Deck stack ran out early!");
                        break;
                    }

                    CardView movingCardBack = deckStack.Dequeue();
                    bool isLocal = (player.PlayerRef == Runner.LocalPlayer);

                    Debug.Log($"[AnimateDealingRoutineAsync] Dealing to player={player.PlayerName} ref={player.PlayerRef.PlayerId} isLocal={isLocal}", this);

                    if ( isLocal ) {
                        // Guard: Make sure hand data arrived
                        if ( player.Hand?.Cards == null || player.Hand.Cards.Count <= i ) {
                            Debug.LogError($"[AnimateDealingRoutineAsync] Hand data missing at index {i} for {player.PlayerName}! Destroying placeholder.", this);
                            Destroy(movingCardBack.gameObject);
                            continue;
                        }

                        Card cardData = player.Hand.Cards[i];
                        Sprite cardSprite = sprites[(cardData.Suit, cardData.Rank)];

                        // Spawn actual face card
                        CardView faceCardView = cardSpawner.SpawnCard(cardData, cardSprite);
                        faceCardView.transform.position = movingCardBack.transform.position;
                        faceCardView.transform.rotation = movingCardBack.transform.rotation;
                        faceCardView.transform.SetParent(player.CardHolder.transform, worldPositionStays: true);

                        // Destroy placeholder back
                        Destroy(movingCardBack.gameObject);

                        // Animate card into holder
                        player.CardHolder.AddCard(faceCardView, animate: true);
                    }
                    else {
                        // Opponent receives the card back
                        movingCardBack.transform.SetParent(player.CardHolder.transform, worldPositionStays: true);
                        player.CardHolder.AddCard(movingCardBack, animate: true);
                    }
                }

                Debug.Log($"[AnimateDealingRoutineAsync] Round {i + 1} waiting {delayMs}ms.", this);
                await UniTask.Delay(delayMs, cancellationToken: ct);
            }

            // 3. Clean up remaining unused cards OUTSIDE the loop (after all 13 rounds finish)
            Debug.Log($"[AnimateDealingRoutineAsync] Cleaning up {deckStack.Count} remaining cards in deckStack.", this);
            while ( deckStack.Count > 0 ) {
                CardView remaining = deckStack.Dequeue();
                if ( remaining != null ) {
                    Destroy(remaining.gameObject);
                }
            }

            // 4. Sort and arrange local hand once all cards arrive
            if ( localPlayer?.CardHolder != null ) {
                localPlayer.CardHolder.SortCards();
                localPlayer.CardHolder.ArrangeCards(animate: true);
            }

            Debug.Log("[AnimateDealingRoutineAsync] Completed.", this);
        }


        private TienLenPlayer GetPlayer( PlayerRef sender ) {
            if ( !sender.IsValid ) {
                Debug.LogWarning($"[GetPlayer] Sender is invalid. sender={sender}, senderId={sender.PlayerId}, playerMapCount={playerMap.Count}");
                return null;
            }

            if ( playerMap.Count == 0 ) {
                Debug.LogWarning($"[GetPlayer] There is no localPlayer register in controller. sender={sender}, networkPlayerMapCount={networkPlayerMap.Count}");
                return null;
            }

            if ( playerMap.TryGetValue(sender, out TienLenPlayer player) ) {
                Debug.Log($"[GetPlayer] Found player. sender={sender}, playerId={player.Id}, playerName={player.PlayerName}");
                return player;
            }

            string knownPlayers = string.Join(", ", playerMap.Keys.Select(p => p.ToString()));
            Debug.LogWarning($"[GetPlayer] No player found for sender={sender}, senderId={sender.PlayerId}. KnownPlayers=[{knownPlayers}]");

            return null;
        }



        #endregion

        //TODO:refactor into other script 
        #region UI
        private void UpdateStartButtonUI() {
            if ( startGameBtn == null ) return;

            // Hide the button for everyone once the game has started
            if ( IsGameStarted ) {
                startGameBtn.gameObject.SetActive(false);
                return;
            }

            // Keep visible for everyone before the match starts
            startGameBtn.gameObject.SetActive(true);

            // ONLY the host can click it, and ONLY if enough players joined
            bool isHost = Object.HasStateAuthority;
            startGameBtn.interactable = isHost && isGameStartable;

            // Optional: Provide visual feedback text
            if ( startBtnText != null ) {
                if ( !isHost ) {
                    startBtnText.text = "Waiting for Host to start...";
                }
                else if ( !isGameStartable ) {
                    startBtnText.text = $"Need {minPlayerToStart - Runner.ActivePlayers.Count()} more localPlayer(s)";
                }
                else {
                    startBtnText.text = "Start Game";
                }
            }
        }

        private void OnStartGameButtonClicked() {
            if ( !Object.HasStateAuthority )
                return;

            if ( !isGameStartable )
                return;

            if ( IsGameStarted )
                return;

            Debug.Log("[StartGame] Host starting authoritative game state.");

            // Networked state.
            IsGameStarted = true;

            // Host only.
            StartGameState();
        }

        private void StartGameState() {
            if ( !Object.HasStateAuthority ) return;

            CreateDeck();

            game.StartGame();

            turnManager.Initialize(game.Players);

            RPCPlayDealAnimation();

            OnRoundStarted?.Invoke();
        }

        public override void Render() {
            if ( lastRenderedGameStarted == IsGameStarted )
                return;

            lastRenderedGameStarted = IsGameStarted;

            Debug.Log(
                $"[Render] GameStarted={IsGameStarted}, " +
                $"LocalPlayer={Runner.LocalPlayer}, " +
                $"StateAuthority={Object.HasStateAuthority}",
                this);

            UpdateStartButtonUI();
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

        [System.Serializable]
        public class PlayerHandPosition {
            public int playerId;
            public CardHolder cardHolder;
            public Transform cardHolderPosition; //might not be needed if we use cardholder position directly
        }
    }
}