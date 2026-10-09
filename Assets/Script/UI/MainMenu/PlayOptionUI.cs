using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    public class PlayOptionUI : MonoBehaviour {

        [SerializeField] Button quickPlayButton;
        [SerializeField] Button hostButton;
        [SerializeField] Button searchTableButton;

        [Space(5)]
        [Header("Panels")]
        [SerializeField] GameObject hostPanel;
        [SerializeField] GameObject searchTablePanel;

        private void OnEnable() {
            hostButton.onClick.AddListener(OnHostButtonClicked);
            searchTableButton.onClick.AddListener(OnSearchTableButtonClicked);
        }

        private void OnDisable() {
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