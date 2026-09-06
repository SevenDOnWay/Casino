using Assets.Script.TienLen.Rule;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Player {
    public class PlayerHand {

        [Inject] CardComparer cardComparer;




        private List<CardView> cardViews;

        public List<CardView> CardViews => cardViews;

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

        public void Sort() {
            //cardViews.Sort(cardComparer.Compare);
        }

        public void Clear() {
            cardViews.Clear();
        }



    }
}