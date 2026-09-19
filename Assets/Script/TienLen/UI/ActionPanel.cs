using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class ActionPanel : MonoBehaviour {
        [Header("Dependencies")]
        private LobbySessionController lobbySessionController;
        private LocalPlayerService localPlayerService;
        private TienLenRuleValidator validator;
        private CardCombinationEvaluator combinationEvaluator;
        private TurnManager turnManager;
        private TienLenGame game;
        private TienLenGameController gameController;

        [Header("UI Elements")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Button playBtn;
        [SerializeField] private Button sortBtn;
        [SerializeField] private Button passBtn;

        private CardHolder cardHolder;
        private TienLenPlayer localPlayer;
        private CardCombination currentCombination;


        private bool isLocalPlayerTurn => turnManager.CurrentPlayer == localPlayer;


        [Inject]
        void Construct( LobbySessionController lobbySessionController,
            LocalPlayerService localPlayerService,
            TienLenRuleValidator validator,
            CardCombinationEvaluator combinationEvaluator,
            TurnManager turnManager,
            TienLenGame game,
            TienLenGameController gameController) {
            this.lobbySessionController = lobbySessionController;
            this.localPlayerService = localPlayerService;
            this.validator = validator;
            this.combinationEvaluator = combinationEvaluator;
            this.turnManager = turnManager;
            this.game = game;
            this.gameController = gameController;
        }

        public void Start() {
            actionPanel.SetActive(false);

            playBtn.onClick.AddListener(OnPlayBtnClick);
            sortBtn.onClick.AddListener(OnSortBtnClick);
            passBtn.onClick.AddListener(OnPassBtnClick);
        }

        public void OnEnable() {
            localPlayerService.OnLocalPlayerSet += HandleLocalPlayerSet;
            //game.OnRoundStarted += HandleStartRound;
            gameController.OnRoundStarted += HandleStartRound;
            turnManager.OnTurnChanged += HandleTurnChanged;


            lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoined;
            lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;

            if ( localPlayerService.Player != null ) {
                HandleLocalPlayerSet(localPlayerService.Player);
            }
        }

        public void OnDisable() {
            localPlayerService.OnLocalPlayerSet -= HandleLocalPlayerSet;
            //game.OnRoundStarted -= HandleStartRound;
            gameController.OnRoundStarted -= HandleStartRound;
            turnManager.OnTurnChanged -= HandleTurnChanged;
            lobbySessionController.OnPlayerJoinedEvent -= HandlePlayerJoined;
            lobbySessionController.OnPlayerLeftEvent -= HandlePlayerLeft;
        }

        private void HandleLocalPlayerSet( TienLenPlayer player ) {
            localPlayer = player;

            localPlayer.CardHolder.OnCardSelected += HandleCardSelected;
        }

        private void HandlePlayerJoined( NetworkRunner runner ) {
            if ( runner.LocalPlayer == localPlayer?.PlayerRef ) {
                actionPanel.SetActive(true);
            }
        }

        private void HandlePlayerLeft( NetworkRunner runner ) {
            if ( runner.LocalPlayer == localPlayer?.PlayerRef ) {
                actionPanel.SetActive(false);
            }
        }

        private void HandleStartRound() {
            actionPanel.SetActive(true);

            currentCombination = null;
            ChangePlayButtonState(true);
            ChangePassButtonState(true);
        }

        private void HandleTurnChanged() {
            // Check if it's the local player's turn
            if ( localPlayer == null ) {
                actionPanel.SetActive(false);
                return;
            }

            ChangePlayButtonState(isLocalPlayerTurn);
            ChangePassButtonState(isLocalPlayerTurn);

        }

        void OnPlayBtnClick() {
            if ( localPlayer == null ) return;

            var hand = localPlayer.CardHolder;
            if ( hand == null ) {
                Debug.LogWarning("No hand available to play.");
                return;
            }

            var selectedCards = hand.SelectedCards;
            if ( selectedCards == null || selectedCards.Count == 0 ) {
                Debug.LogWarning("No cardsViews selected to play.");
                return;
            }

            List<Card> cards = selectedCards
                            .Select(view => view.Card)
                            .ToList();

            // 1. Evaluate selected cards.
            if ( !combinationEvaluator.TryEvaluate(
                    cards,
                    out CardCombination combination) ) {
                Debug.Log("Selected cardsViews do not form a valid combination.");
                return;
            }

            NetworkCard[] networkCards = combination.Cards
                .Select(card => new NetworkCard { Rank = (byte)card.Rank, Suit = (byte)card.Suit })
                .ToArray();

            var networkPlayer = localPlayerService.NetworkPlayer;
            if ( networkPlayer == null ) {
                Debug.LogWarning("[ActionPanel] Cannot call RPCRequestPlayCard because NetworkPlayer is null.");
                return;
            }

            Debug.Log(
                $"[ActionPanel] Calling RPCRequestPlayCard. " +
                $"PlayerRef={networkPlayer.PlayerRef}, " +
                $"LocalPlayer={networkPlayer.Runner.LocalPlayer}, " +
                $"HasInputAuthority={networkPlayer.Object.HasInputAuthority}, " +
                $"SelectedCards={selectedCards.Count}, " +
                $"Combination={combination.Type}"
            );

            networkPlayer.RPCRequestPlayCard(networkCards);

            Debug.Log("[ActionPanel] RPCRequestPlayCard sent successfully.");

            // 3. Play succeeded.
            Debug.Log($"Played {combination.Type}");

            var cardsToAnimate = new List<CardView>(selectedCards);
            localPlayer.CardHolder.RemoveCards(selectedCards);

            // Center animation settings
            //animate(cardsToAnimate);
        }

        private void animate( List<CardView> cardsToAnimate ) {
            
            
        }

        void OnSortBtnClick() {

        }

        void OnPassBtnClick() {
            var networkPlayer = localPlayerService.NetworkPlayer;
            if ( networkPlayer == null ) {
                Debug.LogWarning("[ActionPanel] Cannot call RPCRequestPass because NetworkPlayer is null.");
                return;
            }

            networkPlayer.RPCRequestPass();
        }


        void ChangePlayButtonState( bool enable ) {
            if ( !isLocalPlayerTurn ) return;

            playBtn.interactable = enable;
        }

        void ChangePassButtonState( bool enable ) {
            if ( !isLocalPlayerTurn ) return;

            passBtn.interactable = enable;
        }

        void HandleCardSelected( IReadOnlyList<CardView> selectedCards ) {
            if ( localPlayer == null ) return;

            List<Card> cards = selectedCards
                            .Select(view => view.Card)
                            .ToList();

            if ( !combinationEvaluator.TryEvaluate(cards, out CardCombination combination) ) return;

            if ( validator.CanPlay(combination) ) ChangePlayButtonState(true);
            else ChangePlayButtonState(false);

        }


        private void OnDestroy() {
            sortBtn.onClick.RemoveListener(OnSortBtnClick);
            playBtn.onClick.RemoveListener(OnPlayBtnClick);
            passBtn.onClick.RemoveListener(OnPassBtnClick);
        }
    }

}