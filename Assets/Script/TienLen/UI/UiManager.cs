using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Collections.Unicode;

namespace Assets.Script.TienLen.UI {
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
            startGameUI.gameObject.SetActive(true);

            actionPanel.gameObject.SetActive(false);    
        }

        public void Init() {
            DisplaySessionInfo();
            DisplayStartButton();
            RefreshTableLayout();
        }

        private void DisplaySessionInfo() {
            sessionDisplayUI.gameObject.SetActive(true);
            //sessionDisplayUI.Refresh();
        }

        private void DisplayStartButton() {
            startGameUI.gameObject.SetActive(true);
            startGameUI.RefreshStartButton();
        }

        private void RefreshTableLayout() {
            TableVisualLayoutManager.RefreshLayout();
        }



    }
}