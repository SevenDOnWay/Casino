using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Script.TienLen.Game.Visual {
    public class TienLenTableAnimator : MonoBehaviour {
        [SerializeField] private CardHolder tableCenterPosition;

        public CardHolder TableCenterPosition {
            get {
                if ( tableCenterPosition == null ) {
                    var holders = FindObjectsByType<CardHolder>(FindObjectsSortMode.None);
                    foreach ( var holder in holders ) {
                        if ( holder != null && holder.name.Equals("Center", System.StringComparison.OrdinalIgnoreCase) ) {
                            tableCenterPosition = holder;
                            break;
                        }
                    }
                }
                return tableCenterPosition;
            }
            set => tableCenterPosition = value;
        }

        public void ClearTableCenter() {
            CardHolder center = TableCenterPosition;
            if ( center != null ) {
                center.Clear();
            }
        }

        public void AnimateCards(
            CardHolder hand,
            List<Card> playedCards,
            CardSpawner cardSpawner,
            TienLenSO tienLenSO,
            Dictionary<(CardSuit, CardRank), Sprite> sprites = null
        ) {
            CardHolder center = TableCenterPosition;
            if ( center == null ) {
                Debug.LogError("[TienLenTableAnimator] Table center position is not assigned.");
                return;
            }

            StartCoroutine(AnimateCardsToTableCoroutine(hand, center, playedCards, cardSpawner, tienLenSO, sprites));
        }

        private IEnumerator AnimateCardsToTableCoroutine(
            CardHolder hand,
            CardHolder table,
            List<Card> playedCards,
            CardSpawner cardSpawner,
            TienLenSO tienLenSO,
            Dictionary<(CardSuit, CardRank), Sprite> sprites
        ) {
            float duration = 0.35f;
            float cardSpacing = 0.45f;
            float totalWidth = (playedCards.Count - 1) * cardSpacing;
            float startX = -totalWidth / 2f;

            Vector3 handRandomOffset = new Vector3(
                Random.Range(-0.15f, 0.15f),
                Random.Range(-0.15f, 0.15f),
                0
            );

            float handBaseAngle = Random.Range(-6f, 6f);

            List<CardView> cardsToAnimate = new List<CardView>();

            for ( int i = 0; i < playedCards.Count; i++ ) {
                Card played = playedCards[i];
                CardView cardView = null;

                if ( hand != null && hand.CardViews.Count > 0 ) {
                    // Try exact match first
                    cardView = hand.CardViews.FirstOrDefault(v => v != null && v.Card != null && v.Card.Equals(played) && !cardsToAnimate.Contains(v));
                    // Fallback to any available card view in hand (e.g. face-down opponent card)
                    if ( cardView == null ) {
                        cardView = hand.CardViews.FirstOrDefault(v => v != null && !cardsToAnimate.Contains(v));
                    }
                }

                if ( cardView == null && cardSpawner != null ) {
                    Sprite cardSprite = null;
                    if ( sprites != null && sprites.TryGetValue((played.Suit, played.Rank), out Sprite sp) && sp != null ) {
                        cardSprite = sp;
                    }
                    else if ( tienLenSO != null ) {
                        cardSprite = tienLenSO.GetCardSprite(played.Suit, played.Rank);
                    }

                    cardView = cardSpawner.SpawnCard(played, cardSprite);
                    cardView.transform.position = table.transform.position + Vector3.up * 2f;
                }

                if ( cardView != null ) {
                    cardsToAnimate.Add(cardView);
                }
            }

            if ( hand != null && cardsToAnimate.Count > 0 ) {
                var handCards = cardsToAnimate.Where(c => hand.CardViews.Contains(c)).ToList();
                if ( handCards.Count > 0 ) {
                    hand.RemoveCards(handCards, animate: true);
                }
            }

            for ( int i = 0; i < cardsToAnimate.Count; i++ ) {
                CardView view = cardsToAnimate[i];
                if ( view == null ) continue;

                if ( i < playedCards.Count && playedCards[i] != null ) {
                    Card played = playedCards[i];
                    view.Card = played;

                    Sprite faceSprite = null;
                    if ( sprites != null && sprites.TryGetValue((played.Suit, played.Rank), out Sprite sp) && sp != null ) {
                        faceSprite = sp;
                    }
                    else if ( tienLenSO != null ) {
                        faceSprite = tienLenSO.GetCardSprite(played.Suit, played.Rank);
                    }

                    if ( faceSprite != null ) {
                        view.ChangeSprite(faceSprite);
                    }
                }

                view.SetSortingOrder(10 + i);

                Transform cardTransform = view.transform;
                cardTransform.SetParent(table.transform, worldPositionStays: true);

                float cardX = startX + i * cardSpacing;
                float cardYJitter = Random.Range(-0.03f, 0.03f);
                float cardZOffset = -i * 0.01f;

                Vector3 targetLocalPos = handRandomOffset + new Vector3(cardX, cardYJitter, cardZOffset);
                float randomAngle = handBaseAngle + Random.Range(-2f, 2f);

                cardTransform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(cardTransform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad));
                seq.Join(cardTransform.DOLocalRotate(new Vector3(0, 0, randomAngle), duration));
                seq.Join(cardTransform.DOScale(Vector3.one * 0.9f, duration));

                table.AddCard(view, autoArrange: false);
            }

            yield return null;
        }
    }
}
