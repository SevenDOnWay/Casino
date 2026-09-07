using Assets.Script.TienLen.Rule;
using DG.Tweening;
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

        private readonly List<CardView> cards = new();

        public IReadOnlyList<CardView> Cards => cards;

        [Inject]
        public void Construct( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
            Debug.Log($"[CardHolder] Injected CardComparer successfully on {gameObject.name}", this);
        }

        public void AddCard( CardView cardView, bool animate = true ) {
            if ( cardView == null ) {
                Debug.LogWarning("[CardHolder] AddCard called with null CardView!", this);
                return;
            }

            if ( cards.Contains(cardView) )
                return;

            // 1. CRITICAL: Parent the card to this CardHolder keeping its current world position
            cardView.transform.SetParent(this.transform, worldPositionStays: true);

            cards.Add(cardView);

            // 2. Trigger arrangement so the dealt card flies into its hand slot
            ArrangeCards(animate);
        }

        public void RemoveCard( CardView cardView, bool animate = true ) {
            if ( cardView == null ) return;

            if ( !cards.Remove(cardView) ) return;

            ArrangeCards(animate);
        }

        public void Clear() {
            cards.Clear();
        }

        public void SortCards() {
            if ( cardComparer == null ) {
                Debug.LogError("[CardHolder] Cannot sort cards: 'cardComparer' is NULL! Falling back to raw Card comparison.", this);
                cards.Sort(( a, b ) => a.Card != null && b.Card != null ? a.Card.Rank.CompareTo(b.Card.Rank) : 0);
                return;
            }

            cards.Sort(( a, b ) => cardComparer.Compare(a.Card, b.Card));
        }

        public void ArrangeCards( bool animate = true ) {
            if ( cards.Count == 0 ) return;

            float spacing = CalculateSpacing();
            float totalWidth = spacing * (cards.Count - 1);
            float startX = -totalWidth / 2f;

            for ( int i = 0; i < cards.Count; i++ ) {
                CardView card = cards[i];
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
            if ( cards.Count <= 1 )
                return cardWidth;

            float availableWidth = maxWidth - cardWidth;
            float spacing = availableWidth / (cards.Count - 1);

            return Mathf.Max(spacing, minSpacing);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            float totalBoxWidth = maxWidth;
            float estimatedHeight = cardWidth * 1.4f;

            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(totalBoxWidth, estimatedHeight, 0.05f));

            int count = (cards != null && cards.Count > 0) ? cards.Count : 13;
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