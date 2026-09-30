using System.Collections.Generic;
using System.Linq;

namespace Assets.Script.TienLen.Rule {
    public class CardCombination {
        public CardCombinationType Type { get; }
        public IReadOnlyList<Card> Cards { get; }
        public CardRank MainRank { get; }
        public CardSuit MainSuit { get; }

        public CardCombination(
            CardCombinationType type,
            IEnumerable<Card> cards,
            IComparer<Card> cardComparer ) {
            Type = type;

            var list = cards.ToList();
            list.Sort(cardComparer);
            Cards = list;

            Card mainCard = Cards[^1];
            MainRank = mainCard.Rank;
            MainSuit = mainCard.Suit;
        }

        public bool ContainsTwo() {
            return Cards.Any(c => c.Rank == CardRank.Two);
        }

        public int PairCount => Type == CardCombinationType.PairSequence ? Cards.Count / 2 : 0;
    }
}
