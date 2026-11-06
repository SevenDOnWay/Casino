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

        Dictionary<int, int> stakeOptions = new Dictionary<int, int>() {
            {0, 5}, {1, 10}, {2, 20}, {3, 50}, {4, 100}
        };

        void Start() {
            if ( gameMode != null ) {
                gameMode.ClearOptions();
                gameMode.AddOptions(new List<string> { "normal", "fast" });
            }

            if ( stakeDropdown != null ) {
                stakeDropdown.ClearOptions();
                stakeDropdown.AddOptions(new List<string> { "5", "10", "20", "50", "100" });
            }
        }

        private void OnEnable() {
            if ( hostButton != null ) hostButton.onClick.AddListener(OnHostButtonClicked);
            if ( closeButton != null ) closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDisable() {
            if ( hostButton != null ) hostButton.onClick.RemoveListener(OnHostButtonClicked);
            if ( closeButton != null ) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnCloseClicked() {
            gameObject.SetActive(false);
        }

        public void OnHostButtonClicked() {
            string roomName = roomNameInputField != null ? roomNameInputField.text : string.Empty;
            int stakeIndex = stakeDropdown != null ? stakeDropdown.value : 0;
            int stake = stakeOptions.TryGetValue(stakeIndex, out int val) ? val : 5;

            if ( !ValidateInput(roomName, stake) ) return;

            long money = playerProfileService?.CachedProfile != null ? playerProfileService.CachedProfile.money : 0;
            if ( money < 5 || money < stake ) {
                ShowAlert($"You need at least {Mathf.Max(5, stake)} coins to host this table.", AlertType.Error, 5f);
                return;
            }

            if ( networkController != null ) {
                networkController.HostRoom(roomName, stake);
            }
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
            if ( alertPanel != null ) alertPanel.SetActive(true);
            if ( alertText != null ) alertText.text = message;

            if ( alertPanelImage != null ) {
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
            }

            yield return new WaitForSeconds(duration);

            if ( alertPanel != null ) alertPanel.SetActive(false);
        }
    }
}
