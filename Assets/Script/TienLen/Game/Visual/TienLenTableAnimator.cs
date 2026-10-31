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
            get => tableCenterPosition;
            set => tableCenterPosition = value;
        }

        public void ClearTableCenter() {
            if ( tableCenterPosition != null ) {
                tableCenterPosition.Clear();
            }
        }

        public void AnimateCards(
            CardHolder hand,
            List<Card> playedCards,
            CardSpawner cardSpawner,
            TienLenSO tienLenSO,
            Dictionary<(CardSuit, CardRank), Sprite> sprites = null
        ) {
            if ( tableCenterPosition == null ) {
                Debug.LogError("[TienLenTableAnimator] Table center position is not assigned.");
                return;
            }

            StartCoroutine(AnimateCardsToTableCoroutine(hand, tableCenterPosition, playedCards, cardSpawner, tienLenSO, sprites));
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

            if ( hand != null && hand.CardViews.Count > 0 ) {
                for ( int i = 0; i < playedCards.Count; i++ ) {
                    CardView matchedView = null;
                    Card played = playedCards[i];

                    foreach ( var view in hand.CardViews ) {
                        if ( view != null && view.Card != null && view.Card.Equals(played) ) {
                            matchedView = view;
                            break;
                        }
                    }

                    if ( matchedView == null ) {
                        matchedView = hand.CardViews.FirstOrDefault(v => v != null && !cardsToAnimate.Contains(v));
                    }

                    if ( matchedView != null ) {
                        cardsToAnimate.Add(matchedView);
                    }
                }

                hand.RemoveCards(cardsToAnimate, animate: true);
            }
            else if ( cardSpawner != null ) {
                for ( int i = 0; i < playedCards.Count; i++ ) {
                    Card played = playedCards[i];
                    Sprite cardSprite = null;
                    if ( sprites != null && sprites.TryGetValue((played.Suit, played.Rank), out Sprite sp) ) {
                        cardSprite = sp;
                    }
                    else if ( tienLenSO != null ) {
                        cardSprite = tienLenSO.GetCardSprite(played.Suit, played.Rank);
                    }

                    CardView spawnedView = cardSpawner.SpawnCard(played, cardSprite);
                    spawnedView.transform.position = table.transform.position + Vector3.up * 2f;
                    cardsToAnimate.Add(spawnedView);
                }
            }

            for ( int i = 0; i < cardsToAnimate.Count; i++ ) {
                CardView view = cardsToAnimate[i];
                if ( view == null ) continue;

                if ( i < playedCards.Count && playedCards[i] != null ) {
                    Card played = playedCards[i];
                    view.Card = played;
                    if ( sprites != null && sprites.TryGetValue((played.Suit, played.Rank), out Sprite faceSprite)
                        && faceSprite != null ) {
                        view.ChangeSprite(faceSprite);
                    }
                }

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
