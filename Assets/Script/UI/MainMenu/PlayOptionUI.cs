using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    public class PlayOptionUI : MonoBehaviour {
        [Header("Dependencies")]
        [Inject] TienLenNetworkController networkController;
        [Inject] IPlayerProfileService playerProfileService;
        [SerializeField] Button quickPlayButton;
        [SerializeField] Button hostButton;
        [SerializeField] Button searchTableButton;

        [Space(5)]
        [Header("Panels")]
        [SerializeField] GameObject hostPanel;
        [SerializeField] GameObject searchTablePanel;

        [Space(5)]
        [Header("Alert Notice")]
        [SerializeField] private GameObject alertPanel;
        [SerializeField] private TextMeshProUGUI alertText;
        [SerializeField] private Image alertPanelImage;

        private Coroutine alertCoroutine;

        public const int MinimumStakeToPlay = 5;
        private static readonly int[] OrderedStakes = new[] { 100, 50, 20, 10, 5 };

        public void SetDependenciesForTest( TienLenNetworkController nc, IPlayerProfileService profileService, GameObject panel = null, TextMeshProUGUI text = null ) {
            this.networkController = nc;
            this.playerProfileService = profileService;
            this.alertPanel = panel;
            this.alertText = text;
        }

        private void OnEnable() {
            if ( quickPlayButton != null ) quickPlayButton.onClick.AddListener(OnQuickPlayButtonClicked);
            if ( hostButton != null ) hostButton.onClick.AddListener(OnHostButtonClicked);
            if ( searchTableButton != null ) searchTableButton.onClick.AddListener(OnSearchTableButtonClicked);
        }

        private void OnDisable() {
            if ( quickPlayButton != null ) quickPlayButton.onClick.RemoveListener(OnQuickPlayButtonClicked);
            if ( hostButton != null ) hostButton.onClick.RemoveListener(OnHostButtonClicked);
            if ( searchTableButton != null ) searchTableButton.onClick.RemoveListener(OnSearchTableButtonClicked);
        }

        private void OnHostButtonClicked() {
            if ( hostPanel != null ) hostPanel.SetActive(true);
        }

        private void OnSearchTableButtonClicked() {
            if ( searchTablePanel != null ) searchTablePanel.SetActive(true);
        }

        public async void OnQuickPlayButtonClicked() {
            long money = playerProfileService?.CachedProfile != null ? playerProfileService.CachedProfile.money : 0;

            if ( money < MinimumStakeToPlay ) {
                ShowAlert("You need at least 5 coins to play!", AlertType.Error, 4f);
                return;
            }

            int candidateStake = CalculateStakeForMoney(money);

            try {
                if ( networkController == null ) {
                    throw new InvalidOperationException("NetworkController is not configured.");
                }

                var result = await networkController.QuickPlay(candidateStake);
                if ( !result.Ok ) {
                    ShowAlert($"Failed to join match: {result.ShutdownReason}", AlertType.Error, 5f);
                }
            }
            catch ( Exception ex ) {
                Debug.LogError($"[PlayOptionUI] QuickPlay error: {ex.Message}");
                ShowAlert($"Quick play error: {ex.Message}", AlertType.Error, 5f);
            }
        }

        public static int CalculateStakeForMoney( long money ) {
            if ( money < MinimumStakeToPlay ) return MinimumStakeToPlay;

            float percentageMoney = money * 0.1f;
            foreach ( var stake in OrderedStakes ) {
                if ( percentageMoney >= stake ) {
                    return stake;
                }
            }

            return MinimumStakeToPlay;
        }

        public void ShowAlert( string message, AlertType type, float duration = 4f ) {
            if ( alertPanel != null && alertText != null ) {
                if ( alertCoroutine != null ) StopCoroutine(alertCoroutine);
                alertCoroutine = StartCoroutine(ShowAlertCoroutine(message, type, duration));
            }
            else {
                Debug.LogWarning($"[PlayOptionUI Notice] ({type}) {message}");
            }
        }

        private IEnumerator ShowAlertCoroutine( string message, AlertType type, float duration ) {
            alertPanel.SetActive(true);
            alertText.text = message;

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
            alertCoroutine = null;
        }
    }
}
