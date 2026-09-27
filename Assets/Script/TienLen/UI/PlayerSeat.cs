using Assets.Script.TienLen.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Script.TienLen.UI {
    [System.Serializable]
    public class PlayerSeat : MonoBehaviour {
        [SerializeField, FormerlySerializedAs("seatIndex")]
        private int visualIndex; // Visual slot index (0 = bottom, then clockwise)

        public TienLenPlayer tienLenPlayer;

        public CardHolder cardHolder;
        public Transform cardHolderPosition;

        [Header("Avatar")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        /// <summary>True when this visual slot currently displays a player.</summary>
        public bool IsOccupied => tienLenPlayer != null;

        /// <summary>
        /// The **network** seat index this visual slot is currently bound to.
        /// -1 when empty. Used by TableVisualLayoutManager to diff and avoid
        /// rebinding unchanged slots.
        /// </summary>
        public int BoundSeatIndex { get; private set; } = -1;

        public void BindPlayer( TienLenPlayer player, int networkSeatIndex ) {
            if ( player == null ) {
                Debug.LogError( "[PlayerSeat] Cannot bind a null player." );
                return;
            }

            bool isRebindToDifferentPlayer = BoundSeatIndex != -1
                                          && BoundSeatIndex != networkSeatIndex
                                          && tienLenPlayer != null;

            // Clear on rebind (agreed): when a different player takes this slot,
            // wipe the CardHolder so the incoming player starts clean.
            if ( isRebindToDifferentPlayer && cardHolder != null ) {
                cardHolder.Clear();
            }

            tienLenPlayer = player;
            BoundSeatIndex = networkSeatIndex;

            // Only re-assign the CardHolder if it actually changed.
            if ( player.CardHolder != cardHolder ) {
                player.SetCardHolder( cardHolder );
            }

            ChangeAvatar( true );
        }

        public void ClearSeat() {
            if ( tienLenPlayer != null && cardHolder != null ) {
                cardHolder.Clear();
            }

            tienLenPlayer = null;
            BoundSeatIndex = -1;
            ChangeAvatar( false );
        }

        public void setVisualIndex( int index ) {
            visualIndex = index;
        }

        public int GetVisualIndex() {
            return visualIndex;
        }

        public void ChangeAvatar( bool isAvatarVisible ) {
            if ( spriteRenderer == null ) {
                Debug.LogWarning( $"[PlayerSeat {visualIndex}] spriteRenderer is null, cannot change avatar visibility." );
                return;
            }
            if ( spriteRenderer.gameObject == null ) {
                Debug.LogWarning( $"[PlayerSeat {visualIndex}] avatar gameObject is null, cannot change avatar visibility." );
                return;
            }
            spriteRenderer.gameObject.SetActive( isAvatarVisible );
        }
    }
}