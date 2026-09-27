using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Collections.Unicode;

namespace Assets.Script.TienLen.UI {
    public enum LobbyChangeReason { Initialized, PlayerJoined, PlayerLeft, SeatsChanged, GameStarted, TurnChanged }

    public class UiManager : MonoBehaviour {
        [Header("Dependencies")]
        [SerializeField] private TienLenGameController tienLenGameController;

        [Space(5)]
        [Header("UI Elements")]
        [SerializeField] private ActionPanel actionPanel;
        [SerializeField] private SessionDisplayUI sessionDisplayUI;
        [SerializeField] private StartGameUI startGameUI;
        [SerializeField] private TableVisualLayoutManager TableVisualLayoutManager;



        private void Start() {
            if (startGameUI != null && startGameUI.gameObject != null) {
                startGameUI.gameObject.SetActive(true);
            }

            if (actionPanel != null && actionPanel.gameObject != null) {
                actionPanel.gameObject.SetActive(false);
            }
        }

        public void Init() {
            RefreshLobby(LobbyChangeReason.Initialized);
        }

        public void RefreshLobby(LobbyChangeReason reason) {
            Debug.Log($"[UiManager] RefreshLobby reason={reason}");
            DisplaySessionInfo();
            DisplayStartButton();
            RefreshTableLayout();
        }

        private void DisplaySessionInfo() {
            if (sessionDisplayUI == null || sessionDisplayUI.gameObject == null) {
                return;
            }
            sessionDisplayUI.gameObject.SetActive(true);
            //sessionDisplayUI.Refresh();
        }

        private void DisplayStartButton() {
            if (startGameUI == null || startGameUI.gameObject == null) {
                return;
            }
            startGameUI.gameObject.SetActive(true);
            startGameUI.RefreshStartButton();
        }

        private void RefreshTableLayout() {
            if (TableVisualLayoutManager == null) {
                return;
            }
            TableVisualLayoutManager.RefreshLayout();
        }



    }
}