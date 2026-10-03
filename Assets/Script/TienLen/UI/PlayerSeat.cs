using Assets.Script.Data.SO;
using Assets.Script.TienLen.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Script.TienLen.UI {
    /// <summary>
    /// Container for a visual table seat representing a player.
    /// Manages player identity UI (Avatar, Name, Money, Level), CardUI holder, and turn timer indicator.
    /// </summary>
    [System.Serializable]
    public class PlayerSeat : MonoBehaviour {
        [Header("Slot Configuration")]
        [SerializeField, FormerlySerializedAs("seatIndex")]
        private int visualIndex; // Visual slot index (0 = bottom / local player, then clockwise)

        [Header("Player Info Visuals")]
        [Tooltip("Optional parent GameObject holding all info labels (Avatar, Name, Money, Level)")]
        [SerializeField] private GameObject infoRoot;

        [Tooltip("SpriteRenderer displaying the player avatar")]
        [SerializeField, FormerlySerializedAs("spriteRenderer")]
        private SpriteRenderer avatarRenderer;

        [Tooltip("ScriptableObject catalogue mapping integer avatarId to Sprite")]
        [SerializeField] private AvatarDatabaseSO avatarDatabase;

        [Tooltip("Text component displaying player display name")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("Text component displaying player money/coins")]
        [SerializeField] private TMP_Text moneyText;

        [Tooltip("Text component displaying player level (optional)")]
        [SerializeField] private TMP_Text levelText;

        [Header("Card Container")]
        [Tooltip("CardHolder component managing player cards / hand UI")]
        public CardHolder cardHolder;

        [Tooltip("Transform anchor representing the position of card placement")]
        public Transform cardHolderPosition;

        [Header("Timer / Turn Indicator")]
        [Tooltip("Dedicated ClockWipe turn timer indicator connected via serialized field")]
        [SerializeField] private ClockWipe timerClockWipe;

        public TienLenPlayer tienLenPlayer { get; private set; }

        /// <summary>True when this visual slot currently displays a player.</summary>
        public bool IsOccupied => tienLenPlayer != null;

        /// <summary>
        /// The network seat index this visual slot is currently bound to (-1 when empty).
        /// </summary>
        public int BoundSeatIndex { get; private set; } = -1;

        private void Awake() {
            if ( timerClockWipe != null ) {
                timerClockWipe.gameObject.SetActive(false);
            }
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

            // 1. Update Avatar
            if ( avatarDatabase != null && avatarRenderer != null ) {
                var avatarSprite = avatarDatabase.GetAvatarSprite(player.AvatarId);
                if ( avatarSprite != null ) {
                    avatarRenderer.sprite = avatarSprite;
                }
            }

            // 2. Update Name
            if ( nameText != null ) {
                nameText.text = !string.IsNullOrEmpty(player.PlayerName) ? player.PlayerName : "Player";
            }

            // 3. Update Money
            if ( moneyText != null ) {
                moneyText.text = FormatMoney(player.Money);
            }

            // 4. Update Level
            if ( levelText != null ) {
                levelText.text = $"Lv.{player.Level}";
            }

            SetInfoVisible(true);
            StopTurnTimer();
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

            SetInfoVisible(false);
            StopTurnTimer();
        }

        /// <summary>
        /// Updates the displayed money value on this seat.
        /// </summary>
        public void UpdateMoney( long newAmount ) {
            if ( tienLenPlayer != null ) {
                tienLenPlayer.Money = newAmount;
            }

            if ( moneyText != null ) {
                moneyText.text = FormatMoney(newAmount);
            }
        }

        public void setVisualIndex( int index ) {
            visualIndex = index;
        }

        public int GetVisualIndex() {
            return visualIndex;
        }

        /// <summary>
        /// Controls visibility of the player identity container (Avatar, Name, Money, Level).
        /// </summary>
        public void SetInfoVisible( bool visible ) {
            if ( infoRoot != null ) {
                infoRoot.SetActive(visible);
                return;
            }

            if ( avatarRenderer != null ) avatarRenderer.gameObject.SetActive(visible);
            if ( nameText != null ) nameText.gameObject.SetActive(visible);
            if ( moneyText != null ) moneyText.gameObject.SetActive(visible);
            if ( levelText != null ) levelText.gameObject.SetActive(visible);
        }

        public void ChangeAvatar( bool isAvatarVisible ) {
            if ( avatarRenderer != null && avatarRenderer.gameObject != null ) {
                avatarRenderer.gameObject.SetActive(isAvatarVisible);
            }
        }

        #region Turn Timer

        /// <summary>
        /// Starts and displays the turn timer ring with an initial progress value.
        /// </summary>
        public void StartTurnTimer( float initialProgress = 1f ) {
            if ( timerClockWipe != null ) {
                if ( !timerClockWipe.gameObject.activeSelf ) {
                    timerClockWipe.gameObject.SetActive(true);
                }
                timerClockWipe.SetProgress(initialProgress);
            }
        }

        /// <summary>
        /// Updates the remaining turn time on the dedicated timer indicator.
        /// </summary>
        public void UpdateTurnTimer( float remainingProgress01 ) {
            if ( timerClockWipe != null ) {
                if ( !timerClockWipe.gameObject.activeSelf ) {
                    timerClockWipe.gameObject.SetActive(true);
                }
                timerClockWipe.SetProgress(remainingProgress01);
            }
        }

        /// <summary>
        /// Safely hides the turn timer indicator without modifying player avatar visibility.
        /// </summary>
        public void StopTurnTimer() {
            if ( timerClockWipe != null ) {
                timerClockWipe.gameObject.SetActive(false);
            }
        }

        #endregion

        private static string FormatMoney( long amount ) {
            if ( amount >= 1_000_000 ) {
                return $"${(amount / 1_000_000.0):0.#}M";
            }
            if ( amount >= 10_000 ) {
                return $"${(amount / 1_000.0):0.#}K";
            }
            return $"${amount:N0}";
        }
    }
}
