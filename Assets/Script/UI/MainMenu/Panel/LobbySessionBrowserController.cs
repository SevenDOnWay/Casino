using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Assets.Script.UI.MainMenu.Panel {
    public class LobbySessionBrowserController {
        [Header("Dependencies")]
        private readonly TienLenNetworkController networkController;
        private readonly ILobbyRestService lobbyRestService;
        private readonly INetworkConfig networkConfig;

        private ILobbySessionBrowserView currentView;
        private CancellationTokenSource pollingCts;
        private bool isFetching = false;

        private readonly Dictionary<int, int> stakeOptions = new Dictionary<int, int>() {
            {0, 5}, {1, 10}, {2, 20}, {3, 50}, {4, 100}
        };

        private readonly Dictionary<int, int> channelCounts = new Dictionary<int, int>();
        private readonly Dictionary<string, int> channelSessionCounts = new Dictionary<string, int>();

        public const int MaxSessionsPerChannel = 30;
        public const float AutoRefreshIntervalSeconds = 4.0f;

        private int selectedStakeIndex = 0;
        private int selectedStake = 5;
        private int currentChannel = 1;

        public int SelectedStake => selectedStake;
        public int SelectedStakeIndex => selectedStakeIndex;
        public int CurrentChannel => currentChannel;
        public int MaxChannels => Mathf.Max(5, GetChannelCountForStake(selectedStake));
        public bool IsPolling => pollingCts != null && !pollingCts.IsCancellationRequested;

        [Inject]
        public LobbySessionBrowserController(
            TienLenNetworkController networkController,
            ILobbyRestService lobbyRestService,
            INetworkConfig networkConfig
        ) {
            this.networkController = networkController;
            this.lobbyRestService = lobbyRestService;
            this.networkConfig = networkConfig;
        }

        public void AttachView( ILobbySessionBrowserView view ) {
            currentView = view;
        }

        public void DetachView() {
            StopPolling();
            currentView = null;
        }

        public void OnViewEnabled( ILobbySessionBrowserView view ) {
            currentView = view;

            if ( selectedStakeIndex == 0 && stakeOptions.TryGetValue(0, out int defaultStake) ) {
                selectedStake = defaultStake;
            }

            currentView?.SetSelectedStakeButton(selectedStakeIndex);
            _ = InitializeAndFetchAsync(selectedStakeIndex);
        }

        public void OnViewDisabled() {
            StopPolling();
            currentView = null;
        }

        public async UniTask InitializeAndFetchAsync( int betIndex ) {
            if ( !stakeOptions.TryGetValue(betIndex, out int stake) ) {
                stake = 5;
                betIndex = 0;
            }

            selectedStakeIndex = betIndex;
            selectedStake = stake;

            currentView?.SetSelectedStakeButton(selectedStakeIndex);
            currentView?.SetLoading(true);

            int optChannel = 1;
            int totalChannels = GetChannelCountForStake(stake);

            if ( lobbyRestService != null ) {
                try {
                    var channelInfo = await lobbyRestService.GetChannelsAsync(stake);
                    if ( channelInfo != null ) {
                        int maxCh = Mathf.Max(channelInfo.channels != null ? channelInfo.channels.Count : 1, channelInfo.recommendedChannel);
                        channelCounts[stake] = Mathf.Max(channelCounts.TryGetValue(stake, out int cur) ? cur : 1, maxCh);
                        totalChannels = channelCounts[stake];
                        optChannel = channelInfo.recommendedChannel > 0 ? channelInfo.recommendedChannel : 1;
                    }
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[LobbySessionBrowserController] GetChannelsAsync error: {ex.Message}");
                    optChannel = GetOptimizedChannel(stake);
                    totalChannels = GetChannelCountForStake(stake);
                }
            }
            else {
                optChannel = GetOptimizedChannel(stake);
                totalChannels = GetChannelCountForStake(stake);
            }

            currentChannel = optChannel;
            currentView?.UpdateChannelDropdown(totalChannels, currentChannel);

            await FetchSessionsAndRenderAsync(selectedStake, currentChannel);
            currentView?.SetLoading(false);

            StartPolling();
        }

        public async UniTask SelectStake( int betIndex ) {
            if ( !stakeOptions.TryGetValue(betIndex, out int stake) ) return;

            selectedStakeIndex = betIndex;
            selectedStake = stake;

            currentView?.SetSelectedStakeButton(selectedStakeIndex);
            currentView?.SetLoading(true);

            int optChannel = GetOptimizedChannel(selectedStake);
            int totalChannels = GetChannelCountForStake(selectedStake);

            if ( lobbyRestService != null ) {
                try {
                    var channelInfo = await lobbyRestService.GetChannelsAsync(selectedStake);
                    if ( channelInfo != null ) {
                        int maxCh = Mathf.Max(channelInfo.channels != null ? channelInfo.channels.Count : 1, channelInfo.recommendedChannel);
                        channelCounts[selectedStake] = Mathf.Max(channelCounts.TryGetValue(selectedStake, out int cur) ? cur : 1, maxCh);
                        totalChannels = channelCounts[selectedStake];
                        optChannel = channelInfo.recommendedChannel > 0 ? channelInfo.recommendedChannel : 1;
                    }
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[LobbySessionBrowserController] SelectStake GetChannels fallback: {ex.Message}");
                }
            }

            currentChannel = optChannel;
            currentView?.UpdateChannelDropdown(totalChannels, currentChannel);

            await FetchSessionsAndRenderAsync(selectedStake, currentChannel);
            currentView?.SetLoading(false);

            RestartPolling();
        }

        public async UniTask SelectChannel( int channel ) {
            currentChannel = Mathf.Max(1, channel);

            currentView?.SetLoading(true);
            await FetchSessionsAndRenderAsync(selectedStake, currentChannel);
            currentView?.SetLoading(false);

            RestartPolling();
        }

        public async UniTask SwitchLobbyByStake( int stake, int channel = 1 ) {
            selectedStake = stake;
            selectedStakeIndex = GetBetIndex(stake);
            currentChannel = Mathf.Max(1, channel);

            currentView?.SetSelectedStakeButton(selectedStakeIndex);
            currentView?.UpdateChannelDropdown(GetChannelCountForStake(stake), currentChannel);

            await FetchSessionsAndRenderAsync(selectedStake, currentChannel);
        }

        public UniTask SwitchLobby( int betIndex, int channelIndex ) {
            if ( !stakeOptions.TryGetValue(betIndex, out int stake) ) return UniTask.CompletedTask;
            int targetChannel = channelIndex + 1;
            return SwitchLobbyByStake(stake, targetChannel);
        }

        public UniTask ChangeToLobby( BetButtonScript btn ) {
            if ( btn == null ) return UniTask.CompletedTask;
            return SelectStake(btn.Index);
        }

        public UniTask ChangeToSpecificLobby( BetButtonScript btn, int channelDropdownValue ) {
            int targetChannel = channelDropdownValue + 1;
            if ( btn != null ) {
                selectedStakeIndex = btn.Index;
                if ( stakeOptions.TryGetValue(btn.Index, out int stake) ) {
                    selectedStake = stake;
                }
            }
            return SelectChannel(targetChannel);
        }

        public async UniTask RefreshCurrentLobbyAsync() {
            if ( isFetching ) return;
            await FetchSessionsAndRenderAsync(selectedStake, currentChannel);
        }

        private async UniTask FetchSessionsAndRenderAsync( int stake, int channel ) {
            isFetching = true;
            try {
                if ( networkController != null ) {
                    await networkController.JoinBetLobby(stake, channel);
                }

                if ( lobbyRestService != null ) {
                    var sessions = await lobbyRestService.GetSessionsAsync(stake, channel);
                    sessions ??= new List<LobbySessionDto>();

                    string lobbyId = TienLenNetworkController.GetLobbyId(stake, channel);
                    channelSessionCounts[lobbyId] = sessions.Count;

                    // Auto-sharding if channel reaches capacity
                    if ( sessions.Count >= MaxSessionsPerChannel ) {
                        int highestChannel = GetChannelCountForStake(stake);
                        if ( channel >= highestChannel ) {
                            CreateNewChannel(stake);
                            currentView?.UpdateChannelDropdown(GetChannelCountForStake(stake), channel);
                        }
                    }

                    currentView?.RenderSessions(sessions);
                }
            }
            catch ( Exception ex ) {
                Debug.LogError($"[LobbySessionBrowserController] FetchSessions error: {ex.Message}");
                currentView?.ShowError(ex.Message);
            }
            finally {
                isFetching = false;
            }
        }

        private void StartPolling() {
            StopPolling();
            pollingCts = new CancellationTokenSource();
            _ = AutoRefreshLoopAsync(pollingCts.Token);
        }

        private void RestartPolling() {
            StartPolling();
        }

        public void StopPolling() {
            if ( pollingCts != null ) {
                try {
                    if ( !pollingCts.IsCancellationRequested ) {
                        pollingCts.Cancel();
                    }
                    pollingCts.Dispose();
                }
                catch ( ObjectDisposedException ) { }
                pollingCts = null;
            }
        }

        private async UniTaskVoid AutoRefreshLoopAsync( CancellationToken token ) {
            while ( !token.IsCancellationRequested ) {
                try {
                    await UniTask.Delay(TimeSpan.FromSeconds(AutoRefreshIntervalSeconds), cancellationToken: token);
                }
                catch ( OperationCanceledException ) {
                    break;
                }

                if ( token.IsCancellationRequested ) break;

                // Silent auto-refresh (no full-screen loading spinner)
                if ( !isFetching && lobbyRestService != null ) {
                    try {
                        var sessions = await lobbyRestService.GetSessionsAsync(selectedStake, currentChannel);
                        if ( !token.IsCancellationRequested && sessions != null ) {
                            string lobbyId = TienLenNetworkController.GetLobbyId(selectedStake, currentChannel);
                            channelSessionCounts[lobbyId] = sessions.Count;
                            currentView?.RenderSessions(sessions);
                        }
                    }
                    catch ( Exception ex ) {
                        Debug.LogWarning($"[LobbySessionBrowserController] Auto-refresh tick failed: {ex.Message}");
                    }
                }
            }
        }

        public int GetBetIndex( int stake ) {
            foreach ( var kvp in stakeOptions ) {
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
            if ( !stakeOptions.TryGetValue(betIndex, out int stake) ) {
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

            for ( int ch = 1; ch <= count; ch++ ) {
                string lobbyId = TienLenNetworkController.GetLobbyId(stake, ch);
                int sessions = channelSessionCounts.TryGetValue(lobbyId, out int s) ? s : 0;
                if ( sessions < MaxSessionsPerChannel ) {
                    return ch;
                }
            }

            return CreateNewChannel(stake);
        }

        public int CreateNewChannel( int stake ) {
            int newChannel = GetChannelCountForStake(stake) + 1;
            channelCounts[stake] = newChannel;
            Debug.Log($"[Automatic Sharding] Created new Channel {newChannel} for Stake {stake}");
            return newChannel;
        }

        public void SetChannel( int channel ) {
            int clampedChannel = Mathf.Clamp(channel, 1, MaxChannels);
            if ( clampedChannel == currentChannel && networkController != null && networkController.CurrentLobbyId == TienLenNetworkController.GetLobbyId(selectedStake, currentChannel) ) {
                return;
            }

            _ = SelectChannel(clampedChannel);
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

        public void SetSelectedStakeForTest( int stake ) {
            selectedStake = stake;
            selectedStakeIndex = GetBetIndex(stake);
        }

        public void SetChannelSessionCountForTest( string lobbyId, int count ) {
            channelSessionCounts[lobbyId] = count;
        }
    }
}
