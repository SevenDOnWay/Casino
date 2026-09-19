using Assets.Script.NetWorkScript;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.UI {
    [System.Serializable]
    public class PlayerSeat : MonoBehaviour {
        public int SeatIndex;
        public bool IsOccupied;

        public CardHolder cardHolder;
        public Transform cardHolderPosition; //might not be needed if we use cardholder position directly

        public void BindPlayer( TienLenNetWorkPlayer player ) {
            IsOccupied = true;
            SeatIndex = player.SeatIndex;
            // Additional logic to bind the player to this seat

            cardHolder.ChangeAvatar(true);
        }

    }
}

