using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class DataLayerUnitTests {
        [Test]
        public async Task MockPlayerRepository_Register_SetsDefaultsCorrectly() {
            var repo = new MockPlayerRepository();
            var authService = new AuthService(repo);

            var res = await authService.RegisterAsync("newplayer@test.com", "Password123");

            Assert.IsTrue(res.isSuccess);
            Assert.IsNotNull(res.profile);
            Assert.AreEqual("newplayer", res.profile.displayName);
            Assert.AreEqual(0, res.profile.avatarId);
            Assert.AreEqual(10000, res.profile.money);
            Assert.AreEqual(1, res.profile.level);
        }

        [Test]
        public async Task MockPlayerRepository_UpdateProfile_CustomizesDisplayNameAndAvatar() {
            var repo = new MockPlayerRepository();
            var authService = new AuthService(repo);
            var profileService = new PlayerProfileService(repo, authService);

            var reg = await authService.RegisterAsync("customizer@test.com", "Password123");
            Assert.IsTrue(reg.isSuccess);

            bool updated = await profileService.UpdateProfileAsync("DragonKing", 3);
            Assert.IsTrue(updated);

            Assert.AreEqual("DragonKing", profileService.CachedProfile.displayName);
            Assert.AreEqual(3, profileService.CachedProfile.avatarId);
        }

        [Test]
        public async Task PlayerProfileService_RecordMatchResult_UpdatesBalanceAndHistory() {
            var repo = new MockPlayerRepository();
            var authService = new AuthService(repo);
            var profileService = new PlayerProfileService(repo, authService);

            var reg = await authService.RegisterAsync("winner@test.com", "Password123");
            Assert.IsTrue(reg.isSuccess);

            var report = new MatchResultReportRequest {
                matchId = "test_match_100",
                results = new List<MatchPlayerSummary> {
                    new MatchPlayerSummary {
                        userId = authService.CurrentUser.id,
                        displayName = "winner",
                        avatarId = 0,
                        rank = 1,
                        moneyEarned = 1500,
                        expEarned = 120
                    }
                }
            };

            bool recorded = await profileService.RecordMatchResultAsync(report);
            Assert.IsTrue(recorded);

            Assert.AreEqual(11500, profileService.CachedProfile.money);
            Assert.AreEqual(120, profileService.CachedProfile.exp);
            Assert.AreEqual(2, profileService.CachedProfile.level);
        }
    }
}
