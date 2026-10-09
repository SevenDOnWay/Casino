using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using Fusion;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    public class PlayerProfileUI: MonoBehaviour {
        [Header("Dependencies")]
        [Inject] IPlayerProfileService playerProfileService;

        [SerializeField] AvatarDatabaseSO avatarDatabase;
        [SerializeField] Image avatar;
        [SerializeField] TextMeshProUGUI playerName;
        [SerializeField] TextMeshProUGUI playerLevel;
        [SerializeField] TextMeshProUGUI playerMoney;

        public void OnEnable() {
            if( avatarDatabase.TryGetAvatarSprite(playerProfileService.CachedProfile.avatarId, out Sprite sprite) ) {
                avatar.sprite = sprite;
            }

            playerName.text = playerProfileService.CachedProfile.displayName;
            playerLevel.text = $"LV: {playerProfileService.CachedProfile.level}";
            playerMoney.text = $"{playerProfileService.CachedProfile.money} $";
        }





    }
}