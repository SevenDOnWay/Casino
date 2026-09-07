using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : MonoBehaviour {

        [System.Serializable]
        public class PlayerPosition {
            public int playerId;
            public Transform cardHolderPosition;
        }

        [SerializeField]
        private CardHolder[] cardHolders = new CardHolder[4];


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


        private void Start() {
            game = new TienLenGame();

            game.OnTurnChanged += HandleTurnChanged;
            game.OnCardsPlayed += HandleCardsPlayed;
            game.OnPlayerWon += HandlePlayerWon;

            CreateDeck();
            CreatePlayers();

            game.StartGame();


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




        private void CreatePlayers() {
            TienLenPlayer player = new(
                0,
                "Player",
                true
            );

            TienLenPlayer bot1 = new(
                1,
                "Bot 1",
                false
            );

            TienLenPlayer bot2 = new(
                2,
                "Bot 2",
                false
            );

            TienLenPlayer bot3 = new(
                3,
                "Bot 3",
                false
            );

            player.SetCardHolder(cardHolders[0]);
            bot1.SetCardHolder(cardHolders[1]);
            bot2.SetCardHolder(cardHolders[2]);
            bot3.SetCardHolder(cardHolders[3]);

            game.AddPlayer(player);
            game.AddPlayer(bot1);
            game.AddPlayer(bot2);
            game.AddPlayer(bot3);
        }

        private void HandleTurnChanged( TienLenPlayer player ) {
            Debug.Log($"Turn: {player.PlayerName}");
        }

        private void HandleCardsPlayed( CardCombination combination ) {
            Debug.Log($"Played {combination.Type}");
        }

        private void HandlePlayerWon( TienLenPlayer player ) {
            Debug.Log($"{player.PlayerName} wins!");
        }

    }
}