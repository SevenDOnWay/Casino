using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.UI {
    [System.Serializable]
    public class PlayerSeat : MonoBehaviour {
        public int seatIndex;
        public bool isOccupied;
        public TienLenPlayer tienLenPlayer;

        public CardHolder cardHolder;
        public Transform cardHolderPosition; //might not be needed if we use cardholder position directly


        public void BindNetworkPlayer( TienLenNetWorkPlayer player ) {
            isOccupied = true;
            seatIndex = player.PlayerSeatIndex;

            // Additional logic to bind the player to this seat
            cardHolder.ChangeAvatar(true);
        }

        public void BindLogicPlayer( TienLenPlayer tienLenPlayer ) {
            this.tienLenPlayer = tienLenPlayer;
            tienLenPlayer.SetCardHolder(cardHolder);
        }

        public void ClearSeat() {
            isOccupied = false;
            seatIndex = -1;
            tienLenPlayer = null;

            if ( cardHolder != null ) {
                cardHolder.ChangeAvatar(false);
                // Optionally clear cards: cardHolder.ClearCards();
            }
        }
    }
}

