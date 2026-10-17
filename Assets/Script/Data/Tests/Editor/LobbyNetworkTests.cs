using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using NUnit.Framework;
using UnityEngine;

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
            var panel = go.AddComponent<Assets.Script.UI.MainMenu.Panel.LobbySessionBrowserPanel>();

            panel.SetSelectedStake(50);
            Assert.AreEqual(50, panel.SelectedStake);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_FindTable_UpdatesStakeFromDictionary() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<Assets.Script.UI.MainMenu.Panel.LobbySessionBrowserPanel>();

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
            var panel = go.AddComponent<Assets.Script.UI.MainMenu.Panel.LobbySessionBrowserPanel>();

            panel.SetChannel(999);
            Assert.AreEqual(panel.MaxChannels, panel.CurrentChannel);

            panel.SetChannel(-5);
            Assert.AreEqual(1, panel.CurrentChannel);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BrowserPanel_NextPrevChannel_StepsCorrectly() {
            var go = new GameObject("BrowserPanelTest");
            var panel = go.AddComponent<Assets.Script.UI.MainMenu.Panel.LobbySessionBrowserPanel>();

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
            var panel = go.AddComponent<Assets.Script.UI.MainMenu.Panel.LobbySessionBrowserPanel>();

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

            public Cysharp.Threading.Tasks.UniTask<ChannelQueryResponseDto> GetChannelsAsync( int betStake ) {
                return Cysharp.Threading.Tasks.UniTask.FromResult(new ChannelQueryResponseDto {
                    betStake = betStake,
                    channels = new System.Collections.Generic.List<int> { 1, 2 },
                    recommendedChannel = 2
                });
            }

            public Cysharp.Threading.Tasks.UniTask<System.Collections.Generic.List<LobbySessionDto>> GetSessionsAsync( int betStake, int channel ) {
                return Cysharp.Threading.Tasks.UniTask.FromResult(new System.Collections.Generic.List<LobbySessionDto>());
            }

            public Cysharp.Threading.Tasks.UniTask<bool> RegisterSessionAsync( string sessionName, int betStake, int channel, int playerCount = 1, int maxPlayers = 4 ) {
                RegisteredCount++;
                return Cysharp.Threading.Tasks.UniTask.FromResult(true);
            }

            public Cysharp.Threading.Tasks.UniTask<bool> SendHeartbeatAsync( string sessionName, int playerCount ) {
                HeartbeatCount++;
                return Cysharp.Threading.Tasks.UniTask.FromResult(true);
            }

            public Cysharp.Threading.Tasks.UniTask<bool> UnregisterSessionAsync( string sessionName ) {
                UnregisterCount++;
                return Cysharp.Threading.Tasks.UniTask.FromResult(true);
            }

            public Cysharp.Threading.Tasks.UniTask<QuickJoinResponseDto> QuickJoinAsync( int betStake ) {
                return Cysharp.Threading.Tasks.UniTask.FromResult(new QuickJoinResponseDto {
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
            var panel = go.AddComponent<Assets.Script.UI.MainMenu.Panel.LobbySessionBrowserPanel>();
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
    }
}
