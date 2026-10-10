using Assets.Script.UI.MainMenu.Panel;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    public class BetButtonScript : MonoBehaviour {

        [SerializeField] Button btn;
        [SerializeField] TextMeshProUGUI text;

        LobbySessionBrowserPanel lobbySessionBrowserPanel;
        int index;

        private void OnEnable() {
            btn.onClick.AddListener(OnButtonClicked);
        }

        private void OnDisable() {
            btn.onClick.RemoveListener(OnButtonClicked);
        }

        public void Init(int index, string text, LobbySessionBrowserPanel script ) {
            this.index = index;
            this.text.text = text;
            this.lobbySessionBrowserPanel = script;
        }

        void OnButtonClicked() {
            lobbySessionBrowserPanel.FindTable(index);
        }

    }
}