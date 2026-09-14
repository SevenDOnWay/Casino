using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using DG.Tweening;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        [Networked] public NetworkBool IsGameStarted { get; set; }

        [SerializeField] private GameObject tienLenNetworkPlayerPrefab;
        [SerializeField] private PlayerHandPosition[] playerHandPositions = new PlayerHandPosition[4];
        [SerializeField] private PlayerHandPosition tableCenterPosition = new();
        public bool isGameStartable { get; set; }

        private const int minPlayerToStart = 1; //TODO: Change to 2 or more for actual gameplay
        Dictionary<PlayerRef, TienLenPlayer> playerMap = new();
        Dictionary<PlayerRef, TienLenNetWorkPlayer> networkPlayerMap = new();

        [SerializeField] TienLenSO tienLenSO;

        Dictionary<(CardSuit, CardRank), Sprite> sprites = new Dictionary<(CardSuit, CardRank), Sprite>();


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
            if ( startGameBtn != null ) {
                startGameBtn.onClick.AddListener(OnStartGameButtonClicked);
            }

            if ( Object.HasStateAuthority ) {
                Initialize();
                RegisterExistingPlayers();
            }
            // Sync initial state
            CheckPlayer();
            UpdateStartButtonUI();
        }


        private void Initialize() {
            game.OnTurnChanged += HandleTurnChanged;
            game.OnCardsPlayed += HandleCardsPlayed;
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
            if ( sprites == null ) {
                Debug.LogError("[CreateDeck] FAILED: 'sprites' dictionary returned from tienLenSO.GetLookUpTable() is NULL! (Did you call Initialize() inside the SO?)", this);
                return;
            }

            Debug.Log($"[CreateDeck] Retrieved sprites lookup table with {sprites.Count} items.");

            // 4. Check Spawning
            var list = cardSpawner.SpawnAllCard(sprites);
            if ( list == null ) {
                Debug.LogWarning("[CreateDeck] 'cardSpawner.SpawnAllCard(sprites)' executed, but returned NULL! Check inside SpawnAllCard.", this);
            }
            else {
                Debug.Log($"[CreateDeck] SUCCESS! Spawned {list.Count} cardsViews.", this);
            }

            Deck deck = new Deck();
            deck.SetCardView(list);

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

        public void StartGame() {
            CreateDeck();

            game.StartGame();

            turnManager.Initialize(game.Players);

        }


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
            // Security check: Only the host can execute
            if ( !Object.HasStateAuthority || !isGameStartable || IsGameStarted ) return;

            IsGameStarted = true;
            UpdateStartButtonUI();

            StartGame();
        }




        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPlayCard( NetworkCard[] cards, RpcInfo info = default ) {
            PlayerRef sender = info.Source;

            Debug.Log(
                $"[RPCRequestPlayCard] Received RPC. sender={sender}, senderId={sender.PlayerId}, " +
                $"senderIsValid={sender.IsValid}, playerMapCount={playerMap.Count}, " +
                $"networkPlayerMapCount={networkPlayerMap.Count}, hasStateAuthority={Object.HasStateAuthority}"
            );

            var player = GetPlayer(sender);

            if ( player == null ) {
                Debug.LogWarning($"Received play request from unknown localPlayer {sender.PlayerId}.");
                return;
            }

            List<Card> requestedCards = cards.Select(
                card => card.ToCard())
                .ToList();

            // Does the player actually own these cards?
            if ( !player.HasCards(requestedCards) ) {
                Debug.LogWarning(
                    $"Player {player.Id} tried to play cardsViews they don't own.");

                return;
            }


            // Evaluate on the authoritative side.
            if ( !cardCombinationEvaluator.TryEvaluate(requestedCards, out CardCombination combination) ) {
                return;
            }

            // Is this combination legal against the current table?
            if ( !turnManager.TryPlay(player, combination) ) {
                Debug.LogWarning(
                    $"Player {player.Id} cannot play this combination.");

                return;
            }


            HandleAcceptedPlay(player, requestedCards);
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

            if ( playerMap.TryGetValue(sender, out TienLenPlayer player ) ) {
                Debug.Log($"[GetPlayer] Found player. sender={sender}, playerId={player.Id}, playerName={player.PlayerName}");
                return player;
            }

            string knownPlayers = string.Join(", ", playerMap.Keys.Select(p => p.ToString()));
            Debug.LogWarning($"[GetPlayer] No player found for sender={sender}, senderId={sender.PlayerId}. KnownPlayers=[{knownPlayers}]");

            return null;
        }

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
                $"table={(table != null ? table.name : "NULL")}" );

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