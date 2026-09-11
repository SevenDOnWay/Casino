using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Script {
    public class CardView : MonoBehaviour, IPointerClickHandler {
        SpriteRenderer spriteRenderer;

        Card card;

        private bool interactable;
        private bool selected;



        public event Action<CardView> OnClicked;

        public Card Card { get => card; set => card = value; }

        private void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Init(Card card ) {
            this.card = card;
        }

        //change sprite of the card
        public void ChangeSprite(Sprite sprite) {
            this.spriteRenderer.sprite = sprite;
        }

        public void OnPointerClick( PointerEventData eventData ) {
            if ( !interactable ) return;

            Debug.Log($"[CardView] Card clicked: {card}");

            OnClicked?.Invoke(this);
        }

        public void SetInteractable( bool value ) {
            interactable = value;

            if ( !value ) SetSelected(false);
        }

        public void SetSelected( bool value ) {
            selected = value;

            Vector3 position = transform.localPosition;

            // Example selection effect
            position.y = selected ? 0.7f : 0f;
            transform.localPosition = position;
        }
    }
}