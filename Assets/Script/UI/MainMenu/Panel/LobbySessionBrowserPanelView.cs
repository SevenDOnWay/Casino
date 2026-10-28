using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.UI.Components;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu.Panel {
    public class LobbySessionBrowserPanelView : MonoBehaviour, ILobbySessionBrowserView {

        private sealed class SessionEntry {
            public GameObject GameObject;
            public TableButtonScript TableButtonScript;
            public int index;
        }

        [Header("Dependencies")]
        [Inject] TienLenNetworkController networkController;
        [Inject] ILobbyRestService lobbyRestService;
        [Inject] INetworkConfig networkConfig;
        [Inject] LobbySessionBrowserController controller;
        [Inject] IPlayerProfileService playerProfileService;

        [Header("Main UI")]
        [SerializeField] Button closeButton;
        [SerializeField] GameObject LoadingScreen;
        [SerializeField] GameObject betButtonPrefab;
        [SerializeField] GameObject sessionButtonPrefab;
        [SerializeField] Transform betButtonContainer;
        [SerializeField] Transform sessionButtonContainer;

        private BetButtonScript currentSelectedButton = null;

        private readonly Dictionary<int, int> stakeDictionary = new Dictionary<int, int>() {
            {0, 5}, {1, 10}, {2, 20}, {3, 50}, {4, 100}
        };

        private readonly Dictionary<string, SessionEntry> sessionEntries = new Dictionary<string, SessionEntry>();
        private readonly Dictionary<int, BetButtonScript> betButtons = new Dictionary<int, BetButtonScript>();

        private bool buttonsSpawned = false;
        private bool? useWebSocketApiOverride = null;

        public LobbySessionBrowserController Controller => controller;
        public int SelectedStake => controller != null ? controller.SelectedStake : (stakeDictionary.TryGetValue(0, out int s) ? s : 5);
        public int CurrentChannel => controller != null ? controller.CurrentChannel : 1;
        public int MaxChannels => controller != null ? controller.MaxChannels : 5;

        public bool UseWebSocketApi {
            get {
                if ( useWebSocketApiOverride.HasValue ) return useWebSocketApiOverride.Value;
                if ( networkConfig != null ) return networkConfig.UseWebSocketTransport;
                return false;
            }
        }

        private void Awake() {
            EnsureController();
        }

        private void EnsureController() {
            if ( controller == null ) {
                controller = new LobbySessionBrowserController(networkController, lobbyRestService, networkConfig);
            }
        }

        private void OnEnable() {
            EnsureController();

            if ( closeButton != null ) {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }

            SpawnButton();
            controller?.OnViewEnabled(this);
        }

        private void OnDisable() {
            if ( closeButton != null ) {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            controller?.OnViewDisabled();
            SetLoading(false);
            ClearSessionEntries();
        }

        private void OnCloseButtonClicked() {
            gameObject.SetActive(false);
        }

        public void SpawnButton() {
            if ( buttonsSpawned && betButtons.Count > 0 ) return;
            if ( betButtonPrefab == null || betButtonContainer == null ) return;

            betButtons.Clear();
            for ( int i = 0; i < stakeDictionary.Count; i++ ) {
                var button = Instantiate(betButtonPrefab, betButtonContainer, false);
                var betButtonScript = button.GetComponent<BetButtonScript>();
                if ( betButtonScript != null ) {
                    betButtonScript.Init(i, stakeDictionary[i].ToString(), controller);
                    betButtons[i] = betButtonScript;
                }
            }
            buttonsSpawned = true;
        }

        #region ILobbySessionBrowserView Implementation

        public void SetLoading( bool isLoading ) {
            LoadingScreen?.SetActive(isLoading);
        }

        public void SetSelectedStakeButton( int betIndex ) {
            currentSelectedButton?.EnableDropDown(false);

            currentSelectedButton = betButtons.TryGetValue(betIndex, out var newSelectedButton) ? newSelectedButton : null;
            currentSelectedButton?.EnableDropDown(true);
        }

        public void UpdateChannelDropdown( int channelCount, int selectedChannel ) {
            int selectedBetIndex = controller != null ? controller.SelectedStakeIndex : 0;
            var activeBtn = betButtons.GetValueOrDefault(selectedBetIndex);
            if ( activeBtn != null ) {
                activeBtn.BuildChannel(channelCount, selectedChannel);
            }
        }

        public void RenderSessions( List<LobbySessionDto> sessions ) {
            sessions ??= new List<LobbySessionDto>();
            int currentSelectedStake = SelectedStake;
            var existingKeys = new HashSet<string>(sessionEntries.Keys);

            foreach ( var session in sessions ) {
                if ( string.IsNullOrEmpty(session.sessionName) ) continue;
                if ( session.betStake != currentSelectedStake ) continue;

                existingKeys.Remove(session.sessionName);
                CreateOrUpdateTableEntry(session.sessionName, session.betStake, session.channel, session.playerCount, session.maxPlayers);
            }

            foreach ( var removedKey in existingKeys ) {
                if ( sessionEntries.TryGetValue(removedKey, out var entry) ) {
                    if ( entry.GameObject != null ) {
                        Destroy(entry.GameObject);
                    }
                    sessionEntries.Remove(removedKey);
                }
            }
        }

        public void ShowError( string message ) {
            Debug.LogError($"[LobbySessionBrowserPanelView] Error: {message}");
        }

        #endregion

        private void CreateOrUpdateTableEntry( string sessionName, int stake, int channel, int playerCount, int maxPlayers ) {
            if ( string.IsNullOrEmpty(sessionName) ) return;

            if ( sessionEntries.TryGetValue(sessionName, out var entry) ) {
                var script = entry.TableButtonScript;
                if ( script != null && script.playerCount != playerCount ) {
                    script.UpdatePlayerCount(playerCount);
                }
            }
            else {
                if ( sessionButtonPrefab == null || sessionButtonContainer == null ) return;

                var button = Instantiate(sessionButtonPrefab, sessionButtonContainer, false);
                var script = button.GetComponent<TableButtonScript>();
                if ( script != null ) {
                    script.Init(sessionName, stake, networkController, playerCount, maxPlayers, channel, playerProfileService, ShowError);
                }

                sessionEntries[sessionName] = new SessionEntry {
                    GameObject = button,
                    TableButtonScript = script,
                    index = sessionEntries.Count
                };
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
    }
}
