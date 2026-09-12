using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using DG.Tweening;
using Fusion;
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
        private TienLenGame game;

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
        void Construct( LocalPlayerService localPlayerService,
            TienLenRuleValidator validator,
            CardCombinationEvaluator combinationEvaluator,
            TurnManager turnManager,
            TienLenGame game) {
            this.localPlayerService = localPlayerService;
            this.validator = validator;
            this.combinationEvaluator = combinationEvaluator;
            this.turnManager = turnManager;
            this.game = game;
        }

        public void Start() {
            actionPanel.SetActive(false);

            playBtn.onClick.AddListener(OnPlayBtnClick);
            sortBtn.onClick.AddListener(OnSortBtnClick);
            passBtn.onClick.AddListener(OnPassBtnClick);
        }

        public void OnEnable() {
            game.OnRoundStarted += HandleStartRound;
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

        private void HandleStartRound() {
            actionPanel.SetActive(true);

            currentCombination = null;
            ChangePlayButtonState(true);
            ChangePassButtonState(true);
        }

        private void HandleTurnChanged() {

            // Check if it's the local player's turn


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

            NetworkCard[] networkCards = combination.Cards
                .Select(card => new NetworkCard { Rank = (byte)card.Rank, Suit = (byte)card.Suit })
                .ToArray();

            localPlayer.RPCRequestPlayCard(networkCards);


            // 3. Play succeeded.
            Debug.Log($"Played {combination.Type}");

            var cardsToAnimate = new List<CardView>(selectedCards);
            localPlayer.CardHolder.RemoveCards(selectedCards);

            // Center animation settings
            float duration = 0.35f;
            float cardSpacing = 40f; // Pixel offset between cards if placed side-by-side
            Vector3 centerPos = Vector3.zero;

            for ( int i = 0; i < cardsToAnimate.Count; i++ ) {
                var cardView = cardsToAnimate[i];
                Transform cardTransform = cardView.transform;

                // 1. Detach from the hand layout group so it doesn't fight the layout system
                cardTransform.SetParent(cardTransform.root, worldPositionStays: true);

                // 2. Calculate horizontal offset so cards don't overlap completely in the center
                float offset = (i - (cardsToAnimate.Count - 1) / 2f) * cardSpacing;
                Vector3 targetPos = centerPos + new Vector3(offset, 0f, 0f);

                // 3. Optional: add a slight random rotation for a natural "dropped onto table" feel
                float randomAngle = UnityEngine.Random.Range(-5f, 5f);

                // 4. Kill any active hand tweens and animate to center
                cardTransform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(cardTransform.DOMove(targetPos, duration).SetEase(Ease.OutQuad));
                seq.Join(cardTransform.DORotate(new Vector3(0, 0, randomAngle), duration));
                seq.Join(cardTransform.DOScale(Vector3.one * 0.9f, duration)); // Slightly scale down to table size

                // 5. Cleanup or hand off to table discard pile when done
                seq.OnComplete(() =>
                {
                    // Example: cardView.DisableInteractions();
                    // Destroy(cardView.gameObject, 2f); // or transfer to table manager
                });
            }
        }

        void OnSortBtnClick() {

        }

        void OnPassBtnClick() {
            turnManager.TryPass(localPlayer);
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