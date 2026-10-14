using Assets.Script.NetWorkScript;
using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu.Panel {
    public class LobbySessionBrowserPanel : MonoBehaviour {

        private sealed class SessionEntry {
            public GameObject GameObject;
            public TableButtonScript TableButtonScript;
            public int index;
        }

        [Header("Dependencies")]
        [Inject] TienLenNetworkController networkController;

        [Header("Main UI")]
        [SerializeField] Button closeButton;
        [SerializeField] GameObject betButtonPrefab;
        [SerializeField] GameObject sessionButtonPrefab;
        [SerializeField] Transform betButtonContainer;
        [SerializeField] Transform sessionButtonContainer;

        [Header("Channel / Sharding UI (Optional)")]
        [SerializeField] private TMP_Dropdown channelDropdown;
        [SerializeField] private Button nextChannelButton;
        [SerializeField] private Button prevChannelButton;
        [SerializeField] private TMP_Text channelText;
        [SerializeField] private int maxChannels = 5;

        private Dictionary<string, SessionEntry> sessionEntries = new Dictionary<string, SessionEntry>();
        private List<SessionInfo> availableSessions = new List<SessionInfo>();

        private Dictionary<int, int> dic = new Dictionary<int, int>() {
            {0, 5}, {1, 10}, {2, 20}, {3, 50}, {4, 100}
        };

        private int selectedStake = 0;
        private int currentChannel = 1;
        private bool buttonsSpawned = false;

        public int SelectedStake => selectedStake;
        public int CurrentChannel => currentChannel;
        public int MaxChannels => maxChannels;

        public void SetNetworkControllerForTest( TienLenNetworkController controller ) {
            this.networkController = controller;
        }

        public void SetSelectedStake( int stake ) {
            selectedStake = stake;
        }

        public void InitializeChannelUIForTest( TMP_Dropdown dropdown, Button nextBtn, Button prevBtn, TMP_Text text, int max = 5 ) {
            this.channelDropdown = dropdown;
            this.nextChannelButton = nextBtn;
            this.prevChannelButton = prevBtn;
            this.channelText = text;
            this.maxChannels = max;
            InitializeChannelUI();
        }

        private void Awake() {
            if ( selectedStake == 0 && dic.TryGetValue(0, out int defaultStake) ) {
                selectedStake = defaultStake;
            }
            InitializeChannelUI();
        }

        void Start() {
            SpawnButton();
        }

        private async void OnEnable() {
            if ( closeButton != null ) {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }

            if ( networkController != null ) {
                networkController.SessionListUpdated += OnSessionListUpdated;
            }

            if ( selectedStake == 0 && dic.TryGetValue(0, out int defaultStake) ) {
                selectedStake = defaultStake;
            }

            UpdateChannelUI();
            await SwitchLobby(selectedStake, currentChannel);
        }

        private void OnDisable() {
            if ( closeButton != null ) {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            if ( networkController != null ) {
                networkController.SessionListUpdated -= OnSessionListUpdated;
            }

            ClearSessionEntries();
            availableSessions.Clear();
        }

        private void OnDestroy() {
            if ( channelDropdown != null ) {
                channelDropdown.onValueChanged.RemoveListener(OnChannelDropdownChanged);
            }
            if ( nextChannelButton != null ) {
                nextChannelButton.onClick.RemoveListener(NextChannel);
            }
            if ( prevChannelButton != null ) {
                prevChannelButton.onClick.RemoveListener(PrevChannel);
            }
        }

        private void InitializeChannelUI() {
            if ( channelDropdown != null ) {
                channelDropdown.ClearOptions();
                var options = new List<string>();
                for ( int i = 1; i <= maxChannels; i++ ) {
                    options.Add($"Channel {i}");
                }
                channelDropdown.AddOptions(options);
                channelDropdown.SetValueWithoutNotify(currentChannel - 1);
                channelDropdown.onValueChanged.AddListener(OnChannelDropdownChanged);
            }

            if ( nextChannelButton != null ) {
                nextChannelButton.onClick.AddListener(NextChannel);
            }

            if ( prevChannelButton != null ) {
                prevChannelButton.onClick.AddListener(PrevChannel);
            }

            UpdateChannelUI();
        }

        private void OnCloseButtonClicked() {
            gameObject.SetActive(false);
        }

        public void SpawnButton() {
            if ( buttonsSpawned || betButtonPrefab == null || betButtonContainer == null ) return;

            for ( int i = 0; i < dic.Count; i++ ) {
                var button = Instantiate(betButtonPrefab, betButtonContainer, false);

                BetButtonScript betButtonScript = button.GetComponent<BetButtonScript>();
                if ( betButtonScript != null ) {
                    betButtonScript.Init(i, dic[i].ToString(), this);
                }
            }
            buttonsSpawned = true;
        }

        public async void FindTable( int index ) {
            if ( !dic.TryGetValue(index, out int newStake) ) return;
            if ( newStake == selectedStake && networkController != null && networkController.CurrentLobbyId == TienLenNetworkController.GetLobbyId(selectedStake, currentChannel) ) {
                return;
            }

            selectedStake = newStake;
            await SwitchLobby(selectedStake, currentChannel);
        }

        public async UniTask SwitchLobby( int stake, int channel = 1 ) {
            selectedStake = stake;
            currentChannel = Mathf.Clamp(channel, 1, maxChannels);

            UpdateChannelUI();
            ClearSessionEntries();
            availableSessions.Clear();

            if ( networkController != null ) {
                await networkController.JoinBetLobby(selectedStake, currentChannel);
            }
        }

        public void SetChannel( int channel ) {
            int clampedChannel = Mathf.Clamp(channel, 1, maxChannels);
            if ( clampedChannel == currentChannel && networkController != null && networkController.CurrentLobbyId == TienLenNetworkController.GetLobbyId(selectedStake, currentChannel) ) {
                return;
            }

            currentChannel = clampedChannel;
            UpdateChannelUI();
            _ = SwitchLobby(selectedStake, currentChannel);
        }

        public void NextChannel() {
            if ( currentChannel < maxChannels ) {
                SetChannel(currentChannel + 1);
            }
        }

        public void PrevChannel() {
            if ( currentChannel > 1 ) {
                SetChannel(currentChannel - 1);
            }
        }

        private void OnChannelDropdownChanged( int index ) {
            SetChannel(index + 1);
        }

        private void UpdateChannelUI() {
            if ( channelText != null ) {
                channelText.text = $"Channel {currentChannel}";
            }
            if ( channelDropdown != null && channelDropdown.value != currentChannel - 1 ) {
                channelDropdown.SetValueWithoutNotify(currentChannel - 1);
            }
            if ( nextChannelButton != null ) {
                nextChannelButton.interactable = currentChannel < maxChannels;
            }
            if ( prevChannelButton != null ) {
                prevChannelButton.interactable = currentChannel > 1;
            }
        }

        private void ClearSessionEntries() {
            foreach ( var entry in sessionEntries.Values ) {
                if ( entry != null && entry.GameObject != null ) {
                    Destroy(entry.GameObject);
                }
            }
            sessionEntries.Clear();
        }

        void OnSessionListUpdated( List<SessionInfo> sessionList ) {
            availableSessions = sessionList ?? new List<SessionInfo>();
            RenderSession();
        }

        private void RenderSession() {
            var existingSessionKeys = new HashSet<string>(sessionEntries.Keys);

            foreach ( var session in availableSessions ) {
                if ( !session.IsOpen || !session.IsVisible ) continue;
                if ( !TryGetStake(session, out int roomStake) ) continue;
                if ( roomStake != selectedStake ) continue;

                existingSessionKeys.Remove(session.Name);

                if ( sessionEntries.TryGetValue(session.Name, out var entry) ) {
                    var script = entry.TableButtonScript;

                    if ( script != null && script.playerCount != session.PlayerCount ) {
                        script.UpdatePlayerCount(session.PlayerCount);
                    }
                }
                else {
                    if ( sessionButtonPrefab == null || sessionButtonContainer == null ) continue;

                    var button = Instantiate(sessionButtonPrefab, sessionButtonContainer, false);
                    var script = button.GetComponent<TableButtonScript>();
                    if ( script != null ) {
                        script.Init(session.Name, roomStake, networkController, session.PlayerCount, session.MaxPlayers);
                    }

                    sessionEntries[session.Name] = new SessionEntry {
                        GameObject = button,
                        TableButtonScript = script,
                        index = sessionEntries.Count
                    };
                }
            }

            // Clean up removed / closed session entries
            foreach ( var removedKey in existingSessionKeys ) {
                if ( sessionEntries.TryGetValue(removedKey, out var entry) ) {
                    if ( entry.GameObject != null ) {
                        Destroy(entry.GameObject);
                    }
                    sessionEntries.Remove(removedKey);
                }
            }
        }

        private bool TryGetStake( SessionInfo session, out int stake ) {
            stake = 0;

            if ( session.Properties == null )
                return false;

            if ( !session.Properties.TryGetValue("BetStake", out var property) &&
                 !session.Properties.TryGetValue("Stake", out property) ) {
                return false;
            }

            try {
                stake = Convert.ToInt32(property.PropertyValue);
                return true;
            }
            catch ( Exception ) {
                return false;
            }
        }
    }
}