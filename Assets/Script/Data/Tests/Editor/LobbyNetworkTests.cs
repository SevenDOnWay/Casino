using Assets.Script;
using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.UI.Components;
using Assets.Script.UI.MainMenu;
using Assets.Script.UI.MainMenu.Panel;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class LobbyNetworkTests {

        #region TienLenNetworkController & Lobby ID Tests

        [Test]
        public void GetLobbyId_DefaultChannel_ReturnsExpectedFormat() {
            string lobbyId = TienLenNetworkController.GetLobbyId(5);
            Assert.AreEqual("Bet_5_Ch_1", lobbyId);
        }

        [Test]
        public void GetLobbyId_SpecificChannel_ReturnsExpectedFormat() {
            string lobbyId = TienLenNetworkController.GetLobbyId(20, 3);
            Assert.AreEqual("Bet_20_Ch_3", lobbyId);
        }

        [Test]
        public void GetLobbyId_InvalidChannel_DefaultsToChannelOne() {
            string lobbyZero = TienLenNetworkController.GetLobbyId(50, 0);
            string lobbyNeg = TienLenNetworkController.GetLobbyId(100, -2);

            Assert.AreEqual("Bet_50_Ch_1", lobbyZero);
            Assert.AreEqual("Bet_100_Ch_1", lobbyNeg);
        }

        [Test]
        public void NetworkController_InitialLobbyState_IsEmpty() {
            var go = new GameObject("NetworkControllerTest");
            var controller = go.AddComponent<TienLenNetworkController>();

            Assert.IsNull(controller.CurrentLobbyId);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void NetworkController_WithMockLobbyService_CanAssign() {
            var go = new GameObject("NetworkControllerTest");
            var controller = go.AddComponent<TienLenNetworkController>();
            var mockService = new MockLobbyRestService();

            controller.SetLobbyRestServiceForTest(mockService);
            Assert.IsNotNull(controller);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region LobbySessionBrowserController Tests

        [Test]
        public void Controller_InitialState_MatchesExpectedDefaults() {
            var controller = new LobbySessionBrowserController(null, null, null);

            Assert.AreEqual(5, controller.SelectedStake);
            Assert.AreEqual(0, controller.SelectedStakeIndex);
            Assert.AreEqual(1, controller.CurrentChannel);
            Assert.GreaterOrEqual(controller.MaxChannels, 5);
            Assert.IsFalse(controller.IsPolling);
        }

        [Test]
        public void Controller_SetSelectedStake_UpdatesPropertyAndIndex() {
            var controller = new LobbySessionBrowserController(null, null, null);

            controller.SetSelectedStakeForTest(50);
            Assert.AreEqual(50, controller.SelectedStake);
            Assert.AreEqual(3, controller.SelectedStakeIndex);

            controller.SetSelectedStakeForTest(10);
            Assert.AreEqual(10, controller.SelectedStake);
            Assert.AreEqual(1, controller.SelectedStakeIndex);
        }

        [Test]
        public void Controller_ChannelNavigation_ClampsWithinBounds() {
            var controller = new LobbySessionBrowserController(null, null, null);

            controller.SetChannel(999);
            Assert.AreEqual(controller.MaxChannels, controller.CurrentChannel);

            controller.SetChannel(-5);
            Assert.AreEqual(1, controller.CurrentChannel);
        }

        [Test]
        public void Controller_NextPrevChannel_StepsCorrectly() {
            var controller = new LobbySessionBrowserController(null, null, null);

            Assert.AreEqual(1, controller.CurrentChannel);

            controller.NextChannel();
            Assert.AreEqual(2, controller.CurrentChannel);

            controller.NextChannel();
            Assert.AreEqual(3, controller.CurrentChannel);

            controller.PrevChannel();
            Assert.AreEqual(2, controller.CurrentChannel);

            controller.PrevChannel();
            Assert.AreEqual(1, controller.CurrentChannel);

            // Cannot go below 1
            controller.PrevChannel();
            Assert.AreEqual(1, controller.CurrentChannel);
        }

        [Test]
        public void Controller_AutoShards_WhenChannelReachesCapacity() {
            var controller = new LobbySessionBrowserController(null, null, null);

            // Initially, stake 5 has 1 channel and optimized channel is 1
            Assert.AreEqual(1, controller.GetChannelCountForStake(5));
            Assert.AreEqual(1, controller.GetOptimizedChannel(5));

            // Simulate Channel 1 is full (30 sessions)
            string ch1Lobby = TienLenNetworkController.GetLobbyId(5, 1);
            controller.SetChannelSessionCountForTest(ch1Lobby, 30);

            // Optimized channel should auto-shard to Channel 2
            int optChannel = controller.GetOptimizedChannel(5);
            Assert.AreEqual(2, optChannel);
            Assert.AreEqual(2, controller.GetChannelCountForStake(5));

            // Options should now include Channel 1 and Channel 2
            var options = controller.GetChannelOptions(0); // Bet index 0 is Stake 5
            Assert.AreEqual(2, options.Count);
            Assert.AreEqual("Channel 1", options[0]);
            Assert.AreEqual("Channel 2", options[1]);
        }

        [Test]
        public async System.Threading.Tasks.Task Controller_OnViewEnabled_FetchesSessionsAndStartsPolling() {
            var mockService = new MockLobbyRestService();
            mockService.SessionsToReturn.Add(new LobbySessionDto {
                sessionName = "TableOnLoad",
                betStake = 5,
                channel = 2,
                playerCount = 1,
                maxPlayers = 4
            });

            var controller = new LobbySessionBrowserController(null, mockService, null);
            var mockView = new MockLobbySessionBrowserView();

            controller.OnViewEnabled(mockView);

            // Allow initial async fetch to complete
            await UniTask.Yield();
            await UniTask.Delay(100);

            Assert.AreEqual(0, mockView.SelectedStakeIndex);
            Assert.GreaterOrEqual(mockService.GetSessionsCallCount, 1);
            Assert.GreaterOrEqual(mockView.RenderSessionsCallCount, 1);
            Assert.IsNotNull(mockView.LastRenderedSessions);
            Assert.AreEqual("TableOnLoad", mockView.LastRenderedSessions[0].sessionName);
            Assert.IsTrue(controller.IsPolling);

            controller.OnViewDisabled();
            Assert.IsFalse(controller.IsPolling);
        }

        [Test]
        public async System.Threading.Tasks.Task Controller_SelectStake_FetchesSessionsAndUpdatesView() {
            var mockService = new MockLobbyRestService();
            var controller = new LobbySessionBrowserController(null, mockService, null);
            var mockView = new MockLobbySessionBrowserView();

            controller.AttachView(mockView);
            await controller.SelectStake(2); // Index 2 -> Stake 20

            Assert.AreEqual(20, controller.SelectedStake);
            Assert.AreEqual(2, mockView.SelectedStakeIndex);
            Assert.GreaterOrEqual(mockService.GetSessionsCallCount, 1);
            Assert.IsTrue(controller.IsPolling);

            controller.StopPolling();
            Assert.IsFalse(controller.IsPolling);
        }

        [Test]
        public async System.Threading.Tasks.Task Controller_SelectChannel_FetchesSessionsForTargetChannel() {
            var mockService = new MockLobbyRestService();
            var controller = new LobbySessionBrowserController(null, mockService, null);
            var mockView = new MockLobbySessionBrowserView();

            controller.AttachView(mockView);
            await controller.SelectChannel(3);

            Assert.AreEqual(3, controller.CurrentChannel);
            Assert.GreaterOrEqual(mockService.GetSessionsCallCount, 1);
            Assert.IsTrue(controller.IsPolling);

            controller.StopPolling();
            Assert.IsFalse(controller.IsPolling);
        }

        [Test]
        public async System.Threading.Tasks.Task Controller_SwitchLobbyByStake_FetchesSessionsFromApi() {
            var mockService = new MockLobbyRestService();
            mockService.SessionsToReturn.Add(new LobbySessionDto {
                sessionName = "WebRoom1",
                betStake = 5,
                channel = 1,
                playerCount = 2,
                maxPlayers = 4
            });

            var controller = new LobbySessionBrowserController(null, mockService, null);
            var mockView = new MockLobbySessionBrowserView();

            controller.AttachView(mockView);
            await controller.SwitchLobbyByStake(5, 1);

            Assert.AreEqual(5, controller.SelectedStake);
            Assert.AreEqual(0, controller.SelectedStakeIndex);
            Assert.AreEqual(1, controller.CurrentChannel);
            Assert.AreEqual(1, mockService.GetSessionsCallCount);
            Assert.AreEqual(1, mockView.RenderSessionsCallCount);
            Assert.AreEqual("WebRoom1", mockView.LastRenderedSessions[0].sessionName);
        }

        #endregion

        #region LobbySessionBrowserPanelView (View) Tests

        [Test]
        public void BrowserPanel_Implements_ILobbySessionBrowserView() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();

            Assert.IsTrue(panel is ILobbySessionBrowserView);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_UseWebSocketApi_DefaultsToFalse_WhenNoConfig() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            Assert.IsFalse(panel.UseWebSocketApi);

            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BrowserPanel_SetLoading_DrivesDynamicLoadingCircle_WhenAssigned() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            var circleGo = new GameObject("DynamicCircle");
            circleGo.transform.SetParent(panelGo.transform);
            var rect = circleGo.AddComponent<RectTransform>();
            var img = circleGo.AddComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            var cg = circleGo.AddComponent<CanvasGroup>();
            var circle = circleGo.AddComponent<DynamicLoadingCircle>();

            var field = typeof(LobbySessionBrowserPanelView).GetField("dynamicLoadingCircle", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(panel, circle);

            panel.SetLoading(true);
            Assert.IsTrue(circle.IsPlaying);

            panel.SetLoading(false);
            Assert.IsFalse(circle.IsPlaying);

            // Clean up instant to prevent background fade timer from executing after GameObject is destroyed
            circle.Stop(instant: true);
            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BrowserPanel_SetLoading_FallsBackToLoadingIndicator_WhenNoCircle() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            var indicatorGo = new GameObject("IndicatorLegacy");
            indicatorGo.SetActive(false);

            var field = typeof(LobbySessionBrowserPanelView).GetField("loadingIndicator", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(panel, indicatorGo);

            panel.SetLoading(true);
            Assert.IsTrue(indicatorGo.activeSelf);

            panel.SetLoading(false);
            Assert.IsFalse(indicatorGo.activeSelf);

            Object.DestroyImmediate(indicatorGo);
            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BrowserPanel_SetLoading_NullSafe_WhenNoUIConfigured() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            Assert.DoesNotThrow(() => {
                panel.SetLoading(true);
                panel.SetLoading(false);
            });

            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BrowserPanel_RenderSessions_NullSafe_DoesNotThrow() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            Assert.DoesNotThrow(() => {
                panel.RenderSessions(null);
                panel.RenderSessions(new List<LobbySessionDto>());
            });

            Object.DestroyImmediate(panelGo);
        }

        #endregion

        #region BetButtonScript & DynamicLoadingCircle Tests

        [Test]
        public void BetButton_InitWithController_ParsesCorrectlyAndDispatchesClick() {
            var go = new GameObject("BetButtonTest");
            var betBtn = go.AddComponent<BetButtonScript>();
            var textGo = new GameObject("TextTMP");
            textGo.transform.SetParent(go.transform);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "20";

            var controller = new LobbySessionBrowserController(null, null, null);

            betBtn.Init(2, "20", controller);
            Assert.AreEqual(2, betBtn.Index);

            // OnBetButtonClicked invokes controller.SelectStake(index)
            betBtn.OnBetButtonClicked();
            Assert.AreEqual(20, controller.SelectedStake);
            Assert.AreEqual(2, controller.SelectedStakeIndex);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BetButton_ChannelDropdown_BuildsOptionsAndDispatchesValueChange() {
            var go = new GameObject("BetButtonTest");
            var betBtn = go.AddComponent<BetButtonScript>();

            var dropdownGo = new GameObject("Dropdown");
            dropdownGo.transform.SetParent(go.transform);
            var dropdown = dropdownGo.AddComponent<TMP_Dropdown>();

            var field = typeof(BetButtonScript).GetField("channelDropDown", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(betBtn, dropdown);

            var controller = new LobbySessionBrowserController(null, null, null);
            betBtn.Init(0, "5", controller);

            betBtn.BuildChannel(3, 2);
            Assert.AreEqual(3, dropdown.options.Count);
            Assert.AreEqual("Channel 1", dropdown.options[0].text);
            Assert.AreEqual("Channel 2", dropdown.options[1].text);
            Assert.AreEqual("Channel 3", dropdown.options[2].text);
            Assert.AreEqual(1, dropdown.value); // 0-indexed: Channel 2 is index 1

            betBtn.OnChannelValueChanged(2); // value 2 -> Channel 3
            Assert.AreEqual(3, controller.CurrentChannel);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DynamicLoadingCircle_Lifecycle_PlayAndStop_UpdatesState() {
            var go = new GameObject("DynamicLoadingCircleTest");
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            var cg = go.AddComponent<CanvasGroup>();
            var circle = go.AddComponent<DynamicLoadingCircle>();

            // Play starts animation and blocks raycasts
            circle.Play();
            Assert.IsTrue(circle.IsPlaying);
            Assert.IsTrue(cg.blocksRaycasts);

            // Stop ends animation
            circle.Stop(instant: true);
            Assert.IsFalse(circle.IsPlaying);
            Assert.IsFalse(cg.blocksRaycasts);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region Helper Classes & Mock Services

        [Test]
        public void RootLifeTimeScope_DefaultWebSocketTransport_ReturnsExpectedBaseUrl() {
            var go = new GameObject("RootScopeTest");
            var rootScope = go.AddComponent<RootLifeTimeScope>();

            // When useWebSocketTransport is true, GetBaseUrl returns websocketBaseUrl
            rootScope.useWebSocketTransport = true;
            Assert.AreEqual(rootScope.websocketBaseUrl, rootScope.GetBaseUrl());

            // When useWebSocketTransport is false, GetBaseUrl returns apiBaseUrl (localhost)
            rootScope.useWebSocketTransport = false;
            Assert.AreEqual(rootScope.apiBaseUrl, rootScope.GetBaseUrl());

            Object.DestroyImmediate(go);
        }

        [Test]
        public async System.Threading.Tasks.Task MockLobbyRestService_SimulatesRegistrationAndHeartbeat() {
            var mockService = new MockLobbyRestService();

            bool reg = await mockService.RegisterSessionAsync("RoomA", 5, 1);
            Assert.IsTrue(reg);
            Assert.AreEqual(1, mockService.RegisteredCount);

            bool hb = await mockService.SendHeartbeatAsync("RoomA", 2);
            Assert.IsTrue(hb);
            Assert.AreEqual(1, mockService.HeartbeatCount);

            bool unreg = await mockService.UnregisterSessionAsync("RoomA");
            Assert.IsTrue(unreg);
            Assert.AreEqual(1, mockService.UnregisterCount);

            var qj = await mockService.QuickJoinAsync(5);
            Assert.IsTrue(qj.found);
            Assert.AreEqual("ROOM123", qj.sessionName);
        }

        private class MockLobbyRestService : ILobbyRestService {
            public int RegisteredCount { get; private set; }
            public int HeartbeatCount { get; private set; }
            public int UnregisterCount { get; private set; }
            public int GetSessionsCallCount { get; private set; }

            public List<LobbySessionDto> SessionsToReturn { get; set; } = new List<LobbySessionDto>();

            public UniTask<ChannelQueryResponseDto> GetChannelsAsync( int betStake ) {
                return UniTask.FromResult(new ChannelQueryResponseDto {
                    betStake = betStake,
                    channels = new List<int> { 1, 2 },
                    recommendedChannel = 2
                });
            }

            public UniTask<List<LobbySessionDto>> GetSessionsAsync( int betStake, int channel ) {
                GetSessionsCallCount++;
                return UniTask.FromResult(SessionsToReturn);
            }

            public UniTask<bool> RegisterSessionAsync( string sessionName, int betStake, int channel, int playerCount = 1, int maxPlayers = 4 ) {
                RegisteredCount++;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> SendHeartbeatAsync( string sessionName, int playerCount ) {
                HeartbeatCount++;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> UnregisterSessionAsync( string sessionName ) {
                UnregisterCount++;
                return UniTask.FromResult(true);
            }

            public UniTask<QuickJoinResponseDto> QuickJoinAsync( int betStake ) {
                return UniTask.FromResult(new QuickJoinResponseDto {
                    found = true,
                    action = "join",
                    sessionName = "ROOM123",
                    channel = 1,
                    betStake = betStake
                });
            }
        }

        private class MockLobbySessionBrowserView : ILobbySessionBrowserView {
            public int LoadingCallCount { get; private set; }
            public bool LastLoadingState { get; private set; }
            public int SelectedStakeIndex { get; private set; } = -1;
            public int LastChannelCount { get; private set; }
            public int LastSelectedChannel { get; private set; }
            public int RenderSessionsCallCount { get; private set; }
            public List<LobbySessionDto> LastRenderedSessions { get; private set; }

            public void SetLoading( bool isLoading ) {
                LoadingCallCount++;
                LastLoadingState = isLoading;
            }

            public void SetSelectedStakeButton( int betIndex ) {
                SelectedStakeIndex = betIndex;
            }

            public void UpdateChannelDropdown( int channelCount, int selectedChannel ) {
                LastChannelCount = channelCount;
                LastSelectedChannel = selectedChannel;
            }

            public void RenderSessions( List<LobbySessionDto> sessions ) {
                RenderSessionsCallCount++;
                LastRenderedSessions = sessions;
            }

            public void ShowError( string message ) { }
        }

        #endregion
    }
}
