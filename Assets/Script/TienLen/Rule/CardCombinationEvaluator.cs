using System.Collections.Generic;
using System.Linq;
using VContainer;

namespace Assets.Script.TienLen.Rule {
    public class CardCombinationEvaluator {
        private readonly CardComparer cardComparer;

        [Inject]
        public CardCombinationEvaluator( CardComparer cardComparer ) {
            this.cardComparer = cardComparer;
        }

        public bool TryEvaluate( IEnumerable<Card> cards, out CardCombination combination ) {
            combination = null;

            if ( cards == null ) return false;

            List<Card> cardList = cards.ToList();
            if ( cardList.Count == 0 ) return false;

            CardCombinationType type = EvaluateType(cardList);
            if ( type == CardCombinationType.Invalid ) return false;

            combination = new CardCombination(type, cardList, cardComparer);
            return true;
        }

        private CardCombinationType EvaluateType( IReadOnlyList<Card> cards ) {
            return cards.Count switch {
                1 => CardCombinationType.Single,
                2 when IsPair(cards) => CardCombinationType.Pair,
                3 when IsTriple(cards) => CardCombinationType.Triple,
                4 when IsFourOfKind(cards) => CardCombinationType.FourOfAKind,
                _ when IsStraight(cards) => CardCombinationType.Straight,
                _ when IsPairSequence(cards) => CardCombinationType.PairSequence,
                _ => CardCombinationType.Invalid
            };
        }

        private bool IsPair( IReadOnlyList<Card> cards ) {
            return cards[0].Rank == cards[1].Rank;
        }

        private bool IsTriple( IReadOnlyList<Card> cards ) {
            return cards[0].Rank == cards[1].Rank && cards[1].Rank == cards[2].Rank;
        }

        private bool IsFourOfKind( IReadOnlyList<Card> cards ) {
            CardRank rank = cards[0].Rank;
            return cards[1].Rank == rank && cards[2].Rank == rank && cards[3].Rank == rank;
        }

        private bool IsStraight( IReadOnlyList<Card> cards ) {
            if ( cards.Count < 3 ) return false;

            // In Tiến Lên, Rank.Two cannot be part of a straight.
            for ( int i = 0; i < cards.Count; i++ ) {
                if ( cards[i].Rank == CardRank.Two ) return false;
            }

            List<Card> sorted = cards.OrderBy(c => c.Rank).ToList();

            for ( int i = 1; i < sorted.Count; i++ ) {
                int previous = (int)sorted[i - 1].Rank;
                int current = (int)sorted[i].Rank;

                if ( current != previous + 1 ) {
                    return false;
                }
            }

            return true;
        }

        private bool IsPairSequence( IReadOnlyList<Card> cards ) {
            if ( cards.Count < 6 || cards.Count % 2 != 0 ) return false;

            // In Tiến Lên, Rank.Two cannot be part of a pair sequence (đôi thông).
            for ( int i = 0; i < cards.Count; i++ ) {
                if ( cards[i].Rank == CardRank.Two ) return false;
            }

            var groups = cards
                .GroupBy(c => c.Rank)
                .OrderBy(g => g.Key)
                .ToList();

            // Every rank in the sequence must appear exactly twice.
            if ( groups.Any(g => g.Count() != 2) ) return false;

            // Ranks must be consecutive.
            for ( int i = 1; i < groups.Count; i++ ) {
                int previous = (int)groups[i - 1].Key;
                int current = (int)groups[i].Key;

                if ( current != previous + 1 ) return false;
            }

            return true;
        }
    }
}
