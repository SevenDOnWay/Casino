using Assets.Script.TienLen.Rule;
using DG.Tweening;
using FusionIntroShared;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class CardHolder : MonoBehaviour {
        [Header("Dependencies")]
        private CardComparer cardComparer;

        [Header("Layout")]
        [SerializeField] private float maxWidth = 8f;
        [SerializeField] private float cardWidth = 1f;
        [SerializeField] private float minSpacing = 0.2f;

        [Header("Animation")]
        [SerializeField] private float moveDuration = 0.25f;
        [SerializeField] private Ease moveEase = Ease.OutQuad;

        [Header("Avatar")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        //TODO: Change into list of card.
        private readonly List<CardView> cardsViews = new();
        private readonly List<CardView> selectedCards = new();


        //TODO: Implement a way to set the interactable state of the CardHolder and its cards.
        private bool interactable = true;

        public event Action<IReadOnlyList<CardView>> OnCardSelected;




        public IReadOnlyList<CardView> CardViews => cardsViews;
        public IReadOnlyList<CardView> SelectedCards => selectedCards;

        [Inject]
        public void Construct( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
        }

        public void AddCard( CardView cardView, bool animate = true ) {
            if ( cardView == null ) {
                Debug.LogWarning("[CardHolder] AddCard called with null CardView!", this);
                return;
            }

            if ( cardsViews.Contains(cardView) )
                return;

            // 1. CRITICAL: Parent the card to this CardHolder keeping its current world position
            cardView.transform.SetParent(this.transform, worldPositionStays: true);

            cardsViews.Add(cardView);

            cardView.OnClicked += HandleCardClicked;

            cardView.SetInteractable(interactable);

            // 2. Trigger arrangement so the dealt card flies into its hand slot
            ArrangeCards(animate);
        }

        public void RemoveCard( CardView cardView, bool animate = true ) {
            if ( cardView == null ) return;

            if ( !cardsViews.Remove(cardView) ) return;

            cardView.OnClicked -= HandleCardClicked;
            cardView.SetInteractable(false);

            ArrangeCards(animate);
        }

        public void RemoveCards( IEnumerable<CardView> cardViews, bool animate = true ) {
            if ( cardViews == null ) return;
            foreach ( CardView cardView in cardViews ) {
                if ( cardView == null ) continue;
                if ( cardsViews.Remove(cardView) ) {
                    cardView.OnClicked -= HandleCardClicked;
                    cardView.SetInteractable(false);
                }
            }
            ArrangeCards(animate);
        }

        public void Clear() {
            foreach ( CardView card in cardsViews ) {
                if ( card != null )
                    card.OnClicked -= HandleCardClicked;
            }

            cardsViews.Clear();
        }

        private void HandleCardClicked( CardView cardView ) {

            if ( selectedCards.Contains(cardView) ) {
                selectedCards.Remove(cardView);
                cardView.SetSelected(false);
                OnCardSelected?.Invoke(selectedCards);
            }
            else {
                selectedCards.Add(cardView);
                cardView.SetSelected(true);
                OnCardSelected?.Invoke(selectedCards);

            }


            // TODO: Notify listeners about the selection change and validate the selection.

        }

        public void SortCards() {
            if ( cardComparer == null ) {
                Debug.LogError("[CardHolder] Cannot sort cardsViews: 'cardComparer' is NULL! Falling back to raw Card comparison.", this);
                cardsViews.Sort(( a, b ) => a.Card != null && b.Card != null ? a.Card.Rank.CompareTo(b.Card.Rank) : 0);
                return;
            }

            cardsViews.Sort(( a, b ) => cardComparer.Compare(a.Card, b.Card));
        }

        public void ArrangeCards( bool animate = true ) {
            if ( cardsViews.Count == 0 ) return;

            float spacing = CalculateSpacing();
            float totalWidth = spacing * (cardsViews.Count - 1);
            float startX = -totalWidth / 2f;

            for ( int i = 0; i < cardsViews.Count; i++ ) {
                CardView card = cardsViews[i];
                if ( card == null ) continue;

                Vector3 targetPosition = new Vector3(
                    startX + i * spacing,
                    0f,
                    -i * 0.01f
                );

                card.transform.DOKill();

                if ( animate && Application.isPlaying ) {
                    card.transform
                        .DOLocalMove(targetPosition, moveDuration)
                        .SetEase(moveEase);
                }
                else {
                    card.transform.localPosition = targetPosition;
                }

                // Higher index visually on top
                card.transform.SetSiblingIndex(i);
            }
        }

        private float CalculateSpacing() {
            if ( cardsViews.Count <= 1 )
                return cardWidth;

            float availableWidth = maxWidth - cardWidth;
            float spacing = availableWidth / (cardsViews.Count - 1);

            return Mathf.Max(spacing, minSpacing);
        }

        public void SetInteractable( bool interactable ) {
            foreach ( var card in cardsViews ) {
                card.SetInteractable(interactable);
            }
        }

        public List<CardView> FindCards(List<Card> cards) {
            List<CardView> res = new();
            Dictionary<Card, CardView> dic = new();

            foreach(var cardView in cardsViews ) {
                dic.Add(cardView.Card, cardView);
            }

            foreach ( var card in cards ) {
                if ( dic.TryGetValue(card, out CardView cardView) ) {
                    res.Add(cardView);
                }
            }

            return res;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            float totalBoxWidth = maxWidth;
            float estimatedHeight = cardWidth * 1.4f;

            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(totalBoxWidth, estimatedHeight, 0.05f));

            int count = (cardsViews != null && cardsViews.Count > 0) ? cardsViews.Count : 13;
            float spacing = CalculateSpacing();
            float totalWidth = spacing * (count - 1);
            float startX = -totalWidth / 2f;

            for ( int i = 0; i < count; i++ ) {
                Vector3 slotPos = new Vector3(startX + i * spacing, 0f, -i * 0.01f);
                Gizmos.color = (i % 2 == 0) ? new Color(1f, 0.8f, 0.2f, 0.7f) : new Color(1f, 0.4f, 0.2f, 0.7f);
                Gizmos.DrawWireCube(slotPos, new Vector3(cardWidth, estimatedHeight, 0f));
            }

            Gizmos.matrix = previousMatrix;
        }
#endif
    }


}