using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace Assets.Script {
    public class Card : IEquatable<Card>{

        private CardRank rank;
        private CardSuit suit;

        public CardRank Rank { get => rank; set => rank = value; }
        public CardSuit Suit { get => suit; set => suit = value; }

        public Card( CardRank rank, CardSuit suit ) {
            this.rank = rank;
            this.suit = suit;
        }

        public bool Equals( Card other ) {
            if( other == null ) return false;
            return this.rank == other.rank && this.suit == other.suit;  
        }

        // Required for Dictionary/HashSet lookups
        public override bool Equals( object obj ) {
            return obj is Card other && Equals(other);
        }

        // Must return the exact same hash for identical rank & suit
        public override int GetHashCode() {
            return HashCode.Combine(rank, suit);
        }
    }
}