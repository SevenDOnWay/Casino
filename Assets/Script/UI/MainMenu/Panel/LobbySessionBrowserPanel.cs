using Assets.Script.NetWorkScript;
using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
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

        private Dictionary<string, SessionEntry> sessionEntries = new Dictionary<string, SessionEntry>();
        private List<SessionInfo> availableSessions = new List<SessionInfo>();
        private List<BetButtonScript> betButtons = new List<BetButtonScript>();

        private Dictionary<int, int> dic = new Dictionary<int, int>() {
            {0, 5}, {1, 10}, {2, 20}, {3, 50}, {4, 100}
        };

        private Dictionary<int, int> channelCounts = new Dictionary<int, int>();
        private Dictionary<string, int> channelSessionCounts = new Dictionary<string, int>();

        public const int MaxSessionsPerChannel = 30;

        private int selectedStake = 0;
        private int currentChannel = 1;
        private bool buttonsSpawned = false;

        public int SelectedStake => selectedStake;
        public int CurrentChannel => currentChannel;
        public IReadOnlyList<BetButtonScript> BetButtons => betButtons;

        public void SetNetworkControllerForTest( TienLenNetworkController controller ) {
            this.networkController = controller;
        }

        public void SetSelectedStake( int stake ) {
            selectedStake = stake;
        }

        public void SetChannelSessionCountForTest( string lobbyId, int count ) {
            channelSessionCounts[lobbyId] = count;
        }

        private void Awake() {
            if ( selectedStake == 0 && dic.TryGetValue(0, out int defaultStake) ) {
                selectedStake = defaultStake;
            }
        }

        void Start() {
            SpawnButton();
        }

        private void OnEnable() {
            if ( closeButton != null ) {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }

            if ( networkController != null ) {
                networkController.SessionListUpdated += OnSessionListUpdated;
            }

            SpawnButton();

            if ( selectedStake == 0 && dic.TryGetValue(0, out int defaultStake) ) {
                selectedStake = defaultStake;
            }

            int betIndex = GetBetIndex(selectedStake);
            FindTable(betIndex);
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

        private void OnCloseButtonClicked() {
            gameObject.SetActive(false);
        }

        public void SpawnButton() {
            if ( buttonsSpawned || betButtonPrefab == null || betButtonContainer == null ) return;

            betButtons.Clear();
            for ( int i = 0; i < dic.Count; i++ ) {
                var button = Instantiate(betButtonPrefab, betButtonContainer, false);

                BetButtonScript betButtonScript = button.GetComponent<BetButtonScript>();
                if ( betButtonScript != null ) {
                    betButtonScript.Init(i, dic[i].ToString(), this);
                    betButtons.Add(betButtonScript);
                }
            }
            buttonsSpawned = true;
        }

        public int GetBetIndex( int stake ) {
            foreach ( var kvp in dic ) {
                if ( kvp.Value == stake ) return kvp.Key;
            }
            return 0;
        }

        public int GetChannelCountForStake( int stake ) {
            if ( !channelCounts.TryGetValue(stake, out int count) || count < 1 ) {
                count = 1;
                channelCounts[stake] = count;
            }
            return count;
        }

        public List<string> GetChannelOptions( int betIndex ) {
            if ( !dic.TryGetValue(betIndex, out int stake) ) {
                stake = 5;
            }

            int count = GetChannelCountForStake(stake);
            var options = new List<string>();
            for ( int i = 1; i <= count; i++ ) {
                options.Add($"Channel {i}");
            }
            return options;
        }

        public int GetOptimizedChannel( int stake ) {
            int count = GetChannelCountForStake(stake);

            // Find the first channel with capacity (< 30 sessions)
            for ( int ch = 1; ch <= count; ch++ ) {
                string lobbyId = TienLenNetworkController.GetLobbyId(stake, ch);
                int sessions = channelSessionCounts.TryGetValue(lobbyId, out int s) ? s : 0;
                if ( sessions < MaxSessionsPerChannel ) {
                    return ch;
                }
            }

            // All existing channels are full: auto-shard by creating a new channel
            return CreateNewChannel(stake);
        }

        public int CreateNewChannel( int stake ) {
            int newChannel = GetChannelCountForStake(stake) + 1;
            channelCounts[stake] = newChannel;
            Debug.Log($"[Automatic Sharding] Created new Channel {newChannel} for Stake {stake}");
            return newChannel;
        }

        public void FindTable( int index ) {
            if ( !dic.TryGetValue(index, out selectedStake) ) return;

            int optChannel = GetOptimizedChannel(selectedStake);
            currentChannel = optChannel;

            // Enable channel dropdown for the selected bet button and hide for others
            for ( int i = 0; i < betButtons.Count; i++ ) {
                var btn = betButtons[i];
                if ( btn != null ) {
                    btn.SetChannelActive(btn.Index == index, optChannel);
                }
            }

            _ = SwitchLobbyByStake(selectedStake, optChannel);
        }

        public UniTask SwitchLobby( int betIndex, int channelIndex ) {
            if ( !dic.TryGetValue(betIndex, out int stake) ) return UniTask.CompletedTask;

            int targetChannel = channelIndex + 1;
            return SwitchLobbyByStake(stake, targetChannel);
        }

        public async UniTask SwitchLobbyByStake( int stake, int channel = 1 ) {
            selectedStake = stake;
            currentChannel = Mathf.Max(1, channel);

            ClearSessionEntries();
            availableSessions.Clear();

            if ( networkController != null ) {
                await networkController.JoinBetLobby(selectedStake, currentChannel);
            }
        }

        public int MaxChannels => Mathf.Max(5, GetChannelCountForStake(selectedStake));

        public void SetChannel( int channel ) {
            int clampedChannel = Mathf.Clamp(channel, 1, MaxChannels);
            if ( clampedChannel == currentChannel && networkController != null && networkController.CurrentLobbyId == TienLenNetworkController.GetLobbyId(selectedStake, currentChannel) ) {
                return;
            }

            currentChannel = clampedChannel;
            _ = SwitchLobbyByStake(selectedStake, currentChannel);
        }

        public void NextChannel() {
            if ( currentChannel < MaxChannels ) {
                SetChannel(currentChannel + 1);
            }
        }

        public void PrevChannel() {
            if ( currentChannel > 1 ) {
                SetChannel(currentChannel - 1);
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

            string currentLobbyId = TienLenNetworkController.GetLobbyId(selectedStake, currentChannel);
            channelSessionCounts[currentLobbyId] = availableSessions.Count;

            // Check if current channel reached capacity and auto-create next channel if needed
            if ( availableSessions.Count >= MaxSessionsPerChannel ) {
                int highestChannel = GetChannelCountForStake(selectedStake);
                if ( currentChannel >= highestChannel ) {
                    CreateNewChannel(selectedStake);

                    int activeBetIndex = GetBetIndex(selectedStake);
                    var activeBtn = betButtons.Find(b => b != null && b.Index == activeBetIndex);
                    if ( activeBtn != null ) {
                        activeBtn.BuildChannel(currentChannel);
                    }
                }
            }

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
                        script.Init(session.Name, roomStake, networkController, session.PlayerCount, session.MaxPlayers, currentChannel);
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