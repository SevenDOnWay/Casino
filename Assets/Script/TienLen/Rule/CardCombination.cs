using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Rule {
    public class CardCombination {

        public CardCombinationType Type { get; }

        public IReadOnlyList<Card> Cards { get; }

        public CardRank MainRank { get; }

        public CardSuit MainSuit { get; }

        [Inject]
        public CardCombination(
        CardComparer cardComparer,
        CardCombinationType type,
        IEnumerable<Card> cards ) {
            Type = type;

            Cards = cards
                .OrderBy(c => c, Comparer<Card>.Create(cardComparer.Compare))
                .ToList();

            Card mainCard = Cards[^1];

            MainRank = mainCard.Rank;
            MainSuit = mainCard.Suit;
        }



    }
}