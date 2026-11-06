using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Rule {
    public class TienLenRuleValidator {
        [Header("Dependencies")]
        private CardComparer cardComparer;

        public CardCombination CurrentCombination { get; private set; }

        [Inject]
        public void Construct( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
        }

        public void Reset() {
            CurrentCombination = null;
        }

        public void SetCurrentCombination( CardCombination combination ) {
            CurrentCombination = combination;
        }

        public bool CanPlay( CardCombination next ) {
            if ( next == null ) return false;

            // Nothing has been played yet -> any valid combination is allowed.
            if ( CurrentCombination == null ) return true;

            // If same type, compare standard Tiến Lên ranking.
            if ( CurrentCombination.Type == next.Type ) {
                if ( CurrentCombination.Type == CardCombinationType.Straight ) {
                    if ( CurrentCombination.Cards.Count != next.Cards.Count ) {
                        return false;
                    }
                }
                else if ( CurrentCombination.Type == CardCombinationType.PairSequence ) {
                    if ( next.PairCount > CurrentCombination.PairCount ) {
                        return true; // e.g. 4 đôi thông cuts 3 đôi thông
                    }
                    if ( next.PairCount < CurrentCombination.PairCount ) {
                        return false;
                    }
                }

                return CompareSameType(CurrentCombination, next);
            }

            // Different combination types -> check cutting rules (chặt heo / chặt hàng).
            return CanCut(CurrentCombination, next);
        }

        private bool CompareSameType( CardCombination previous, CardCombination next ) {
            if ( previous.MainRank != next.MainRank ) {
                return previous.MainRank.CompareTo(next.MainRank) < 0;
            }

            return cardComparer.GetSuitValue(previous.MainSuit)
                < cardComparer.GetSuitValue(next.MainSuit);
        }

        private bool CanCut( CardCombination previous, CardCombination next ) {
            // 1. Single 2 can be cut by:
            //    - Four of a Kind (Tứ quý)
            //    - 3+ Pair Sequence (3 đôi thông, 4 đôi thông)
            if ( previous.Type == CardCombinationType.Single && previous.ContainsTwo() ) {
                if ( next.Type == CardCombinationType.FourOfAKind ) return true;
                if ( next.Type == CardCombinationType.PairSequence && next.PairCount >= 3 ) return true;
            }

            // 2. Pair of 2s can be cut by:
            //    - Four of a Kind (Tứ quý)
            //    - 4 Pair Sequence (4 đôi thông)
            if ( previous.Type == CardCombinationType.Pair && previous.ContainsTwo() ) {
                if ( next.Type == CardCombinationType.FourOfAKind ) return true;
                if ( next.Type == CardCombinationType.PairSequence && next.PairCount >= 4 ) return true;
            }

            // 3. 3 Pair Sequence (3 đôi thông) can be cut by:
            //    - Four of a Kind (Tứ quý)
            //    - 4+ Pair Sequence
            if ( previous.Type == CardCombinationType.PairSequence && previous.PairCount == 3 ) {
                if ( next.Type == CardCombinationType.FourOfAKind ) return true;
                if ( next.Type == CardCombinationType.PairSequence && next.PairCount >= 4 ) return true;
            }

            // 4. Four of a Kind can be cut by:
            //    - 4 Pair Sequence (4 đôi thông)
            if ( previous.Type == CardCombinationType.FourOfAKind ) {
                if ( next.Type == CardCombinationType.PairSequence && next.PairCount >= 4 ) return true;
            }

            return false;
        }
    }
}
