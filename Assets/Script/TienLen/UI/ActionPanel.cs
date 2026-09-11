using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class ActionPanel : MonoBehaviour {
        [Header("Dependencies")]
        private LocalPlayerService localPlayerService;
        private TienLenRuleValidator validator;
        private CardCombinationEvaluator combinationEvaluator;
        private TurnManager turnManager;

        [Header("UI Elements")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Button playBtn;
        [SerializeField] private Button sortBtn;
        [SerializeField] private Button passBtn;

        private CardHolder cardHolder;
        private TienLenPlayer localPlayer;
        private CardCombination currentCombination;



        [Inject]
        void Construct( LocalPlayerService localPlayerService,
            TienLenRuleValidator validator,
            CardCombinationEvaluator combinationEvaluator,
            TurnManager turnManager ) {
            this.localPlayerService = localPlayerService;
            this.validator = validator;
            this.combinationEvaluator = combinationEvaluator;
            this.turnManager = turnManager;
        }

        public void Start() {
            actionPanel.SetActive(false);

            playBtn.onClick.AddListener(OnPlayBtnClick);
            sortBtn.onClick.AddListener(OnSortBtnClick);
            passBtn.onClick.AddListener(OnPassBtnClick);
        }

        public void OnEnable() {
            turnManager.OnTurnChanged += HandleTurnChanged;
            localPlayerService.OnLocalPlayerSet += HandleLocalPlayerSet;
            

            if ( localPlayerService.Player != null ) {
                HandleLocalPlayerSet(localPlayerService.Player);
            }
        }

        private void HandleLocalPlayerSet( TienLenPlayer player ) {
            localPlayer = player;

            localPlayer.CardHolder.OnCardSelected += HandleCardSelected;
        }

        private void HandleTurnChanged() {

            // Check if it's the local player's turn
            bool enable;
            if ( turnManager.CurrentPlayer == localPlayer ) enable = true;
            else enable = false;

            ChangePlayButtonState(enable);
            ChangePassButtonState(enable);


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
                Debug.LogWarning("No cards selected to play.");
                return;
            }

            List<Card> cards = selectedCards
                            .Select(view => view.Card)
                            .ToList();

            // 1. Evaluate selected cards.
            if ( !combinationEvaluator.TryEvaluate(
                    cards,
                    out CardCombination combination) ) {
                Debug.Log("Selected cards do not form a valid combination.");
                return;
            }

            // 2. Ask TurnManager if this player can play it.
            if ( !turnManager.TryPlay(localPlayer, combination) ) {
                Debug.Log("This combination cannot be played.");
                return;
            }

            // 3. Play succeeded.
            Debug.Log($"Played {combination.Type}");

            localPlayer.CardHolder.RemoveCards(selectedCards);


        }

        void OnSortBtnClick() {

        }

        void OnPassBtnClick() {
            turnManager.TryPass(localPlayer);
        }


        void ChangePlayButtonState( bool enable ) {
            playBtn.interactable = enable;
        }

        void ChangePassButtonState( bool enable ) {
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