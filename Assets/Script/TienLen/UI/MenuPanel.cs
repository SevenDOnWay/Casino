using Assets.Script.TienLen.Game;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class MenuPanel : MonoBehaviour {
        [Header("Dependencies")]
        [SerializeField] private SessionController sessionController;

        [Header("UI Buttons")]
        [SerializeField] private Button goToLobbyBtn;
        [SerializeField] private Button settingBtn;

        [Inject]
        public void Construct( SessionController sessionController = null ) {
            if ( sessionController != null ) {
                this.sessionController = sessionController;
            }
        }

        private SessionController SessionCtrl => sessionController != null
            ? sessionController
            : (sessionController = FindAnyObjectByType<SessionController>());

        private void OnEnable() {
            if ( goToLobbyBtn != null ) goToLobbyBtn.onClick.AddListener(OnGoToLobbyBtnClick);
            if ( settingBtn != null ) settingBtn.onClick.AddListener(OnSettingBtnClick);
        }

        private void OnDisable() {
            if ( goToLobbyBtn != null ) goToLobbyBtn.onClick.RemoveListener(OnGoToLobbyBtnClick);
            if ( settingBtn != null ) settingBtn.onClick.RemoveListener(OnSettingBtnClick);
        }

        void OnGoToLobbyBtnClick() {
            SessionCtrl?.RequestLeaveToLobby();
        }

        void OnSettingBtnClick() {
            Debug.Log("Setting button clicked");
        }
    }
}
