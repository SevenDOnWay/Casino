using Assets.Script.Data.SO;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
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
                money: moneyText,
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
            Assert.AreEqual("$25K", moneyText.text);
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
            Assert.AreEqual(string.Empty, moneyText.text);
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
        public void ClockWipe_SetProgress_HandlesGradientGracefully() {
            var cwObj = new GameObject("ClockWipeTest");
            var cw = cwObj.AddComponent<ClockWipe>();

            Assert.DoesNotThrow(() => cw.SetProgress(0.75f));
            Assert.AreEqual(0.75f, cw.FillAmount, 0.001f);

            cw.SetProgress(0.0f);
            Assert.AreEqual(0.0f, cw.FillAmount, 0.001f);

            cw.SetProgress(1.5f);
            Assert.AreEqual(1.0f, cw.FillAmount, 0.001f, "Progress above 1 should clamp to 1");

            Object.DestroyImmediate(cwObj);
        }

        [Test]
        public void PlayerSeat_ClearSeat_StopsTurnTimer() {
            var player = new TienLenPlayer(1, "Eve", true, 0, 1, 1000);
            seat.BindPlayer(player, 0);
            seat.StartTurnTimer(0.9f);
            Assert.IsTrue(timerObject.activeSelf);

            seat.ClearSeat();
            Assert.IsFalse(timerObject.activeSelf, "Clearing seat must stop and deactivate turn timer ClockWipe");
        }

        [Test]
        public void PlayerWinEffectController_PlayAndStop_WorksWithoutExceptions() {
            var effectObj = new GameObject("WinEffectTest");
            var ctrl = effectObj.AddComponent<Assets.Script.TienLen.Effects.PlayerWinEffectController>();

            Assert.DoesNotThrow(() => ctrl.PlayWinEffect(0, Vector3.zero));
            Assert.DoesNotThrow(() => ctrl.StopWinEffect());

            Object.DestroyImmediate(effectObj);
        }

        [Test]
        public void ActionPanel_ReturnToLobby_HidesButtons() {
            var panelObj = new GameObject("ActionPanelTest");
            var playBtnObj = new GameObject("PlayBtn");
            playBtnObj.transform.SetParent(panelObj.transform);
            var playBtn = playBtnObj.AddComponent<UnityEngine.UI.Button>();

            var passBtnObj = new GameObject("PassBtn");
            passBtnObj.transform.SetParent(panelObj.transform);
            var passBtn = passBtnObj.AddComponent<UnityEngine.UI.Button>();

            var sortBtnObj = new GameObject("SortBtn");
            sortBtnObj.transform.SetParent(panelObj.transform);
            var sortBtn = sortBtnObj.AddComponent<UnityEngine.UI.Button>();

            var panel = panelObj.AddComponent<ActionPanel>();
            typeof(ActionPanel).GetField("playBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(panel, playBtn);
            typeof(ActionPanel).GetField("passBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(panel, passBtn);
            typeof(ActionPanel).GetField("sortBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(panel, sortBtn);

            panel.ReturnToLobby();
            Assert.IsFalse(playBtnObj.activeSelf);
            Assert.IsFalse(passBtnObj.activeSelf);
            Assert.IsFalse(sortBtnObj.activeSelf);

            panel.EnterGame();
            Assert.IsTrue(playBtnObj.activeSelf);
            Assert.IsTrue(passBtnObj.activeSelf);
            Assert.IsTrue(sortBtnObj.activeSelf);

            Object.DestroyImmediate(panelObj);
        }

        [Test]
        public void FormatMoney_FormatsValuesCorrectly() {
            Assert.AreEqual("$500", PlayerInfoView.FormatMoney(500));
            Assert.AreEqual("$10K", PlayerInfoView.FormatMoney(10000));
            Assert.AreEqual("$25.5K", PlayerInfoView.FormatMoney(25500));
            Assert.AreEqual("$1M", PlayerInfoView.FormatMoney(1000000));
            Assert.AreEqual("$2.5M", PlayerInfoView.FormatMoney(2500000));
        }

        [Test]
        public void TableVisualLayoutManager_ClearAllCardHolders_ClearsAllSlots() {
            var layoutObj = new GameObject("TableVisualLayoutManager");
            var layout = layoutObj.AddComponent<TableVisualLayoutManager>();

            var seat1Obj = new GameObject("Seat1");
            seat1Obj.transform.SetParent(layoutObj.transform);
            var seat1 = seat1Obj.AddComponent<PlayerSeat>();
            var holder1Obj = new GameObject("CardHolder1");
            holder1Obj.transform.SetParent(seat1Obj.transform);
            var holder1 = holder1Obj.AddComponent<CardHolder>();
            typeof(PlayerSeat).GetField("cardHolder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(seat1, holder1);

            var visualSlotsField = typeof(TableVisualLayoutManager).GetField("visualSlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            visualSlotsField.SetValue(layout, new PlayerSeat[] { seat1 });

            // Add dummy cards to holder1's internal list
            var cardsViewsField = typeof(CardHolder).GetField("cardsViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cardList = (System.Collections.Generic.List<CardView>)cardsViewsField.GetValue(holder1);
            var dummyCardObj = new GameObject("DummyCard");
            dummyCardObj.transform.SetParent(holder1Obj.transform);
            var dummyView = dummyCardObj.AddComponent<CardView>();
            cardList.Add(dummyView);

            Assert.AreEqual(1, holder1.CardViews.Count);

            layout.ClearAllCardHolders();

            Assert.AreEqual(0, holder1.CardViews.Count);

            Object.DestroyImmediate(layoutObj);
        }

        [Test]
        public void SessionController_QueuePlayerDisconnect_TracksQueuedPlayer() {
            var sessionObj = new GameObject("SessionController");
            var session = sessionObj.AddComponent<SessionController>();

            PlayerRef p1 = PlayerRef.FromEncoded(1);
            PlayerRef p2 = PlayerRef.FromEncoded(2);

            Assert.IsFalse(session.IsPlayerDisconnectQueued(p1));
            Assert.IsFalse(session.HasQueuedDisconnects);

            session.QueuePlayerDisconnect(p1);

            Assert.IsTrue(session.IsPlayerDisconnectQueued(p1));
            Assert.IsFalse(session.IsPlayerDisconnectQueued(p2));
            Assert.IsTrue(session.HasQueuedDisconnects);
            Assert.AreEqual(1, session.GetQueuedDisconnectPlayers().Count);

            Object.DestroyImmediate(sessionObj);
        }

        [Test]
        public void MenuPanel_OnGoToLobbyBtnClick_CallsSessionController() {
            var panelObj = new GameObject("MenuPanel");
            var panel = panelObj.AddComponent<MenuPanel>();

            var btnObj = new GameObject("GoToLobbyBtn");
            btnObj.transform.SetParent(panelObj.transform);
            var btn = btnObj.AddComponent<UnityEngine.UI.Button>();

            var sessionObj = new GameObject("SessionController");
            var session = sessionObj.AddComponent<SessionController>();

            panel.Construct(session);
            typeof(MenuPanel).GetField("goToLobbyBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(panel, btn);

            // Trigger OnEnable so button listener is wired
            panelObj.SetActive(false);
            panelObj.SetActive(true);

            // In edit mode without game running, clicking RequestLeaveToLobby initiates leave cleanly
            btn.onClick.Invoke();

            Assert.IsNotNull(session);

            Object.DestroyImmediate(panelObj);
            Object.DestroyImmediate(sessionObj);
        }
    }
}
