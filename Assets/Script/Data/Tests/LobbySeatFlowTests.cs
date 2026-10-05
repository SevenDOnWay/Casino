using Assets.Script.Data.Models;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class LobbySeatFlowTests {
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
        public void LobbySeatFlow_ProfileToSeatBinding_DisplaysCorrectly() {
            // 1. Create a PlayerProfileData with displayName="DragonMaster", avatarId=2, level=5, money=75000.
            var profile = new PlayerProfileData {
                displayName = "DragonMaster",
                avatarId = 2,
                level = 5,
                money = 75000
            };

            // 2. Set profile into LocalPlayerService.
            var localPlayerService = new LocalPlayerService();
            localPlayerService.SetProfile(profile);

            // 3. Verify localPlayerService.GetName() == "DragonMaster", GetAvatarId() == 2, GetLevel() == 5, GetMoney() == 75000.
            Assert.AreEqual("DragonMaster", localPlayerService.GetName());
            Assert.AreEqual(2, localPlayerService.GetAvatarId());
            Assert.AreEqual(5, localPlayerService.GetLevel());
            Assert.AreEqual(75000, localPlayerService.GetMoney());

            // 4. Create a TienLenPlayer with the profile data (or updated via UpdateFromNetwork).
            var player = new TienLenPlayer(
                id: 1,
                playerName: localPlayerService.GetName(),
                isHuman: true,
                avatarId: localPlayerService.GetAvatarId(),
                level: localPlayerService.GetLevel(),
                money: localPlayerService.GetMoney()
            );

            // 5. Bind TienLenPlayer to PlayerSeat.
            seat.BindPlayer(player, 0);

            // 6. Assert that PlayerSeat.tienLenPlayer.PlayerName == "DragonMaster" and money / level are correctly displayed.
            Assert.IsNotNull(seat.tienLenPlayer);
            Assert.AreEqual("DragonMaster", seat.tienLenPlayer.PlayerName);
            Assert.AreEqual(2, seat.tienLenPlayer.AvatarId);
            Assert.AreEqual(5, seat.tienLenPlayer.Level);
            Assert.AreEqual(75000, seat.tienLenPlayer.Money);

            Assert.AreEqual("DragonMaster", nameText.text);
            Assert.AreEqual("$75K", moneyText.text);
            Assert.AreEqual("Lv.5", levelText.text);
        }

        [Test]
        public void LobbySeatFlow_ProfileUpdate_PropagatesToSeatMoney() {
            var profile = new PlayerProfileData {
                displayName = "DragonMaster",
                avatarId = 2,
                level = 5,
                money = 75000
            };

            var localPlayerService = new LocalPlayerService();
            localPlayerService.SetProfile(profile);

            var player = new TienLenPlayer(
                id: 1,
                playerName: localPlayerService.GetName(),
                isHuman: true,
                avatarId: localPlayerService.GetAvatarId(),
                level: localPlayerService.GetLevel(),
                money: localPlayerService.GetMoney()
            );

            seat.BindPlayer(player, 0);

            // Update money on seat (e.g. after a winning hand)
            seat.UpdateMoney(120000);

            Assert.AreEqual(120000, seat.tienLenPlayer.Money);
            Assert.AreEqual("$120K", moneyText.text);
        }
    }
}
