using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.Rule {
    public class CardComparer {


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
            if ( a.Rank != b.Rank )
                return a.Rank.CompareTo(b.Rank);

            return GetSuitValue(a.Suit)
                .CompareTo(GetSuitValue(b.Suit));
        }


        
        public bool GreaterThan( Card a, Card b ) {
            return Compare(a, b) > 0;
        }


    }
}