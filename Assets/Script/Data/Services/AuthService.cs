using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Script.Data.Services {
    public class AuthService : IAuthService {
        private readonly IPlayerRepository playerRepository;
        private readonly ITokenStorage tokenStorage;
        private readonly SemaphoreSlim refreshLock = new(1, 1);

        public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && CurrentUser != null;
        public string AuthToken { get; private set; }
        public PlayerProfileData CurrentUser { get; private set; }

        public event Action<PlayerProfileData> OnUserAuthenticated;
        public event Action OnUserLoggedOut;

        public AuthService( IPlayerRepository playerRepository, ITokenStorage tokenStorage ) {
            this.playerRepository = playerRepository;
            this.tokenStorage = tokenStorage;
        }

        public async UniTask<AuthResponse> LoginAsync( string email, string password ) {
            var response = await playerRepository.LoginAsync(new LoginRequest {
                email = email,
                password = password
            });

            if ( response != null && response.isSuccess ) {
                if ( !string.IsNullOrEmpty(response.refreshToken) ) {
                    await tokenStorage.SaveRefreshTokenAsync(response.refreshToken);
                }
                SetAuthenticatedUser(response.accessToken, response.profile);
            }

            return response;
        }

        public async UniTask<AuthResponse> RegisterAsync( string email, string password ) {
            var response = await playerRepository.RegisterAsync(new RegisterRequest {
                email = email,
                password = password
            });

            if ( response != null && response.isSuccess ) {
                if ( !string.IsNullOrEmpty(response.refreshToken) ) {
                    await tokenStorage.SaveRefreshTokenAsync(response.refreshToken);
                }
                SetAuthenticatedUser(response.accessToken, response.profile);
            }

            return response;
        }

        public async UniTask Logout() {
            string savedRefreshToken = await tokenStorage.LoadRefreshTokenAsync();
            if ( !string.IsNullOrEmpty(savedRefreshToken) ) {
                try {
                    await playerRepository.LogoutAsync(new LogoutRequest { refreshToken = savedRefreshToken });
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[AuthService] Error notifying backend of logout: {ex.Message}");
                }
            }

            AuthToken = string.Empty;
            CurrentUser = null;
            await tokenStorage.ClearAsync();
            OnUserLoggedOut?.Invoke();
        }

        public void SetAuthenticatedUser( string token, PlayerProfileData profile ) {
            AuthToken = token;
            CurrentUser = profile;
            OnUserAuthenticated?.Invoke(profile);
        }

        public async UniTask<bool> RestoreSessionAsync() {
            return await RefreshSessionAsync();
        }

        public async UniTask<bool> RefreshSessionAsync() {
            await refreshLock.WaitAsync();
            try {
                string savedRefreshToken = await tokenStorage.LoadRefreshTokenAsync();
                if ( string.IsNullOrEmpty(savedRefreshToken) ) {
                    return false;
                }

                var response = await playerRepository.RefreshTokenAsync(new RefreshTokenRequest {
                    refreshToken = savedRefreshToken
                });

                if ( response != null && response.isSuccess && !string.IsNullOrEmpty(response.accessToken) ) {
                    string newAccessToken = response.accessToken;

                    // Save rotated refresh token if provided
                    if ( !string.IsNullOrEmpty(response.refreshToken) ) {
                        await tokenStorage.SaveRefreshTokenAsync(response.refreshToken);
                    }

                    AuthToken = newAccessToken;

                    // Load user profile using the fresh access token
                    var profile = await playerRepository.GetProfileAsync(newAccessToken);
                    if ( profile != null ) {
                        CurrentUser = profile;
                        OnUserAuthenticated?.Invoke(profile);
                        return true;
                    }
                }

                // If refresh was rejected or user profile could not be loaded, clear stale session
                await Logout();
                return false;
            }
            catch ( Exception ex ) {
                Debug.LogError($"[AuthService] Error during RefreshSessionAsync: {ex.Message}");
                return false;
            }
            finally {
                refreshLock.Release();
            }
        }
    }
}
