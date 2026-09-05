using System.Collections;
using UnityEngine;

namespace Assets.Script {
    public class CardView : MonoBehaviour {

        Sprite sprite;

        private void Awake() {
            sprite = GetComponent<Sprite>();
        }


        //change sprite of the card
        public void ChangeSprite(Sprite sprite) {
            this.sprite = sprite;
        }


    }
}