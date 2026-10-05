using Assets.Script.Data.SO;
using Assets.Script.TienLen.Player;
using TMPro;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    /// <summary>
    /// Displays player profile visuals (avatar, name, money, level) and turn timer at a table seat.
    /// </summary>
    public class PlayerInfoView : MonoBehaviour {
        [Header("Avatar & Profile")]
        [SerializeField] private AvatarDatabaseSO avatarDatabase;
        [SerializeField] private SpriteRenderer avatar;
        [SerializeField] private TMP_Text money;
        [SerializeField] private TMP_Text levelText;

        [Header("Turn Timer Indicator")]
        [SerializeField] private ClockWipe turnTimer;

        public AvatarDatabaseSO AvatarDatabase => avatarDatabase;
        public SpriteRenderer Avatar => avatar;
        public TMP_Text MoneyText => money;
        public TMP_Text LevelText => levelText;
        public ClockWipe TurnTimer => turnTimer;

        [Inject]
        public void Construct( AvatarDatabaseSO avatarDatabase ) {
            this.avatarDatabase = avatarDatabase;
        }

        public void SetPlayerInfo( TienLenPlayer player ) {
            if ( player == null ) return;

            avatarDatabase.TryGetAvatarSprite(player.AvatarId, out Sprite avatarSprite);

            avatar.sprite = avatarSprite;
            money.text = FormatMoney(player.Money);
            levelText.text = $"player.Level";


            SetVisible(true);
            StopTurnTimer();
        }

        public void Clear() {
            if ( avatar != null ) avatar.sprite = null;
            if ( money != null ) money.text = string.Empty;
            if ( levelText != null ) levelText.text = string.Empty;

            StopTurnTimer();
            SetVisible(false);
        }

        public void UpdateMoney( long newAmount ) {
            money.text = FormatMoney(newAmount);
        }

        public void UpdateMoney( int newAmount ) {
            UpdateMoney((long)newAmount);
        }

        public void SetVisible( bool visible ) {
            gameObject.SetActive(visible);

            if ( avatar != null ) avatar.gameObject.SetActive(visible);
            if ( money != null ) money.gameObject.SetActive(visible);
            if ( levelText != null ) levelText.gameObject.SetActive(visible);
        }

        public void SetAvatarVisible( bool visible ) {
            if ( avatar != null && avatar.gameObject != null ) {
                avatar.gameObject.SetActive(visible);
            }
        }

        public void StartTurnTimer( float initialProgress = 1f ) {
            if ( turnTimer != null ) {
                if ( !turnTimer.gameObject.activeSelf ) {
                    turnTimer.gameObject.SetActive(true);
                }
                turnTimer.SetProgress(initialProgress);
            }
        }

        public void UpdateTurnTimer( float remainingProgress01 ) {
            if ( turnTimer != null ) {
                if ( !turnTimer.gameObject.activeSelf ) {
                    turnTimer.gameObject.SetActive(true);
                }
                turnTimer.SetProgress(remainingProgress01);
            }
        }

        public void StopTurnTimer() {
            if ( turnTimer != null ) {
                turnTimer.gameObject.SetActive(false);
            }
        }

        public void SetTurnTimer( float fillAmount ) {
            UpdateTurnTimer(fillAmount);
        }

        public void ClearTurnTimer() {
            StopTurnTimer();
        }

        public static string FormatMoney( long amount ) {
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
