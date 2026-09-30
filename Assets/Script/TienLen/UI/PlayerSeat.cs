using Assets.Script.TienLen.Player;
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

        [Header("Timer / Turn Indicator")]
        [SerializeField] private ClockWipe timerClockWipe;

        /// <summary>True when this visual slot currently displays a player.</summary>
        public bool IsOccupied => tienLenPlayer != null;

        /// <summary>
        /// The network seat index this visual slot is currently bound to (-1 when empty).
        /// </summary>
        public int BoundSeatIndex { get; private set; } = -1;

        private void Awake() {
            EnsureClockWipe();
        }

        private void EnsureClockWipe() {
            if ( timerClockWipe == null ) {
                timerClockWipe = GetComponentInChildren<ClockWipe>(true);
                if ( timerClockWipe == null && spriteRenderer != null ) {
                    timerClockWipe = spriteRenderer.GetComponent<ClockWipe>();
                    if ( timerClockWipe == null ) {
                        timerClockWipe = spriteRenderer.gameObject.AddComponent<ClockWipe>();
                    }
                }
            }
        }

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

            ChangeAvatar(true);
            StopTurnTimer();
        }

        public void ClearSeat() {
            if ( cardHolder != null ) {
                cardHolder.Clear();
            }

            tienLenPlayer = null;
            BoundSeatIndex = -1;
            ChangeAvatar(false);
            StopTurnTimer();
        }

        public void setVisualIndex( int index ) {
            visualIndex = index;
        }

        public int GetVisualIndex() {
            return visualIndex;
        }

        public void ChangeAvatar( bool isAvatarVisible ) {
            if ( spriteRenderer == null ) return;
            if ( spriteRenderer.gameObject == null ) return;
            spriteRenderer.gameObject.SetActive(isAvatarVisible);
        }

        public void StartTurnTimer( float initialProgress = 1f ) {
            EnsureClockWipe();
            if ( timerClockWipe != null ) {
                if ( !timerClockWipe.gameObject.activeSelf ) {
                    timerClockWipe.gameObject.SetActive(true);
                }
                timerClockWipe.SetProgress(initialProgress);
            }
        }

        public void UpdateTurnTimer( float remainingProgress01 ) {
            EnsureClockWipe();
            if ( timerClockWipe != null ) {
                if ( !timerClockWipe.gameObject.activeSelf ) {
                    timerClockWipe.gameObject.SetActive(true);
                }
                timerClockWipe.SetProgress(remainingProgress01);
            }
        }

        public void StopTurnTimer() {
            EnsureClockWipe();
            if ( timerClockWipe != null ) {
                if ( spriteRenderer != null && timerClockWipe.gameObject == spriteRenderer.gameObject ) {
                    timerClockWipe.ResetToDefault();
                }
                else {
                    timerClockWipe.SetProgress(0f);
                    timerClockWipe.gameObject.SetActive(false);
                }
            }
        }
    }
}
