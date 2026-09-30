using System.Collections.Generic;

namespace Assets.Script.TienLen.Rule {
    public class CardComparer : IComparer<Card> {
        public int GetSuitValue( CardSuit suit ) {
            return suit switch {
                CardSuit.Spades => 0,
                CardSuit.Clubs => 1,
                CardSuit.Diamonds => 2,
                CardSuit.Hearts => 3,
                _ => 0
            };
        }

        public int Compare( Card a, Card b ) {
            if ( ReferenceEquals(a, b) ) return 0;
            if ( a is null ) return -1;
            if ( b is null ) return 1;

            if ( a.Rank != b.Rank ) {
                return a.Rank.CompareTo(b.Rank);
            }

            return GetSuitValue(a.Suit).CompareTo(GetSuitValue(b.Suit));
        }

        public bool GreaterThan( Card a, Card b ) {
            return Compare(a, b) > 0;
        }
    }
}
