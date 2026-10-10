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


        [SerializeField] TMP_InputField roomNameInputField;
        [SerializeField] TMP_Dropdown gameMode;
        [SerializeField] Button hostButton;
        [SerializeField] Button closeButton;
        Dictionary<int, int > dic = new Dictionary<int, int>() {
            {0, 5}, {1,10}, {2,20}, {3,50} ,{4, 100 }
        };

        void Start() {
            gameMode.ClearOptions();
            gameMode.AddOptions(new List<string> { "5", "10", "20", "50", "100" });
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
            int stake = dic[gameMode.value];

            if(ValidateInput(roomName, stake)) return;

            networkController.HostRoom(roomName, stake);
        }

        bool ValidateInput( string roomName, int stake ) {
            if ( string.IsNullOrEmpty(roomName) ) {
                Debug.LogWarning("Room name cannot be empty.");
                return false;
            }
            if ( stake <= 0 ) {
                Debug.LogWarning("Stake must be greater than zero.");
                return false;
            }
            return true;
        }

    }
}