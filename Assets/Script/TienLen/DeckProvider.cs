using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Script.TienLen {
    public class DeckProvider{
        private Deck deck = new Deck(); //use inject
      

        
        private List<CardView> cards;


        //not start
        void Start() {
            cards = deck.CardViews.ToList();
        }

        
        void DealCard() {

        }




    }
}