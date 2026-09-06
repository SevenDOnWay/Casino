using System.Collections;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Rule {
    public class TienLenRule {

        CardComparer cardComparer;

        [Inject]
        void Construct( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
        }

        public bool CanPlay( CardCombination previous, CardCombination next ) {
            if ( next == null ) return false;

            // No previous combination.
            if ( previous == null ) return true;

            // Special cuts.
            //if ( CanCut(previous, next) ) return true;

            // Different combination types normally cannot beat each other.
            if ( previous.Type != next.Type ) return false;

            // Same type -> compare.
            return CompareSameType(previous, next);
        }

        private bool CompareSameType( CardCombination a, CardCombination b ) {
            if ( a.MainRank != b.MainRank ) {
                return a.MainRank.CompareTo(b.MainRank) < 0;
            }

            return a.MainSuit.CompareTo(b.MainSuit) < 0;
        }


        //skip for now

        //private bool CanCut( CardCombination previous, CardCombination next ) {
        //    // Four of a kind can cut a single 2.
        //    if ( previous.Type == CardCombinationType.Single && previous.ContainsTwo() ) {
        //        return next.Type == CardCombinationType.FourOfAKind;
        //    }

        //    return false;
        //}

    }
}