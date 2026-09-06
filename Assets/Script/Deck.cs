using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script {
    public class Deck : MonoBehaviour {

        private readonly List<Card> cards = new();
        private readonly List<CardView> cardViews = new();

        public IReadOnlyList<Card> Cards => cards;
        public IReadOnlyList<CardView> CardViews => cardViews;

        [SerializeField] private CardSpriteAtlas cardSpriteAtlas;

        public int Count => cards.Count;


        void Start() {
            CreateDeck();
        }

        private void CreateDeck() {
            cards.Clear();

            Debug.Break();

            CardSuit[] suits =
            {
                CardSuit.Clubs,
                CardSuit.Diamonds,
                CardSuit.Spades,
                CardSuit.Hearts
            };

            foreach ( CardSuit suit in suits ) {
                for ( int rank = 3; rank <= 15; rank++ ) {
                    cards.Add(
                        new Card((CardRank)rank, suit)
                    );

                    Sprite cardSprite = cardSpriteAtlas.GetCardSprite(cards[^1]);

                    GameObject cardObject = new GameObject($"Card_{suit}_{(CardRank)rank}");
                    cardObject.AddComponent<SpriteRenderer>().sprite = cardSprite;
                    cardObject.AddComponent<CardView>();

                    cardObject.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    cardObject.transform.SetParent(this.gameObject.transform);

                    cardViews.Add(cardObject.GetComponent<CardView>());
                }
            }

            Debug.Break();
        }


        public void Shuffle() {
            System.Random random = new();

            for ( int i = cards.Count - 1; i > 0; i-- ) {
                int j = random.Next(i + 1);

                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        public CardView Draw() {
            if ( cards.Count == 0 )
                throw new InvalidOperationException("Deck is empty.");

            CardView cardView = cardViews[^1];

            cards.RemoveAt(cards.Count - 1);

            return cardView;
        }

        public List<CardView> Draw( int amount ) {
            if ( amount > cards.Count )
                throw new InvalidOperationException(
                    $"Cannot draw {amount} cards. Only {cards.Count} remaining."
                );

            List<CardView> result = new();

            for ( int i = 0; i < amount; i++ ) {
                result.Add(Draw());
            }

            return result;
        }
    }


}