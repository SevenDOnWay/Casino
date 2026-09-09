using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Script.TienLen.CardFolder {
    class CardSpawner : MonoBehaviour {



        public List<CardView> SpawnAllCard( Dictionary<(CardSuit, CardRank), Sprite> spriteLookup ) {

            List<CardView> cardViews = new List<CardView>();

            foreach ( var (card, cardSprite) in spriteLookup ) {
                GameObject cardObject = new GameObject($"Card_{card.Item1}_{card.Item2}");
                cardObject.AddComponent<SpriteRenderer>().sprite = cardSprite;
                cardObject.AddComponent<BoxCollider2D>();

                var cardView = cardObject.AddComponent<CardView>();
                cardView.Init(new Card(card.Item2, card.Item1));

                cardViews.Add(cardView);
            }

            return cardViews;
        }


        //public CardView SpawnCard( ) {
        //Transform spawnParent = parent != null ? parent : deckAnchor;
        //CardView view = Instantiate(cardPrefab, spawnParent);

        //Sprite sprite = spriteAtlas.GetCardSprite(cardData);
        //view.Initialize(cardData, sprite);

        //viewLookup[cardData] = view;
        //return view;
        //}

        //public CardView GetView( Card card ) => viewLookup.GetValueOrDefault(card);



    }
}
