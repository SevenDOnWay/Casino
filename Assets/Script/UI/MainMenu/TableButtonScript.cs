using Assets.Script.NetWorkScript;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    public class TableButtonScript : MonoBehaviour {

        [SerializeField] Button btn;
        [SerializeField] TextMeshProUGUI text;

        string roomName;
        int betAmount;
        TienLenNetworkController networkController;
        
        public int playerCount { get; private set; }

        public int maxPlayerCount { get; private set; } 
        void OnEnable() {
            btn.onClick.AddListener(OnButtonClicked);
        }

        void OnDisable() {
            btn.onClick.RemoveListener(OnButtonClicked);
        }

        public void Init( string roomName, 
            int betAmount, 
            TienLenNetworkController networkController,
            int playerCount,
            int maxPlayerCount) {
            this.roomName = roomName;
            this.betAmount = betAmount;
            this.networkController = networkController;
            this.playerCount = playerCount;
            this.maxPlayerCount = maxPlayerCount;
        }


        void OnButtonClicked() {
            networkController.JoinRoom(roomName, betAmount);
        }

        public void UpdatePlayerCount( int newPlayerCount ) {
            playerCount = newPlayerCount;

            UpdateUI();
        }

        void UpdateUI() {
            text.text = $"{playerCount}/{maxPlayerCount}";
        }

    }
}