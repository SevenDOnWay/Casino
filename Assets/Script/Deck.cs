using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Assets.Script {
    public class Deck {

        private readonly List<Card> cards = new();

        public IReadOnlyList<Card> Cards => cards;

        public int Count => cards.Count;


        public void CreateDeck() {
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
        }


        public void Shuffle() {
            int time =  RandomNumberGenerator.GetInt32(3,10);

            for ( int i = 0; i < time; i++ ) {
                int n = cards.Count;
                while ( n > 1 ) {
                    n--;
                    int k = RandomNumberGenerator.GetInt32(n + 1);
                    (cards[k], cards[n]) = (cards[n], cards[k]);
                }
            }
        }

        //public CardView DrawCardView() {
        //    if ( cardViews.Count == 0 )
        //        throw new InvalidOperationException("Deck is empty.");

        //    CardView cardView = cardViews[^1];

        //    cardViews.RemoveAt(cardViews.Count - 1);

        //    return cardView;
        //}

        public Card DrawCard() {
            if ( cards.Count == 0 )
                throw new InvalidOperationException("Deck is empty.");
            Card card = cards[^1];
            cards.RemoveAt(cards.Count - 1);
            return card;
        }

        //public List<CardView> Draw( int amount ) {
        //    if ( amount > cardViews.Count )
        //        throw new InvalidOperationException(
        //            $"Cannot draw {amount} cardsViews. Only {cardViews.Count} remaining."
        //        );

        //    List<CardView> result = new();

        //    for ( int i = 0; i < amount; i++ ) {
        //        result.Add(DrawCardView());
        //    }

        //    return result;
        //}


        //public void SetCardView( List<CardView> cardViews ) {
        //    this.cardViews.Clear();
        //    this.cardViews.AddRange(cardViews);
        //}

    }
}