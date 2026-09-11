using Assets.Script.TienLen.Rule;
using System.Collections.Generic;
using VContainer;

namespace Assets.Script.TienLen.Player {
    public class PlayerHand {


        private List<CardView> cardViews = new();

        public List<CardView> CardViews => cardViews; //TODO: Change this to list<card> 

        public int Count => cardViews.Count;




        public void AddCard( CardView cardView ) {
            cardViews.Add(cardView);
        }

        public void AddCard( List<CardView> cardViews ) {
            this.cardViews.AddRange(cardViews);
        }

        public void Remove( CardView card ) {
            cardViews.Remove(card);
        }

        public void Remove( IEnumerable<CardView> selectedCards ) {
            foreach ( CardView card in selectedCards )
                cardViews.Remove(card);
        }

        public void Clear() {
            cardViews.Clear();
        }



    }
}