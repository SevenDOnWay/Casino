using Assets.Script.NetWorkScript;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    public class PlayOptionUI : MonoBehaviour {

        [Inject] TienLenNetworkController networkController;
        [SerializeField] Button quickPlayButton;
        [SerializeField] Button hostButton;
        [SerializeField] Button searchTableButton;

        [Space(5)]
        [Header("Panels")]
        [SerializeField] GameObject hostPanel;
        [SerializeField] GameObject searchTablePanel;

        private void OnEnable() {
            quickPlayButton.onClick.AddListener(() => networkController.QuickPlay());
            hostButton.onClick.AddListener(OnHostButtonClicked);
            searchTableButton.onClick.AddListener(OnSearchTableButtonClicked);
        }

        private void OnDisable() {
            quickPlayButton.onClick.RemoveAllListeners();
            hostButton.onClick.RemoveListener(OnHostButtonClicked);
            searchTableButton.onClick.RemoveListener(OnSearchTableButtonClicked);
        }

        private void OnHostButtonClicked() {
            hostPanel.SetActive(true);
        }

        private void OnSearchTableButtonClicked() {
            searchTablePanel.SetActive(true);
        }



    }
}