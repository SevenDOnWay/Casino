using Assets.Script.UI.MainMenu.Panel;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    public class BetButtonScript : MonoBehaviour {

        [SerializeField] private Button betButton;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private TMP_Dropdown channelDropDown;

        private LobbySessionBrowserPanel lobbySessionBrowserPanel;
        private GameObject channelGameObject;
        private int index;

        public int Index => index;
        public TMP_Dropdown ChannelDropDown => channelDropDown;

        void Start() {
            channelGameObject = channelDropDown.gameObject;
        }

        private void OnEnable() {
            if ( betButton != null ) {
                betButton.onClick.AddListener(OnBetButtonClicked);
            }
            if ( channelDropDown != null ) {
                channelDropDown.onValueChanged.AddListener(OnChannelValueChanged);
            }
        }

        private void OnDisable() {
            if ( betButton != null ) betButton.onClick.RemoveListener(OnBetButtonClicked);
            if ( channelDropDown != null ) channelDropDown.onValueChanged.RemoveListener(OnChannelValueChanged);
        }

        public void Init( int index, string text, LobbySessionBrowserPanel script ) {
            this.index = index;
            this.text.text = text;
            this.lobbySessionBrowserPanel = script;

            SetChannelActive(false);
        }

        private void OnBetButtonClicked() {
            if ( lobbySessionBrowserPanel != null ) {
                lobbySessionBrowserPanel.FindTable(index);
            }
        }

        private void OnChannelValueChanged( int value ) {
            if ( lobbySessionBrowserPanel != null ) {
                _ = lobbySessionBrowserPanel.SwitchLobby(index, value);
            }
        }

        public void SetChannelActive( bool active, int selectedChannel = 1 ) {
            channelGameObject.SetActive(active);
            if ( active ) BuildChannel(selectedChannel);
        }

        public void BuildChannel( int selectedChannel = 1 ) {
            channelDropDown.ClearOptions();
            var options = lobbySessionBrowserPanel.GetChannelOptions(index);
            if ( options != null && options.Count > 0 ) {
                channelDropDown.AddOptions(options);
                int targetIndex = Mathf.Clamp(selectedChannel - 1, 0, options.Count - 1);
                channelDropDown.SetValueWithoutNotify(targetIndex);
            }
        }
    }
}