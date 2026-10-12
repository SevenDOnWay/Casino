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
        [SerializeField] private GameObject infoRoot;

        [Header("Turn Timer Indicator")]
        [SerializeField] private ClockWipe turnTimer;

        public AvatarDatabaseSO AvatarDatabase => avatarDatabase;
        public SpriteRenderer Avatar => avatar;
        public TMP_Text MoneyText => money;
        public ClockWipe TurnTimer => turnTimer;
        public GameObject InfoRoot => infoRoot;

        [Inject]
        public void Construct( AvatarDatabaseSO avatarDatabase = null ) {
            if ( avatarDatabase != null ) {
                this.avatarDatabase = avatarDatabase;
            }
        }

        public void Initialize(
            SpriteRenderer avatar = null,
            TMP_Text money = null,
            ClockWipe turnTimer = null,
            GameObject infoRoot = null,
            AvatarDatabaseSO avatarDatabase = null
        ) {
            this.avatar = avatar;
            this.money = money;

            this.turnTimer = turnTimer;
            this.infoRoot = infoRoot;
            this.avatarDatabase = avatarDatabase;
        }

        public void SetPlayerInfo( TienLenPlayer player ) {
            if ( player == null ) return;

            if ( avatarDatabase != null && avatarDatabase.TryGetAvatarSprite(player.AvatarId, out Sprite avatarSprite) ) {
                if ( avatar != null ) avatar.sprite = avatarSprite;
            }

            if ( money != null ) {
                money.text = FormatMoney(player.Money);
            }

            StopTurnTimer();
        }

        public void Clear() {
            if ( avatar != null ) avatar.sprite = null;
            if ( money != null ) money.text = string.Empty;

            StopTurnTimer();
        }

        public void UpdateMoney( long newAmount ) {
            if ( money != null ) {
                money.text = FormatMoney(newAmount);
            }
        }

        public void UpdateMoney( int newAmount ) {
            UpdateMoney((long)newAmount);
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
