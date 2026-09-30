using System;

namespace Assets.Script {
    public class Card : IEquatable<Card> {
        private CardRank rank;
        private CardSuit suit;

        public CardRank Rank {
            get => rank;
            set => rank = value;
        }

        public CardSuit Suit {
            get => suit;
            set => suit = value;
        }

        public Card( CardRank rank, CardSuit suit ) {
            this.rank = rank;
            this.suit = suit;
        }

        public bool Equals( Card other ) {
            if ( other is null ) return false;
            return this.rank == other.rank && this.suit == other.suit;
        }

        public override bool Equals( object obj ) {
            return obj is Card other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(rank, suit);
        }

        public override string ToString() {
            return $"{rank} of {suit}";
        }
    }
}
