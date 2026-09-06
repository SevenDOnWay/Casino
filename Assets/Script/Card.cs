using System.Collections;
using UnityEngine;

namespace Assets.Script {
    public class Card {

        private CardRank rank;
        private CardSuit suit;

        public CardRank Rank { get => rank; set => rank = value; }
        public CardSuit Suit { get => suit; set => suit = value; }

        public Card( CardRank rank, CardSuit suit ) {
            this.rank = rank;
            this.suit = suit;
        }
    }
}