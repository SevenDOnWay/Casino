using Assets.Script.Data.SO;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class PlayerSeatIntegrationTests {
        private GameObject seatObject;
        private PlayerSeat seat;
        private PlayerInfoView infoView;
        private GameObject timerObject;
        private ClockWipe timer;
        private TextMeshPro moneyText;
        private TextMeshPro nameText;
        private TextMeshPro levelText;
        private SpriteRenderer avatarRenderer;

        [SetUp]
        public void SetUp() {
            seatObject = new GameObject("PlayerSeat");

            var container = new GameObject("PlayerDisplayContainer");
            container.transform.SetParent(seatObject.transform);

            var avatarObj = new GameObject("Image");
            avatarObj.transform.SetParent(container.transform);
            avatarRenderer = avatarObj.AddComponent<SpriteRenderer>();

            timerObject = new GameObject("TimerClock");
            timerObject.transform.SetParent(avatarObj.transform);
            timer = timerObject.AddComponent<ClockWipe>();

            var moneyObj = new GameObject("MoneyText");
            moneyObj.transform.SetParent(container.transform);
            moneyText = moneyObj.AddComponent<TextMeshPro>();

            var nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(container.transform);
            nameText = nameObj.AddComponent<TextMeshPro>();

            var levelObj = new GameObject("LevelText");
            levelObj.transform.SetParent(container.transform);
            levelText = levelObj.AddComponent<TextMeshPro>();

            infoView = seatObject.AddComponent<PlayerInfoView>();
            infoView.Initialize(
                avatar: avatarRenderer,
                nameText: nameText,
                money: moneyText,
                levelText: levelText,
                turnTimer: timer,
                infoRoot: container
            );

            seat = seatObject.AddComponent<PlayerSeat>();
            seat.SetInfoView(infoView);
        }

        [TearDown]
        public void TearDown() {
            if ( seatObject != null ) {
                Object.DestroyImmediate(seatObject);
            }
        }

        [Test]
        public void PlayerSeat_BindPlayer_UpdatesInfoViewAndModel() {
            var player = new TienLenPlayer(1, "Alice", true, 0, 5, 25000);

            seat.BindPlayer(player, 2);

            Assert.IsTrue(seat.IsOccupied);
            Assert.AreEqual(2, seat.BoundSeatIndex);
            Assert.AreEqual(player, seat.tienLenPlayer);
            Assert.AreEqual("Alice", nameText.text);
            Assert.AreEqual("$25K", moneyText.text);
            Assert.AreEqual("Lv.5", levelText.text);
        }

        [Test]
        public void PlayerSeat_UpdateMoney_UpdatesModelAndInfoView() {
            var player = new TienLenPlayer(1, "Bob", true, 0, 1, 1000);
            seat.BindPlayer(player, 0);

            seat.UpdateMoney(1500000);

            Assert.AreEqual(1500000, seat.tienLenPlayer.Money);
            Assert.AreEqual("$1.5M", moneyText.text);
        }

        [Test]
        public void PlayerSeat_ClearSeat_ResetsModelAndInfoView() {
            var player = new TienLenPlayer(1, "Charlie", true, 0, 2, 5000);
            seat.BindPlayer(player, 1);

            seat.ClearSeat();

            Assert.IsFalse(seat.IsOccupied);
            Assert.AreEqual(-1, seat.BoundSeatIndex);
            Assert.IsNull(seat.tienLenPlayer);
            Assert.AreEqual(string.Empty, nameText.text);
            Assert.AreEqual(string.Empty, moneyText.text);
            Assert.AreEqual(string.Empty, levelText.text);
        }

        [Test]
        public void PlayerSeat_TurnTimer_ControlsClockWipe() {
            var player = new TienLenPlayer(1, "Dave", true, 0, 1, 1000);
            seat.BindPlayer(player, 0);

            seat.StartTurnTimer(0.8f);
            Assert.IsTrue(timerObject.activeSelf);
            Assert.AreEqual(0.8f, timer.FillAmount, 0.001f);

            seat.UpdateTurnTimer(0.4f);
            Assert.AreEqual(0.4f, timer.FillAmount, 0.001f);

            seat.StopTurnTimer();
            Assert.IsFalse(timerObject.activeSelf);
        }

        [Test]
        public void FormatMoney_FormatsValuesCorrectly() {
            Assert.AreEqual("$500", PlayerInfoView.FormatMoney(500));
            Assert.AreEqual("$10K", PlayerInfoView.FormatMoney(10000));
            Assert.AreEqual("$25.5K", PlayerInfoView.FormatMoney(25500));
            Assert.AreEqual("$1M", PlayerInfoView.FormatMoney(1000000));
            Assert.AreEqual("$2.5M", PlayerInfoView.FormatMoney(2500000));
        }
    }
}
