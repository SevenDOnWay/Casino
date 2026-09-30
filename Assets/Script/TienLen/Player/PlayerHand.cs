using System.Collections.Generic;

namespace Assets.Script.TienLen.Player {
    public class PlayerHand {
        private readonly List<Card> cards = new();

        public IReadOnlyList<Card> Cards => cards;
        public int Count => cards.Count;

        public void AddCard( Card card ) {
            if ( card != null ) {
                cards.Add(card);
            }
        }

        public void AddCard( IEnumerable<Card> cardList ) {
            if ( cardList != null ) {
                cards.AddRange(cardList);
            }
        }

        public void Remove( Card card ) {
            if ( card != null ) {
                cards.Remove(card);
            }
        }

        public void Remove( IEnumerable<Card> selectedCards ) {
            if ( selectedCards == null ) return;
            foreach ( Card card in selectedCards ) {
                if ( card != null ) {
                    cards.Remove(card);
                }
            }
        }

        public void Clear() {
            cards.Clear();
        }
    }
}
