using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Script {
    public class CardView : MonoBehaviour, IPointerClickHandler {
        [SerializeField] private float selectElevation = 0.7f;

        private SpriteRenderer spriteRenderer;
        private Card card;
        private bool interactable;
        private bool selected;

        public event Action<CardView> OnClicked;

        public Card Card {
            get => card;
            set => card = value;
        }

        public bool IsSelected => selected;
        public bool IsInteractable => interactable;

        private void Awake() {
            EnsureSpriteRenderer();
        }

        private void EnsureSpriteRenderer() {
            if ( spriteRenderer == null ) {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void Init( Card card ) {
            this.card = card;
        }

        public void ChangeSprite( Sprite sprite ) {
            EnsureSpriteRenderer();
            if ( spriteRenderer != null ) {
                spriteRenderer.sprite = sprite;
            }
        }

        public void OnPointerClick( PointerEventData eventData ) {
            if ( !interactable ) return;
            OnClicked?.Invoke(this);
        }

        public void SetInteractable( bool value ) {
            interactable = value;
            if ( !value ) {
                SetSelected(false);
            }
        }

        public void SetSelected( bool value ) {
            selected = value;

            Vector3 position = transform.localPosition;
            position.y = selected ? selectElevation : 0f;
            transform.localPosition = position;
        }
    }
}
