using Assets.Script.TienLen.Rule;
using System.Collections.Generic;
using VContainer;

namespace Assets.Script.TienLen.Player {
    public class PlayerHand {

        private List<Card> cards = new();

        public List<Card> Cards => cards; //TODO: Change this to list<card> 

        public int Count => cards.Count;


        public void AddCard( Card card ) {
            cards.Add(card);
        }

        public void AddCard( List<Card> cardList ) {
            cards.AddRange(cardList);
        }

        public void Remove( Card card ) {
            cards.Remove(card);
        }

        public void Remove( IEnumerable<Card> selectedCards ) {
            foreach ( Card card in selectedCards )
                cards.Remove(card);
        }

        public void Clear() {
            cards.Clear();
        }



    }
}