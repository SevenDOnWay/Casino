using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using Fusion;
using Fusion.Sockets;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static Unity.Collections.Unicode;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : NetworkBehaviour, INetworkRunnerCallbacks {

        [System.Serializable]
        public class PlayerHandPosition {
            public int playerId;
            public CardHolder cardHolder;
            public Transform cardHolderPosition; //might not be needed if we use cardholder position directly
        }

        [SerializeField] private PlayerHandPosition[] playerHandPositions = new PlayerHandPosition[4];

        [Space(5)]
        [Header("Start Game Button")]
        [SerializeField] private Button startGameBtn;
        [SerializeField] private TMP_Text startBtnText;

        [Networked] public NetworkBool IsGameStarted { get; set; }

        public bool isGameStartable { get; set; }

        private const int minPlayerToStart = 1; //TODO: Change to 2 or more for actual gameplay


        private TienLenGame game;

        [Header("Dependencies")]
        CardSpawner cardSpawner;


        [SerializeField] TienLenSO tienLenSO;

        Dictionary<(CardSuit, CardRank), Sprite> sprites = new Dictionary<(CardSuit, CardRank), Sprite>();


        [Inject]
        void Construct(
            CardSpawner cardSpawner ) {
            this.cardSpawner = cardSpawner;
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
            game = new TienLenGame();

            game.OnTurnChanged += HandleTurnChanged;
            game.OnCardsPlayed += HandleCardsPlayed;
            game.OnPlayerWon += HandlePlayerWon;
        }

        private void RegisterExistingPlayers() {
            foreach ( PlayerRef player in Runner.ActivePlayers ) {
                RegisterPlayer(player);
            }
        }

        private void RegisterPlayer( PlayerRef player ) {
            if ( game == null ) {
                Debug.LogError("Cannot register player: TienLenGame is not initialized.");
                return;
            }

            // Prevent duplicate registration
            if ( game.Players.Any(p => p.PlayerRef == player) ) {
                return;
            }

            int playerId = FindAvailablePlayerId();

            if ( playerId == -1 ) {
                Debug.LogError($"No available Tiến Lên slot for {player}.");
                return;
            }

            PlayerHandPosition position = playerHandPositions[playerId];

            TienLenPlayer tienLenPlayer = new TienLenPlayer(
                playerId,
                player,
                $"Player {playerId + 1}",
                player == Runner.LocalPlayer
            );

            tienLenPlayer.SetCardHolder(position.cardHolder);

            game.AddPlayer(tienLenPlayer);

            Debug.Log(
                $"[TienLen] Registered {player} as Player {playerId}"
            );
        }

        public override void Despawned( NetworkRunner runner, bool hasState ) {
            if ( startGameBtn != null ) {
                startGameBtn.onClick.RemoveListener(OnStartGameButtonClicked);
            }
        }

        private void OnGameStateChanged() {
            UpdateStartButtonUI();
        }

        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            Debug.Log($"[TienLen] Player joined: {player.PlayerId}");

            if ( !Object.HasStateAuthority ) return;

            RegisterPlayer(player);

            CheckPlayer();
            UpdateStartButtonUI();
        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            Debug.Log($"[TienLen] Player left: {player.PlayerId}");
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
                Debug.Log($"[CreateDeck] SUCCESS! Spawned {list.Count} cards.", this);
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
                    startBtnText.text = $"Need {minPlayerToStart - Runner.ActivePlayers.Count()} more player(s)";
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