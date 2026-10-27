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
            if ( playerProfileService?.CachedProfile == null ) return;

            if ( avatarDatabase != null && avatarDatabase.TryGetAvatarSprite(playerProfileService.CachedProfile.avatarId, out Sprite sprite) ) {
                if ( avatar != null ) avatar.sprite = sprite;
            }

            if ( playerName != null ) playerName.text = playerProfileService.CachedProfile.displayName;
            if ( playerLevel != null ) playerLevel.text = $"LV: {playerProfileService.CachedProfile.level}";
            if ( playerMoney != null ) playerMoney.text = $"{playerProfileService.CachedProfile.money} $";
        }
    }
}
