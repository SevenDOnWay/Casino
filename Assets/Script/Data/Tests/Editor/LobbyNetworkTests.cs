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
    }
}
