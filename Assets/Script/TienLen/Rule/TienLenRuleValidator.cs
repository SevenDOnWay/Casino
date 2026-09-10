using System.Collections;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Rule {
    public class TienLenRuleValidator {
        [Header("Dependencies")]
        CardComparer cardComparer;

        public CardCombination CurrentCombination { get; private set; }

        [Inject]
        void Construct( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
        }

        public void Reset() {
            CurrentCombination = null;
        }

        public void SetCurrentCombination( CardCombination combination ) {
            CurrentCombination = combination;
        }


        public bool CanPlay( CardCombination next ) {
            if ( next == null )
                return false;

            // Nothing has been played yet.
            if ( CurrentCombination == null )
                return true;

            // Different combination types normally cannot beat each other.
            if ( CurrentCombination.Type != next.Type )
                return false;

            return CompareSameType(CurrentCombination, next);
        }


        private bool CompareSameType(
       CardCombination previous,
       CardCombination next ) {
            if ( previous.MainRank != next.MainRank ) {
                return previous.MainRank.CompareTo(next.MainRank) < 0;
            }

            return previous.MainSuit.CompareTo(next.MainSuit) < 0;
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