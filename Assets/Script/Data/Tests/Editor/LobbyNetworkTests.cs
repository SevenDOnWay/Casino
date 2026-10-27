using Assets.Script;
using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.UI.Components;
using Assets.Script.UI.MainMenu;
using Assets.Script.UI.MainMenu.Panel;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class LobbyNetworkTests {
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
        public void BrowserPanel_SetSelectedStake_UpdatesProperty() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();

            panel.SetSelectedStake(50);
            Assert.AreEqual(50, panel.SelectedStake);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_FindTable_UpdatesStakeFromDictionary() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();

            // Index 0 -> 5, Index 1 -> 10, Index 2 -> 20, Index 3 -> 50, Index 4 -> 100
            panel.FindTable(3);
            Assert.AreEqual(50, panel.SelectedStake);

            panel.FindTable(1);
            Assert.AreEqual(10, panel.SelectedStake);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_ChannelNavigation_ClampsWithinBounds() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();

            panel.SetChannel(999);
            Assert.AreEqual(panel.MaxChannels, panel.CurrentChannel);

            panel.SetChannel(-5);
            Assert.AreEqual(1, panel.CurrentChannel);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_NextPrevChannel_StepsCorrectly() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();

            Assert.AreEqual(1, panel.CurrentChannel);

            panel.NextChannel();
            Assert.AreEqual(2, panel.CurrentChannel);

            panel.NextChannel();
            Assert.AreEqual(3, panel.CurrentChannel);

            panel.PrevChannel();
            Assert.AreEqual(2, panel.CurrentChannel);

            panel.PrevChannel();
            Assert.AreEqual(1, panel.CurrentChannel);

            // Cannot go below 1
            panel.PrevChannel();
            Assert.AreEqual(1, panel.CurrentChannel);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_AutoShards_WhenChannelReachesCapacity() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();

            // Initially, stake 5 has 1 channel and optimized channel is 1
            Assert.AreEqual(1, panel.GetChannelCountForStake(5));
            Assert.AreEqual(1, panel.GetOptimizedChannel(5));

            // Simulate Channel 1 is full (30 sessions)
            string ch1Lobby = TienLenNetworkController.GetLobbyId(5, 1);
            panel.SetChannelSessionCountForTest(ch1Lobby, 30);

            // Optimized channel should auto-shard to Channel 2
            int optChannel = panel.GetOptimizedChannel(5);
            Assert.AreEqual(2, optChannel);
            Assert.AreEqual(2, panel.GetChannelCountForStake(5));

            // Options should now include Channel 1 and Channel 2
            var options = panel.GetChannelOptions(0); // Bet index 0 is Stake 5
            Assert.AreEqual(2, options.Count);
            Assert.AreEqual("Channel 1", options[0]);
            Assert.AreEqual("Channel 2", options[1]);

            Object.DestroyImmediate(go);
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

        [Test]
        public void BrowserPanel_WithMockLobbyService_CanAssign() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<LobbySessionBrowserPanelView>();
            var mockService = new MockLobbyRestService();

            panel.SetLobbyRestServiceForTest(mockService);
            Assert.IsNotNull(panel);

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
        public void BrowserPanel_UseWebSocketApi_RespectsNetworkConfig() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            bool useWs = true;
            var config = new NetworkConfig(() => useWs, () => "http://localhost:5000/api", () => "https://casino-2odk.onrender.com/api");
            panel.SetNetworkConfigForTest(config);

            Assert.IsTrue(panel.UseWebSocketApi);

            useWs = false;
            Assert.IsFalse(panel.UseWebSocketApi);

            // Override for testing
            panel.SetUseWebSocketApiForTest(true);
            Assert.IsTrue(panel.UseWebSocketApi);

            panel.SetUseWebSocketApiForTest(false);
            Assert.IsFalse(panel.UseWebSocketApi);

            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public async System.Threading.Tasks.Task BrowserPanel_SwitchLobby_WhenWebSocketApiActive_FetchesSessionsFromApi() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();
            var mockService = new MockLobbyRestService();

            mockService.SessionsToReturn.Add(new LobbySessionDto {
                sessionName = "WebRoom1",
                betStake = 5,
                channel = 1,
                playerCount = 2,
                maxPlayers = 4
            });

            panel.SetLobbyRestServiceForTest(mockService);
            panel.SetUseWebSocketApiForTest(true);

            await panel.SwitchLobbyByStake(5, 1);

            Assert.AreEqual(1, mockService.GetSessionsCallCount);

            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BetButton_InitAndStake_ParsesCorrectly() {
            var go = new GameObject("BetButtonTest");
            var betBtn = go.AddComponent<BetButtonScript>();
            var textGo = new GameObject("TextTMP");
            textGo.transform.SetParent(go.transform);
            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "20";

            var panelGo = new GameObject("PanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            betBtn.Init(2, "20", panel);
            Assert.AreEqual(2, betBtn.Index);

            Object.DestroyImmediate(panelGo);
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
            circle.Stop();
            Assert.IsFalse(circle.IsPlaying);
            Assert.IsFalse(cg.blocksRaycasts);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_SetLoading_DrivesDynamicLoadingCircle() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            var circleGo = new GameObject("DynamicCircle");
            circleGo.transform.SetParent(panelGo.transform);
            var circle = circleGo.AddComponent<DynamicLoadingCircle>();
            panel.SetDynamicLoadingCircleForTest(circle);

            // Test SetLoading(true) and ShowLoading(true)
            panel.SetLoading(true);
            Assert.IsTrue(circle.IsPlaying);

            panel.SetLoading(false);
            Assert.IsFalse(circle.IsPlaying);

            panel.ShowLoading(true);
            Assert.IsTrue(circle.IsPlaying);

            panel.ShowLoading(false);
            Assert.IsFalse(circle.IsPlaying);

            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BrowserPanel_SetLoading_NullSafe_WhenNoUIConfigured() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            Assert.DoesNotThrow(() => {
                panel.SetLoading(true);
                panel.SetLoading(false);
                panel.ShowLoading(true);
                panel.ShowLoading(false);
            });

            Object.DestroyImmediate(panelGo);
        }

        [Test]
        public void BrowserPanel_SetLoading_FallsBackToLoadingIndicatorGameObject() {
            var panelGo = new GameObject("BrowserPanelTest");
            var panel = panelGo.AddComponent<LobbySessionBrowserPanelView>();

            var indicatorGo = new GameObject("IndicatorLegacy");
            indicatorGo.SetActive(false);
            panel.SetLoadingIndicatorForTest(indicatorGo);

            panel.SetLoading(true);
            Assert.IsTrue(indicatorGo.activeSelf);

            panel.SetLoading(false);
            Assert.IsFalse(indicatorGo.activeSelf);

            Object.DestroyImmediate(indicatorGo);
            Object.DestroyImmediate(panelGo);
        }
    }
}
