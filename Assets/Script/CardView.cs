using System.Collections;
using UnityEngine;

namespace Assets.Script {
    public class CardView : MonoBehaviour {
        Sprite sprite;


        Card card;

        public Card Card { get => card; set => card = value; }

        private void Awake() {
            sprite = GetComponent<Sprite>();
        }

        public void Init(Card card ) {
            this.card = card;
        }


        //change sprite of the card
        public void ChangeSprite(Sprite sprite) {
            this.sprite = sprite;
        }

        

    }
}