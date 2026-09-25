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



        public void ClearSeat() {
            isOccupied = false;
            tienLenPlayer = null;

            if ( cardHolder != null ) {
                cardHolder.ChangeAvatar(false);
                // Optionally clear cards: cardHolder.ClearCards();
            }
        }

        //TODO: Implement the logic to assign a sprite to the seat based on the player or other criteria.
        //for now, it will be a placeholder method that can be expanded later.
        public void assignSprite(bool isOccupied) {
            cardHolder.ChangeAvatar(isOccupied);
        }


        public void setSeatIndex(int index ) {
            seatIndex = index;
        }

        public int GetSeatIndex() {
            return seatIndex;
        }

    }
}

