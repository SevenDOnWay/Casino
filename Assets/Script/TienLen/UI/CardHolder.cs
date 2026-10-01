using Assets.Script.TienLen.Rule;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class CardHolder : MonoBehaviour {
        [Header("Dependencies")]
        private CardComparer cardComparer;
        private static readonly CardComparer DefaultComparer = new();

        [Header("Layout")]
        [SerializeField] private float maxWidth = 8f;
        [SerializeField] private float cardWidth = 1f;
        [SerializeField] private float minSpacing = 0.2f;
        [SerializeField] private float maxSpacing = 0.65f;

        [Header("Animation")]
        [SerializeField] private float moveDuration = 0.25f;
        [SerializeField] private Ease moveEase = Ease.OutQuad;

        private readonly List<CardView> cardsViews = new();
        private readonly List<CardView> selectedCards = new();
        private bool interactable = true;

        public event Action<IReadOnlyList<CardView>> OnCardSelected;

        public IReadOnlyList<CardView> CardViews => cardsViews;
        public IReadOnlyList<CardView> SelectedCards => selectedCards;

        [Inject]
        public void Construct( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
        }

        public void AddCard( CardView cardView, bool animate = true, bool autoArrange = true ) {
            if ( cardView == null ) return;
            if ( cardsViews.Contains(cardView) ) return;

            cardView.transform.SetParent(transform, worldPositionStays: true);
            cardsViews.Add(cardView);

            cardView.OnClicked += HandleCardClicked;
            cardView.SetInteractable(interactable);

            if ( autoArrange ) {
                ArrangeCards(animate);
            }
        }

        public void RemoveCard( CardView cardView, bool animate = true ) {
            if ( cardView == null ) return;
            if ( !cardsViews.Remove(cardView) ) return;

            cardView.OnClicked -= HandleCardClicked;
            cardView.SetInteractable(false);

            ArrangeCards(animate);
        }

        public void RemoveCards( IEnumerable<CardView> toRemove, bool animate = true ) {
            if ( toRemove == null ) return;
            foreach ( CardView cardView in toRemove ) {
                if ( cardView == null ) continue;
                if ( cardsViews.Remove(cardView) ) {
                    cardView.OnClicked -= HandleCardClicked;
                    cardView.SetInteractable(false);
                }
            }
            ArrangeCards(animate);
        }

        public void Clear() {
            selectedCards.Clear();

            foreach ( CardView card in cardsViews ) {
                if ( card == null ) continue;
                card.OnClicked -= HandleCardClicked;
                Destroy(card.gameObject);
            }

            cardsViews.Clear();
            OnCardSelected?.Invoke(selectedCards);
        }

        public void ClearSelection() {
            foreach ( CardView card in selectedCards ) {
                if ( card != null ) card.SetSelected(false);
            }

            if ( selectedCards.Count == 0 ) return;

            selectedCards.Clear();
            OnCardSelected?.Invoke(selectedCards);
        }

        private void HandleCardClicked( CardView cardView ) {
            if ( selectedCards.Contains(cardView) ) {
                selectedCards.Remove(cardView);
                cardView.SetSelected(false);
            }
            else {
                selectedCards.Add(cardView);
                cardView.SetSelected(true);
            }

            OnCardSelected?.Invoke(selectedCards);
        }

        public enum SortMode {
            ByRank,
            ByGroup
        }

        private SortMode currentSortMode = SortMode.ByRank;

        public void SortCards() {
            SortMode nextMode = (currentSortMode == SortMode.ByRank) ? SortMode.ByGroup : SortMode.ByRank;
            SortCards(nextMode);
        }

        public void SortCards( SortMode mode ) {
            currentSortMode = mode;
            var comparer = cardComparer ?? DefaultComparer;

            if ( mode == SortMode.ByGroup ) {
                var rankCounts = new Dictionary<CardRank, int>();
                foreach ( var view in cardsViews ) {
                    if ( view?.Card == null ) continue;
                    rankCounts[view.Card.Rank] = rankCounts.GetValueOrDefault(view.Card.Rank, 0) + 1;
                }

                cardsViews.Sort(( a, b ) => {
                    Card aCard = a?.Card;
                    Card bCard = b?.Card;
                    if ( aCard == null ) return bCard == null ? 0 : 1;
                    if ( bCard == null ) return -1;

                    int aCount = rankCounts.GetValueOrDefault(aCard.Rank, 0);
                    int bCount = rankCounts.GetValueOrDefault(bCard.Rank, 0);

                    if ( aCount != bCount ) {
                        return bCount.CompareTo(aCount);
                    }

                    return comparer.Compare(aCard, bCard);
                });
            }
            else {
                cardsViews.Sort(( a, b ) => {
                    Card aCard = a?.Card;
                    Card bCard = b?.Card;
                    if ( aCard == null ) return bCard == null ? 0 : 1;
                    if ( bCard == null ) return -1;
                    return comparer.Compare(aCard, bCard);
                });
            }
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
                    card.IsSelected ? 0.7f : 0f,
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

                card.transform.SetSiblingIndex(i);
            }
        }

        private float CalculateSpacing() {
            if ( cardsViews.Count <= 1 ) return cardWidth;

            float availableWidth = maxWidth - cardWidth;
            float spacing = availableWidth / (cardsViews.Count - 1);
            float effectiveMaxSpacing = maxSpacing > 0f ? maxSpacing : 0.65f;

            return Mathf.Clamp(spacing, minSpacing, effectiveMaxSpacing);
        }

        public void SetInteractable( bool value ) {
            interactable = value;
            foreach ( var card in cardsViews ) {
                if ( card != null ) {
                    card.SetInteractable(value);
                }
            }
        }

        public List<CardView> FindCards( List<Card> cards ) {
            List<CardView> result = new();
            if ( cards == null || cards.Count == 0 ) return result;

            Dictionary<Card, CardView> cardToView = new();
            List<CardView> facelessViews = new();

            foreach ( var cardView in cardsViews ) {
                if ( cardView == null ) continue;
                if ( cardView.Card == null ) {
                    facelessViews.Add(cardView);
                    continue;
                }
                cardToView.TryAdd(cardView.Card, cardView);
            }

            foreach ( var card in cards ) {
                if ( card != null && cardToView.TryGetValue(card, out CardView view) ) {
                    result.Add(view);
                }
            }

            // Viewers hold back placeholders: fall back to placeholder views so animation plays
            for ( int i = result.Count; i < cards.Count && facelessViews.Count > 0; i++ ) {
                CardView fallback = facelessViews[0];
                facelessViews.RemoveAt(0);
                if ( !result.Contains(fallback) ) {
                    result.Add(fallback);
                }
            }

            return result;
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
