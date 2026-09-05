using System.Collections;
using UnityEngine;

namespace Assets.Script {
    public class Card {

        public CardRank rank { get; private set; }
        public CardSuit suit { get; private set; }

        public Card( CardRank rank, CardSuit suit ) {
            this.rank = rank;
            this.suit = suit;
        }
    }
}