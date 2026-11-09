using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.TienLen.UI {
    public class MenuPanel : MonoBehaviour {
        [Header("Dependencies")]


        [SerializeField] Button goToLobbyBtn;
        [SerializeField] Button settingBtn;

        private void OnEnable() {
            goToLobbyBtn.onClick.AddListener(OnGoToLobbyBtnClick);
            settingBtn.onClick.AddListener(OnSettingBtnClick);
        }

        private void OnDisable() {
            goToLobbyBtn.onClick.RemoveListener(OnGoToLobbyBtnClick);
            settingBtn.onClick.RemoveListener(OnSettingBtnClick);
        }

        void OnGoToLobbyBtnClick() {

            //call to script to queue
            //script check if played
            //if not played, go to lobby
            //if played, show message "left at the end of game"

        }

        void OnSettingBtnClick() {
            Debug.Log("Setting button clicked");
        }

    }
}
