using Assets.Script.TienLen.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Script.TienLen.UI {
    /// <summary>
    /// Container for a visual table seat representing a player.
    /// Delegates player identity UI (Avatar, Name, Money, Level) and turn timer indicator to PlayerInfoView,
    /// and manages CardHolder and seat assignment state.
    /// </summary>
    [System.Serializable]
    public class PlayerSeat : MonoBehaviour {
        [Header("Slot Configuration")]
        [SerializeField, FormerlySerializedAs("seatIndex")]
        private int visualIndex; // Visual slot index (0 = bottom / local player, then clockwise)

        [Header("Player Info Visuals")]
        [SerializeField] private PlayerInfoView infoView;

        [Header("Card Container")]
        [SerializeField] private CardHolder cardHolder;

        public TienLenPlayer tienLenPlayer { get; private set; }

        /// <summary>True when this visual slot currently displays a player.</summary>
        public bool IsOccupied => tienLenPlayer != null;

        /// <summary>
        /// The network seat index this visual slot is currently bound to (-1 when empty).
        /// </summary>
        public int BoundSeatIndex { get; private set; } = -1;

        public PlayerInfoView InfoView => infoView;

        public CardHolder CardHolder { get => cardHolder; }

        public void SetInfoView( PlayerInfoView view ) {
            infoView = view;
        }


        /// <summary>
        /// Binds domain player model and network seat index to this visual seat container.
        /// </summary>
        public void BindPlayer( TienLenPlayer player, int networkSeatIndex ) {
            if ( player == null ) {
                Debug.LogError("[PlayerSeat] Cannot bind a null player.");
                return;
            }

            bool isRebindToDifferentPlayer = BoundSeatIndex != -1
                                          && BoundSeatIndex != networkSeatIndex
                                          && tienLenPlayer != null;

            if ( isRebindToDifferentPlayer && cardHolder != null ) {
                cardHolder.Clear();
            }

            tienLenPlayer = player;
            BoundSeatIndex = networkSeatIndex;

            if ( infoView != null ) {
                infoView.SetPlayerInfo(player);
            }
        }

        /// <summary>
        /// Clears all player visuals and cards from this seat slot.
        /// </summary>
        public void ClearSeat() {
            if ( cardHolder != null ) {
                cardHolder.Clear();
            }

            tienLenPlayer = null;
            BoundSeatIndex = -1;

            if ( infoView != null ) {
                infoView.Clear();
            }
        }

        /// <summary>
        /// Updates the displayed money value on this seat.
        /// </summary>
        public void UpdateMoney( long newAmount ) {
            if ( tienLenPlayer != null ) {
                tienLenPlayer.Money = newAmount;
            }

            if ( infoView != null ) {
                infoView.UpdateMoney(newAmount);
            }
        }

        public void setVisualIndex( int index ) {
            visualIndex = index;
        }

        public int GetVisualIndex() {
            return visualIndex;
        }

        #region Turn Timer

        /// <summary>
        /// Starts and displays the turn timer ring with an initial progress value.
        /// </summary>
        public void StartTurnTimer( float initialProgress = 1f ) {
            if ( infoView != null ) {
                infoView.StartTurnTimer(initialProgress);
            }
        }

        /// <summary>
        /// Updates the remaining turn time on the dedicated timer indicator.
        /// </summary>
        public void UpdateTurnTimer( float remainingProgress01 ) {
            if ( infoView != null ) {
                infoView.UpdateTurnTimer(remainingProgress01);
            }
        }

        /// <summary>
        /// Safely hides the turn timer indicator without modifying player avatar visibility.
        /// </summary>
        public void StopTurnTimer() {
            if ( infoView != null ) {
                infoView.StopTurnTimer();
            }
        }

        #endregion

        public static string FormatMoney( long amount ) => PlayerInfoView.FormatMoney(amount);
    }
}
