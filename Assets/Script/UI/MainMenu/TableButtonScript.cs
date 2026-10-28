using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    public class TableButtonScript : MonoBehaviour {

        [SerializeField] private Button btn;
        [SerializeField] private TextMeshProUGUI text;

        private string roomName;
        private int betAmount;
        private int channel = 1;
        private TienLenNetworkController networkController;
        private IPlayerProfileService profileService;
        private Action<string> onNotice;

        public int playerCount { get; private set; }
        public int maxPlayerCount { get; private set; } 

        void OnEnable() {
            if ( btn != null ) {
                btn.onClick.AddListener(OnButtonClicked);
            }
        }

        void OnDisable() {
            if ( btn != null ) {
                btn.onClick.RemoveListener(OnButtonClicked);
            }
        }

        public void Init( string roomName, 
            int betAmount, 
            TienLenNetworkController networkController,
            int playerCount,
            int maxPlayerCount,
            int channel = 1,
            IPlayerProfileService profileService = null,
            Action<string> onNotice = null ) {
            this.roomName = roomName;
            this.betAmount = betAmount;
            this.networkController = networkController;
            this.playerCount = playerCount;
            this.maxPlayerCount = maxPlayerCount;
            this.channel = channel;
            this.profileService = profileService;
            this.onNotice = onNotice;

            UpdateUI();
        }

        void OnButtonClicked() {
            if ( profileService?.CachedProfile != null ) {
                if ( profileService.CachedProfile.money < 5 || profileService.CachedProfile.money < betAmount ) {
                    string msg = $"You need at least {Mathf.Max(5, betAmount)} coins to join this table.";
                    Debug.LogWarning($"[TableButtonScript] {msg}");
                    onNotice?.Invoke(msg);
                    return;
                }
            }

            if ( networkController != null ) {
                networkController.JoinRoom(roomName, betAmount, channel);
            }
        }

        public void UpdatePlayerCount( int newPlayerCount ) {
            playerCount = newPlayerCount;
            UpdateUI();
        }

        void UpdateUI() {
            if ( text != null ) {
                text.text = $"{playerCount}/{maxPlayerCount}";
            }
        }
    }
}