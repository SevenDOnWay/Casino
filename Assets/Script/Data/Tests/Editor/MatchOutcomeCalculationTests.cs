using Assets.Script.Data.Models;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
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

        [Test]
        public void MatchOutcomeReporter_Standalone_BuildsCorrectReport() {
            var mockSeat = new MockSeatQueryService();
            mockSeat.Seated[0] = new TienLenPlayer(1, "Player1", true, 0, 1, 5000, "user_1");
            mockSeat.Seated[1] = new TienLenPlayer(2, "Player2", true, 1, 1, 5000, "user_2");
            mockSeat.Seated[2] = new TienLenPlayer(3, "Player3", true, 2, 1, 5000, "user_3");

            var reporter = new TienLenMatchOutcomeReporter(mockSeat);
            var report = reporter.BuildMatchReport(winnerSeat: 0, currentBet: 20);

            Assert.IsNotNull(report);
            Assert.AreEqual(3, report.results.Count);

            var winner = report.results.First(r => r.userId == "user_1");
            Assert.AreEqual(1, winner.rank);
            Assert.AreEqual(40, winner.moneyEarned); // 2 losers * 20

            var loser = report.results.First(r => r.userId == "user_2");
            Assert.AreEqual(2, loser.rank);
            Assert.AreEqual(-20, loser.moneyEarned);
        }

        [Test]
        public void TurnHandler_FindStartingSeatOrderIndex_FindsThreeOfSpades() {
            var p1 = new TienLenPlayer(1, "Alice", true);
            p1.Hand.AddCard(new Card(CardRank.Three, CardSuit.Hearts));
            p1.Hand.AddCard(new Card(CardRank.Ace, CardSuit.Diamonds));

            var p2 = new TienLenPlayer(2, "Bob", true);
            p2.Hand.AddCard(new Card(CardRank.Three, CardSuit.Spades)); // Holds 3 of Spades
            p2.Hand.AddCard(new Card(CardRank.King, CardSuit.Clubs));

            var ordered = new List<TienLenPlayer> { p1, p2 };
            var turnHandler = new TienLenTurnHandler(null, null);

            int startingIndex = turnHandler.FindStartingSeatOrderIndex(ordered);
            Assert.AreEqual(1, startingIndex, "Bob at index 1 holds 3 of Spades");
        }

        [Test]
        public void TurnHandler_DecideAutoAction_WhenCombinationExists_ReturnsPass() {
            var validator = new TienLenRuleValidator();
            var leadCombo = new CardCombination(
                CardCombinationType.Single,
                new List<Card> { new Card(CardRank.Five, CardSuit.Hearts) },
                new CardComparer()
            );
            validator.SetCurrentCombination(leadCombo);

            var turnHandler = new TienLenTurnHandler(null, validator);
            var player = new TienLenPlayer(1, "Alice", true);
            player.Hand.AddCard(new Card(CardRank.Seven, CardSuit.Diamonds));

            var decision = turnHandler.DecideAutoAction(player, isFirstPlayOfGame: false);
            Assert.AreEqual(AutoActionResultType.Pass, decision.ActionType);
        }

        [Test]
        public void TurnManager_PlayCard_DoesNotResetValidatorPrematurely() {
            var validator = new TienLenRuleValidator();
            var leadCombo = new CardCombination(
                CardCombinationType.Single,
                new List<Card> { new Card(CardRank.Ace, CardSuit.Spades) },
                new CardComparer()
            );
            validator.SetCurrentCombination(leadCombo);

            var p1 = new TienLenPlayer(1, "Player 1", true);
            var p2 = new TienLenPlayer(2, "Player 2", true);
            p1.Hand.AddCard(new Card(CardRank.Three, CardSuit.Spades));
            p2.Hand.AddCard(new Card(CardRank.Four, CardSuit.Hearts));
            var turnManager = new TurnManager(validator);
            turnManager.Initialize(new List<TienLenPlayer> { p1, p2 });
            turnManager.SetLastPlayPlayer(p1);

            // When turn advances to next player, validator must NOT be reset
            turnManager.AdvanceTurn();

            Assert.AreEqual(p2, turnManager.CurrentPlayer, "Turn should advance to Player 2");
            Assert.IsNotNull(validator.CurrentCombination, "Validator combination must persist for the next player");
            Assert.IsTrue(turnManager.CanPass(p2), "Player 2 should be allowed to pass against active combination");
        }

        [Test]
        public void TurnManager_PassTurn_WhenAllOtherPlayersPass_ResetsRoundToLeader() {
            var validator = new TienLenRuleValidator();
            var leadCombo = new CardCombination(
                CardCombinationType.Single,
                new List<Card> { new Card(CardRank.King, CardSuit.Hearts) },
                new CardComparer()
            );
            validator.SetCurrentCombination(leadCombo);

            var p1 = new TienLenPlayer(1, "Player 1", true);
            var p2 = new TienLenPlayer(2, "Player 2", true);
            p1.Hand.AddCard(new Card(CardRank.Three, CardSuit.Spades));
            p2.Hand.AddCard(new Card(CardRank.Four, CardSuit.Hearts));
            var turnManager = new TurnManager(validator);
            turnManager.Initialize(new List<TienLenPlayer> { p1, p2 });
            turnManager.SetLastPlayPlayer(p1);
            turnManager.AdvanceTurn(); // Now Player 2's turn

            Assert.AreEqual(p2, turnManager.CurrentPlayer);

            // Player 2 passes
            turnManager.AddPassPlayer(p2);
            Assert.IsTrue(turnManager.CheckRoundFinished(), "Round should be finished because Player 2 passed");

            turnManager.ResetRoundToLeader();

            Assert.AreEqual(p1, turnManager.CurrentPlayer, "Turn should reset back to Player 1 (trick leader)");
            Assert.IsNull(validator.CurrentCombination, "Validator combination should be reset for new round");
            Assert.IsFalse(turnManager.CanPass(p1), "Player 1 cannot pass when leading a new round");
        }

        [Test]
        public void TurnManager_ThreePlayers_PassedPlayerSkippedUntilNewRound() {
            var validator = new TienLenRuleValidator();
            var leadCombo = new CardCombination(
                CardCombinationType.Single,
                new List<Card> { new Card(CardRank.Five, CardSuit.Clubs) },
                new CardComparer()
            );
            validator.SetCurrentCombination(leadCombo);

            var p1 = new TienLenPlayer(1, "P1", true);
            var p2 = new TienLenPlayer(2, "P2", true);
            var p3 = new TienLenPlayer(3, "P3", true);
            p1.Hand.AddCard(new Card(CardRank.Three, CardSuit.Spades));
            p2.Hand.AddCard(new Card(CardRank.Four, CardSuit.Hearts));
            p3.Hand.AddCard(new Card(CardRank.Five, CardSuit.Diamonds));
            var turnManager = new TurnManager(validator);
            turnManager.Initialize(new List<TienLenPlayer> { p1, p2, p3 });
            turnManager.SetLastPlayPlayer(p1);
            turnManager.AdvanceTurn(); // Now P2's turn

            Assert.AreEqual(p2, turnManager.CurrentPlayer);

            // P2 passes
            turnManager.AddPassPlayer(p2);
            Assert.IsFalse(turnManager.CheckRoundFinished(), "Round is not finished yet, P1 and P3 remain");

            turnManager.AdvanceTurn(); // Advance from P2 to P3
            Assert.AreEqual(p3, turnManager.CurrentPlayer, "Turn should advance to P3");

            // P3 passes
            turnManager.AddPassPlayer(p3);
            Assert.IsTrue(turnManager.CheckRoundFinished(), "Round finished because only P1 remains");

            turnManager.ResetRoundToLeader();
            Assert.AreEqual(p1, turnManager.CurrentPlayer, "P1 leads new round");
        }

        [Test]
        public void TurnManager_HandleTurnTimeout_MarksCurrentPlayerInactive() {
            var validator = new TienLenRuleValidator();
            var p1 = new TienLenPlayer(1, "Player 1", true);
            var p2 = new TienLenPlayer(2, "Player 2", true);
            p1.Hand.AddCard(new Card(CardRank.Three, CardSuit.Spades));
            p2.Hand.AddCard(new Card(CardRank.Four, CardSuit.Hearts));

            var turnManager = new TurnManager(validator);
            turnManager.Initialize(new List<TienLenPlayer> { p1, p2 });

            Assert.IsFalse(p1.IsInactive, "Player 1 should start active");

            turnManager.HandleTurnTimeout();

            Assert.IsTrue(p1.IsInactive, "Player 1 should be marked inactive upon turn timeout");
        }

        [Test]
        public void TurnManager_TurnDurationProperty_CanBeConfigured() {
            var turnManager = new TurnManager();
            turnManager.TurnDuration = 15f;
            Assert.AreEqual(15f, turnManager.TurnDuration);
        }

        [Test]
        public void ActionHandler_WhenWinnerSeatAlreadySet_RejectsPlayAndPass() {
            var go = new GameObject("TestGameController");
            var gc = go.AddComponent<TienLenGameController>();
            gc.WinnerSeat = 1;
            gc.IsGameStarted = true;

            var p1 = new TienLenPlayer(1, "Alice", true);
            var actionHandler = new TienLenActionHandler(
                cardCombinationEvaluator: new CardCombinationEvaluator(new CardComparer()),
                turnManager: null,
                validator: new TienLenRuleValidator(),
                seatQueryService: null,
                gameController: gc
            );

            bool playOk = actionHandler.TryValidateAndExecutePlay(p1, new Assets.Script.TienLen.NetworkCard[1], out _, out string playFail);
            Assert.IsFalse(playOk);
            Assert.That(playFail, Does.Contain("concluded"));

            bool passOk = actionHandler.TryExecutePass(p1, out _, out string passFail);
            Assert.IsFalse(passOk);
            Assert.That(passFail, Does.Contain("concluded"));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MatchOutcomeReporter_WhenUserIdIsNumericFusionId_OmitsNumericId() {
            var mockSeat = new MockSeatQueryService();
            var p1 = new TienLenPlayer(1, "Player1", true, 0, 1, 5000, null);
            mockSeat.Seated[0] = p1;

            var report = TienLenMatchOutcomeReporter.BuildMatchReport(0, 10, mockSeat);
            Assert.IsNotNull(report);
            Assert.AreEqual(1, report.results.Count);
            Assert.IsTrue(string.IsNullOrEmpty(report.results[0].userId), "Numeric ID should not be set as userId");
        }

        [Test]
        public void MigrationHandler_TryResumeState_RestoresFirstPlayState() {
            var validator = new TienLenRuleValidator();
            var p1 = new TienLenPlayer(1, "P1", true);
            var p2 = new TienLenPlayer(2, "P2", true);
            var mockSeat = new MockSeatQueryService();
            mockSeat.Seated[0] = p1;
            mockSeat.Seated[1] = p2;

            var goTurn = new GameObject("TestTurnManager");
            var turnManager = goTurn.AddComponent<TurnManager>();
            turnManager.Construct(validator);

            var actionHandler = new TienLenActionHandler(
                cardCombinationEvaluator: new CardCombinationEvaluator(new CardComparer()),
                turnManager: turnManager,
                validator: validator,
                seatQueryService: mockSeat
            );
            actionHandler.IsFirstPlayOfGame = true;

            var migrationHandler = new TienLenMigrationHandler(
                mockSeat, turnManager, new CardCombinationEvaluator(new CardComparer()), validator
            );

            var tableCards = new List<Card> { new Card(CardRank.Three, CardSuit.Spades) };
            bool resumed = migrationHandler.TryResumeState(
                currentTableCardCount: 1,
                tableCards: tableCards,
                currentTurnSeat: 0,
                singleRemainingWinnerSeat: out _,
                turnAdvanced: out _,
                actionHandler: actionHandler
            );

            Assert.IsTrue(resumed);
            Assert.IsFalse(actionHandler.IsFirstPlayOfGame, "First play should be false when resuming with table cards");

            Object.DestroyImmediate(goTurn);
        }
    }
}
