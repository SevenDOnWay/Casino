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

        private LobbySessionBrowserController controller;
        private GameObject channelGameObject;
        private int index;

        public int Index => index;
        public TMP_Dropdown ChannelDropDown => channelDropDown;

        private void OnEnable() {
            if ( betButton != null ) {
                betButton.onClick.AddListener(OnBetButtonClicked);
            }
            if ( channelDropDown != null ) {
                channelDropDown.onValueChanged.AddListener(OnChannelValueChanged);
            }
        }

        private void OnDisable() {
            if ( betButton != null ) {
                betButton.onClick.RemoveListener(OnBetButtonClicked);
            }
            if ( channelDropDown != null ) {
                channelDropDown.onValueChanged.RemoveListener(OnChannelValueChanged);
            }
        }

        public void Init( int index, string text, LobbySessionBrowserController controller ) {
            this.index = index;
            if ( this.text != null ) {
                this.text.text = text;
            }
            this.controller = controller;
            SetChannelActive(false);
        }

        public void Init( int index, string text, LobbySessionBrowserPanelView panelView ) {
            this.index = index;
            if ( this.text != null ) {
                this.text.text = text;
            }
            if ( panelView != null ) {
                this.controller = panelView.Controller;
            }
            SetChannelActive(false);
        }

        public void SetController( LobbySessionBrowserController controller ) {
            this.controller = controller;
        }

        public void OnBetButtonClicked() {
            if ( controller != null ) {
                _ = controller.SelectStake(index);
            }
        }

        public void OnChannelValueChanged( int value ) {
            if ( controller != null ) {
                _ = controller.SelectChannel(value + 1);
            }
        }

        public void SetChannelActive( bool active, int selectedChannel = 1 ) {
            channelGameObject ??= channelDropDown?.gameObject;
            if ( channelGameObject != null ) {
                channelGameObject.SetActive(active);
            }
            if ( active ) {
                BuildChannel(selectedChannel);
            }
        }

        public void BuildChannel( int selectedChannel = 1 ) {
            int channelCount = controller != null ? controller.GetChannelCountForStake(controller.SelectedStake) : 1;
            BuildChannel(channelCount, selectedChannel);
        }

        public void BuildChannel( int channelCount, int selectedChannel = 1 ) {
            if ( channelDropDown == null ) return;

            channelDropDown.ClearOptions();
            var options = new List<string>();
            for ( int i = 1; i <= Mathf.Max(1, channelCount); i++ ) {
                options.Add($"Channel {i}");
            }

            channelDropDown.AddOptions(options);
            int targetIndex = Mathf.Clamp(selectedChannel - 1, 0, options.Count - 1);
            channelDropDown.SetValueWithoutNotify(targetIndex);
        }

        public void EnableDropDown( bool enable ) {
            channelGameObject ??= channelDropDown?.gameObject;
            if ( channelGameObject != null ) {
                channelGameObject.SetActive(enable);
            }
        }
    }
}
