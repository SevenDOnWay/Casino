using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    public class hostUiPanel : MonoBehaviour {

        [Header("Dependencies")]
        [Inject] TienLenNetworkController networkController;
        [Inject] IPlayerProfileService playerProfileService;


        [SerializeField] TMP_InputField roomNameInputField;
        [SerializeField] TMP_Dropdown gameMode;
        [SerializeField] TMP_Dropdown stakeDropdown;
        [SerializeField] Button hostButton;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject alertPanel;
        [SerializeField] TextMeshProUGUI alertText;
        [SerializeField] Image alertPanelImage;
        Dictionary<int, string> gamemodeOptions = new () {
            {0, "normal"}, { 1, "fast"}
        };

        //TODO: Change this into configurable value, maybe read from config file or SO
        Dictionary<int, int> stakeOptions = new Dictionary<int, int>() {
            {0, 5}, {1,10}, {2,20}, {3,50} ,{4, 100 }
        };

        void Start() {
            gameMode.ClearOptions();

            gameMode.AddOptions(new List<string> { "normal", "fast" });
            stakeDropdown.AddOptions(new List<string> { "5", "10", "20", "50", "100" });
        }

        private void OnEnable() {
            hostButton.onClick.AddListener(OnHostButtonClicked);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnDisable() {
            hostButton.onClick.RemoveListener(OnHostButtonClicked);
            closeButton.onClick.RemoveAllListeners();
        }

        public void OnHostButtonClicked() {
            string roomName = roomNameInputField.text;
            int stake = stakeOptions[stakeDropdown.value];

            if (!ValidateInput(roomName, stake)) return;

            long money = playerProfileService?.CachedProfile != null ? playerProfileService.CachedProfile.money : 0;
            if ( money < 5 || money < stake ) {
                ShowAlert($"You need at least {Mathf.Max(5, stake)} coins to host this table.", AlertType.Error, 5f);
                return;
            }

            networkController.HostRoom(roomName, stake);
        }

        bool ValidateInput( string roomName, int stake ) {
            if ( string.IsNullOrEmpty(roomName) ) {
                ShowAlert("Room name cannot be empty.", AlertType.Error, 5f);
                return false;
            }
            return true;
        }

        private void ShowAlert( string s, AlertType type, float duration ) {
            StartCoroutine(ShowAlertCoroutine(s, type, duration));
        }

        IEnumerator ShowAlertCoroutine( string message, AlertType type, float duration ) {
            alertPanel.SetActive(true);
            alertText.text = message;

            switch ( type ) {
                case AlertType.Success:
                    alertPanelImage.color = new Color32(69, 129, 18, 255);
                    break;
                case AlertType.Error:
                    alertPanelImage.color = new Color32(185, 47, 46, 255);
                    break;
                case AlertType.Info:
                    alertPanelImage.color = new Color32(212, 231, 177, 255);
                    break;
            }


            yield return new WaitForSeconds(duration);

            alertPanel.SetActive(false);
        }

    }
}