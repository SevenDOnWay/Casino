using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using System.Collections.Generic;
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

        Dictionary<int, int> stakeOptions = new Dictionary<int, int>() {
            {0, 5}, {1,10}, {2,20}, {3,50} ,{4, 100 }
        };


        private void OnEnable() {
            quickPlayButton.onClick.AddListener(OnQuickPlayButtonClicked);
            hostButton.onClick.AddListener(OnHostButtonClicked);
            searchTableButton.onClick.AddListener(OnSearchTableButtonClicked);
        }

        private void OnDisable() {
            quickPlayButton.onClick.RemoveListener(OnQuickPlayButtonClicked);
            hostButton.onClick.RemoveListener(OnHostButtonClicked);
            searchTableButton.onClick.RemoveListener(OnSearchTableButtonClicked);
        }

        private void OnHostButtonClicked() {
            hostPanel.SetActive(true);
        }

        private void OnSearchTableButtonClicked() {
            searchTablePanel.SetActive(true);
        }

        private void OnQuickPlayButtonClicked() {
            long money = playerProfileService.CachedProfile.money;

            int candidateStake = 0;
            for ( int i = 0; i < stakeOptions.Count; i++ ) {
                if ( money >= stakeOptions[i] ) break;
                candidateStake = stakeOptions[i];
            }


            networkController.QuickPlay(candidateStake);
        }
    }
}