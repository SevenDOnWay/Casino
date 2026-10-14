using Assets.Script.UI.MainMenu.Panel;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    public class BetButtonScript : MonoBehaviour {

        [SerializeField] private Button btn;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private TMP_Dropdown channelDropDown;

        private LobbySessionBrowserPanel lobbySessionBrowserPanel;
        private int index;

        public int Index => index;
        public TMP_Dropdown ChannelDropDown => channelDropDown;

        private void OnEnable() {
            if ( btn != null ) {
                btn.onClick.AddListener(OnButtonClicked);
            }
            if ( channelDropDown != null ) {
                channelDropDown.onValueChanged.AddListener(OnChannelValueChanged);
            }
        }

        private void OnDisable() {
            if ( btn != null ) {
                btn.onClick.RemoveListener(OnButtonClicked);
            }
            if ( channelDropDown != null ) {
                channelDropDown.onValueChanged.RemoveListener(OnChannelValueChanged);
            }
        }

        public void Init( int index, string text, LobbySessionBrowserPanel script ) {
            this.index = index;
            if ( this.text != null ) {
                this.text.text = text;
            }
            this.lobbySessionBrowserPanel = script;

            SetChannelActive(false);
        }

        private void OnButtonClicked() {
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
            if ( channelDropDown != null && channelDropDown.gameObject != null ) {
                channelDropDown.gameObject.SetActive(active);
                if ( active ) {
                    BuildChannel(selectedChannel);
                }
            }
        }

        public void BuildChannel( int selectedChannel = 1 ) {
            if ( channelDropDown == null || lobbySessionBrowserPanel == null ) return;

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