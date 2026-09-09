using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Assets.Script {
    public class Deck {

        private readonly List<Card> cards = new();
        private readonly List<CardView> cardViews = new();

        public IReadOnlyList<Card> Cards => cards;
        public IReadOnlyList<CardView> CardViews => cardViews;


        public int Count => cards.Count;


        private void CreateDeck( CardSpriteAtlas cardSpriteAtlas ) {
            cards.Clear();

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
                }
            }

            if ( cardViews.Count == 0 ) {
                throw new InvalidOperationException("No card views were created. Check if the CardSpriteAtlas is assigned and contains sprites.");
            }

            foreach ( var cardView in cardViews ) {
                // Do something with each card view if needed
                Debug.Log(cardView.name);
            }

        }


        public void Shuffle() {
            int time =  RandomNumberGenerator.GetInt32(3,10);

            for ( int i = 0; i < time; i++ ) {
                int n = cardViews.Count;
                while ( n > 1 ) {
                    n--;
                    int k = RandomNumberGenerator.GetInt32(n + 1);
                    (cardViews[k], cardViews[n]) = (cardViews[n], cardViews[k]);
                }
            }
        }

        public CardView Draw() {
            if ( cardViews.Count == 0 )
                throw new InvalidOperationException("Deck is empty.");

            CardView cardView = cardViews[^1];

            cardViews.RemoveAt(cardViews.Count - 1);

            return cardView;
        }

        public List<CardView> Draw( int amount ) {
            if ( amount > cardViews.Count )
                throw new InvalidOperationException(
                    $"Cannot draw {amount} cards. Only {cardViews.Count} remaining."
                );

            List<CardView> result = new();

            for ( int i = 0; i < amount; i++ ) {
                result.Add(Draw());
            }

            return result;
        }


        public void SetCardView( List<CardView> cardViews ) {
            this.cardViews.Clear();
            this.cardViews.AddRange(cardViews);
        }

    }
}