using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    public class PlayerProfileUI: MonoBehaviour {
        [Header("Dependencies")]
        [Inject] IPlayerProfileService playerProfileService;
        [Inject] AvatarDatabaseSO avatarDatabase;

        [SerializeField] Image avatar;
        [SerializeField] TextMeshProUGUI playerName;
        [SerializeField] TextMeshProUGUI playerLevel;
        [SerializeField] TextMeshProUGUI playerMoney;

        public void OnEnable() {
            if ( playerProfileService != null ) {
                playerProfileService.OnDisplayNameChanged += OnDisplayNameChanged;
                playerProfileService.OnAvatarChanged += OnAvatarChanged;
                playerProfileService.OnMoneyChanged += OnMoneyChanged;
                playerProfileService.OnLevelOrExpChanged += OnLevelOrExpChanged;
            }

            Refresh();
        }

        public void OnDisable() {
            if ( playerProfileService != null ) {
                playerProfileService.OnDisplayNameChanged -= OnDisplayNameChanged;
                playerProfileService.OnAvatarChanged -= OnAvatarChanged;
                playerProfileService.OnMoneyChanged -= OnMoneyChanged;
                playerProfileService.OnLevelOrExpChanged -= OnLevelOrExpChanged;
            }
        }

        public void Refresh() {
            if ( playerProfileService?.CachedProfile == null ) return;

            UpdateAvatar(playerProfileService.CachedProfile.avatarId);
            if ( playerName != null ) playerName.text = playerProfileService.CachedProfile.displayName;
            if ( playerLevel != null ) playerLevel.text = $"LV: {playerProfileService.CachedProfile.level}";
            if ( playerMoney != null ) playerMoney.text = $"{playerProfileService.CachedProfile.money} $";
        }

        private void UpdateAvatar( int avatarId ) {
            if ( avatarDatabase != null && avatarDatabase.TryGetAvatarSprite(avatarId, out Sprite sprite) ) {
                if ( avatar != null ) avatar.sprite = sprite;
            }
        }

        private void OnDisplayNameChanged( string newName ) {
            if ( playerName != null ) playerName.text = newName;
        }

        private void OnAvatarChanged( int newAvatarId ) => UpdateAvatar(newAvatarId);

        private void OnMoneyChanged( long newMoney ) {
            if ( playerMoney != null ) playerMoney.text = $"{newMoney} $";
        }

        private void OnLevelOrExpChanged( int newLevel, int newExp ) {
            if ( playerLevel != null ) playerLevel.text = $"LV: {newLevel}";
        }
    }
}
