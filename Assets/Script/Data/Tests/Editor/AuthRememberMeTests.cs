using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Script.Data.Tests {
    [TestFixture]
    public class AuthRememberMeTests {
        private MockPlayerRepository mockRepository;
        private InMemoryTokenStorage mockTokenStorage;
        private AuthService authService;

        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(AuthService.RememberMePrefKey);
            PlayerPrefs.Save();

            mockRepository = new MockPlayerRepository();
            mockTokenStorage = new InMemoryTokenStorage();
            authService = new AuthService(mockRepository, mockTokenStorage);
        }

        [TearDown]
        public void TearDown() {
            PlayerPrefs.DeleteKey(AuthService.RememberMePrefKey);
            PlayerPrefs.Save();
        }

        [Test]
        public async Task LoginWithRememberMe_SavesTokenAndSetsPreference() {
            // Register user first
            var reg = await authService.RegisterAsync("player1@test.com", "Password123");
            Assert.IsTrue(reg.isSuccess);

            // Re-login with rememberMe = true
            var login = await authService.LoginAsync("player1@test.com", "Password123", rememberMe: true);

            Assert.IsTrue(login.isSuccess);
            Assert.IsTrue(authService.IsAuthenticated);
            Assert.IsTrue(authService.IsRememberMeEnabled, "Remember Me preference flag should be set to true.");

            string savedToken = await mockTokenStorage.LoadRefreshTokenAsync();
            Assert.IsFalse(string.IsNullOrEmpty(savedToken), "Refresh token should be saved when rememberMe is true.");
        }

        [Test]
        public async Task LoginWithoutRememberMe_ClearsTokenAndPreference() {
            // Pre-seed storage and pref
            await mockTokenStorage.SaveRefreshTokenAsync("stale_token");
            PlayerPrefs.SetInt(AuthService.RememberMePrefKey, 1);
            PlayerPrefs.Save();

            // Register and login without rememberMe
            await authService.RegisterAsync("player2@test.com", "Password123");
            var login = await authService.LoginAsync("player2@test.com", "Password123", rememberMe: false);

            Assert.IsTrue(login.isSuccess);
            Assert.IsFalse(authService.IsRememberMeEnabled, "Remember Me preference flag should be false.");

            string savedToken = await mockTokenStorage.LoadRefreshTokenAsync();
            Assert.IsTrue(string.IsNullOrEmpty(savedToken), "Saved refresh token should be cleared when rememberMe is false.");
        }

        [Test]
        public async Task RestoreSessionAsync_WithSavedToken_RestoresUserWithoutCredentials() {
            // Login with the seeded test user and rememberMe = true
            var login = await authService.LoginAsync("player@test.com", "password123", rememberMe: true);
            Assert.IsTrue(login.isSuccess);
            string savedToken = await mockTokenStorage.LoadRefreshTokenAsync();
            Assert.IsFalse(string.IsNullOrEmpty(savedToken));

            // Simulate app reboot with a fresh AuthService instance using the same persistent storage
            var rebootedAuthService = new AuthService(mockRepository, mockTokenStorage);
            Assert.IsFalse(rebootedAuthService.IsAuthenticated, "New AuthService instance should not be authenticated before restore.");

            // Restore session
            bool restored = await rebootedAuthService.RestoreSessionAsync();

            Assert.IsTrue(restored, "RestoreSessionAsync should succeed when valid refresh token exists.");
            Assert.IsTrue(rebootedAuthService.IsAuthenticated, "User should be authenticated after session restore.");
            Assert.IsNotNull(rebootedAuthService.CurrentUser);
            Assert.AreEqual("player@test.com", rebootedAuthService.CurrentUser.email);
            Assert.AreEqual("AcePlayer", rebootedAuthService.CurrentUser.displayName);
        }

        [Test]
        public async Task Logout_ClearsStoredToken_PreventsSubsequentSessionRestore() {
            // Register & login with rememberMe
            await authService.RegisterAsync("player4@test.com", "Password123");
            await authService.LoginAsync("player4@test.com", "Password123", rememberMe: true);
            Assert.IsTrue(authService.IsAuthenticated);

            // User logs out
            await authService.Logout();

            Assert.IsFalse(authService.IsAuthenticated, "AuthService should no longer be authenticated after logout.");
            string savedToken = await mockTokenStorage.LoadRefreshTokenAsync();
            Assert.IsTrue(string.IsNullOrEmpty(savedToken), "Stored refresh token must be wiped on logout.");

            // Subsequent restore attempt must fail
            bool restored = await authService.RestoreSessionAsync();
            Assert.IsFalse(restored, "RestoreSessionAsync must fail after logout.");
        }
    }
}
