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
    }
}
