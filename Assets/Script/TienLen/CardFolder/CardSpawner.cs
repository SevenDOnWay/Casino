using Assets.Script.TienLen.Game;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.TienLen.CardFolder {
    public class CardSpawner : MonoBehaviour {
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

        public CardView SpawnCard( Card card, Sprite sprite ) {
            GameObject cardObject = new GameObject($"Card_{card.Rank}_{card.Suit}");
            cardObject.AddComponent<SpriteRenderer>().sprite = sprite;
            cardObject.AddComponent<BoxCollider2D>();

            var cardView = cardObject.AddComponent<CardView>();
            cardView.Init(new Card(card.Rank, card.Suit));

            return cardView;
        }

        public CardView SpawnCardBack( Sprite sprite ) {
            GameObject cardObject = new GameObject("Card_Back");
            cardObject.AddComponent<SpriteRenderer>().sprite = sprite;
            cardObject.AddComponent<BoxCollider2D>();

            return cardObject.AddComponent<CardView>();
        }
    }
}
