using Assets.Script.Data.Models;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Fusion;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class MatchOutcomeCalculationTests {
        private class MockSeatQueryService : ISeatQueryService {
            public int SeatRevision => 1;
            public Dictionary<int, TienLenPlayer> Seated = new();
            public Dictionary<int, TienLenNetWorkPlayer> NetMap = new();

            public bool TryGetSeat( PlayerRef player, out int seatIndex ) {
                seatIndex = -1;
                return false;
            }

            public IReadOnlyList<TienLenNetWorkPlayer> GetNetworkPlayer() => NetMap.Values.ToList();
            public IReadOnlyDictionary<int, TienLenNetWorkPlayer> GetNetworkPlayerMap() => NetMap;
            public IReadOnlyDictionary<int, TienLenPlayer> GetSeatedPlayers() => Seated;
            public TienLenPlayer GetLogicPlayer( PlayerRef player ) => null;
        }

        [Test]
        public void TienLenPlayer_UserId_PreservedAndUpdated() {
            var player = new TienLenPlayer(1, "Alice", true, 0, 1, 10000, "user_123");
            Assert.AreEqual("user_123", player.UserId);

            player.SetUserId("user_456");
            Assert.AreEqual("user_456", player.UserId);
        }

        [Test]
        public void BuildMatchReport_TwoPlayers_WinnerReceivesBet_LoserPaysBet() {
            var go = new GameObject("TestController");
            var controller = go.AddComponent<TienLenGameController>();

            var mockSeat = new MockSeatQueryService();
            var p1 = new TienLenPlayer(1, "PlayerA", true, 1, 1, 10000, "user_a");
            var p2 = new TienLenPlayer(2, "PlayerB", true, 2, 1, 10000, "user_b");
            mockSeat.Seated[0] = p1;
            mockSeat.Seated[1] = p2;

            controller.SetDependenciesForTest(mockSeat, null, null, stake: 100);

            var report = controller.BuildMatchReport(winnerSeat: 0);

            Assert.IsNotNull(report);
            Assert.AreEqual(2, report.results.Count);

            var winner = report.results.FirstOrDefault(r => r.userId == "user_a");
            var loser = report.results.FirstOrDefault(r => r.userId == "user_b");

            Assert.IsNotNull(winner);
            Assert.IsNotNull(loser);

            Assert.AreEqual(1, winner.rank);
            Assert.AreEqual(100, winner.moneyEarned);
            Assert.AreEqual(50, winner.expEarned);

            Assert.AreEqual(2, loser.rank);
            Assert.AreEqual(-100, loser.moneyEarned);
            Assert.AreEqual(15, loser.expEarned);

            Assert.AreEqual(0, winner.moneyEarned + loser.moneyEarned, "Zero-sum game balance invariant");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BuildMatchReport_FourPlayers_WinnerReceivesAllOpponentBets() {
            var go = new GameObject("TestController");
            var controller = go.AddComponent<TienLenGameController>();

            var mockSeat = new MockSeatQueryService();
            for ( int i = 0; i < 4; i++ ) {
                mockSeat.Seated[i] = new TienLenPlayer(i + 1, $"Player_{i}", true, i, 1, 10000, $"user_{i}");
            }

            controller.SetDependenciesForTest(mockSeat, null, null, stake: 50);

            var report = controller.BuildMatchReport(winnerSeat: 2);

            Assert.IsNotNull(report);
            Assert.AreEqual(4, report.results.Count);

            var winner = report.results.FirstOrDefault(r => r.userId == "user_2");
            Assert.IsNotNull(winner);
            Assert.AreEqual(1, winner.rank);
            Assert.AreEqual(150, winner.moneyEarned, "Winner receives 3 * 50 = 150 from 3 losers");

            var losers = report.results.Where(r => r.userId != "user_2").ToList();
            Assert.AreEqual(3, losers.Count);
            foreach ( var l in losers ) {
                Assert.AreEqual(2, l.rank);
                Assert.AreEqual(-50, l.moneyEarned);
            }

            long totalSum = report.results.Sum(r => r.moneyEarned);
            Assert.AreEqual(0, totalSum, "Zero-sum invariant across 4 players");

            Object.DestroyImmediate(go);
        }
    }
}
