using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.UI {
    [System.Serializable]
    public class PlayerSeat : MonoBehaviour {
        [SerializeField] private int seatIndex; // Visual index of the seat (0 to 3)
        public bool isOccupied;
        public TienLenPlayer tienLenPlayer;

        public CardHolder cardHolder;
        public Transform cardHolderPosition; //might not be needed if we use cardholder position directly

        [Header("Avatar")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        public void BindNetworkPlayer( TienLenNetWorkPlayer player ) {
            if ( player == null ) {
                Debug.LogError("Cannot bind a null network player to the seat.");
                return;
            }

            isOccupied = true;

            //TODO: Implement logic to bind network player to the avatar
            ChangeAvatar(true);
        }

        public void BindLogicPlayer( TienLenPlayer tienLenPlayer ) {
            if( tienLenPlayer == null ) {
                Debug.LogError("Cannot bind a null logic player to the seat.");
                return;
            }

            this.tienLenPlayer = tienLenPlayer;
            tienLenPlayer.SetCardHolder(cardHolder);

            Debug.Log($"Logic player {tienLenPlayer.PlayerName} has been bound to seat {seatIndex}");
        }

        public void BindPlayer(TienLenPlayer tienLenPlayer) {
            if( tienLenPlayer == null ) {
                Debug.LogError("Cannot bind a null player to the seat.");
                return;
            }

            this.tienLenPlayer = tienLenPlayer;

            tienLenPlayer.SetCardHolder(cardHolder);
            isOccupied = true;

        }



        public void ClearSeat() {
            isOccupied = false;
            tienLenPlayer = null;

            ChangeAvatar(false);
        }

        public void setSeatIndex(int index ) {
            seatIndex = index;
        }

        public int GetSeatIndex() {
            return seatIndex;
        }

        public void ChangeAvatar( bool isAvatarVisible ) {
            spriteRenderer.gameObject.SetActive(isAvatarVisible); //TODO: Change into configurable sprite or avatar.
        }

    }
}

