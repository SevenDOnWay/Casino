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


        public void BindNetworkPlayer( TienLenNetWorkPlayer player ) {
            if ( player == null ) {
                Debug.LogError("Cannot bind a null network player to the seat.");
                return;
            }

            isOccupied = true;

            // Additional logic to bind the player to this seat
            cardHolder.ChangeAvatar(true);

            Debug.Log($"Player has occupied seat {seatIndex}");
        }

        public void ClearSeat() {
            isOccupied = false;
            tienLenPlayer = null;

            if ( cardHolder != null ) {
                cardHolder.ChangeAvatar(false);
                // Optionally clear cards: cardHolder.ClearCards();
            }
        }

        public void BindLogicPlayer( TienLenPlayer tienLenPlayer ) {
            this.tienLenPlayer = tienLenPlayer;
            tienLenPlayer.SetCardHolder(cardHolder);
        }


        public void setSeatIndex(int index ) {
            seatIndex = index;
        }


    }
}

