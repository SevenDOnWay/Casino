using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace Assets.Script.Data.Services {
    public class AuthService : IAuthService {
        private const string TokenPrefKey = "TIENLEN_AUTH_TOKEN";
        private readonly IPlayerRepository playerRepository;

        public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && CurrentUser != null;
        public string AuthToken { get; private set; }
        public PlayerProfileData CurrentUser { get; private set; }

        public event Action<PlayerProfileData> OnUserAuthenticated;
        public event Action OnUserLoggedOut;

        public AuthService( IPlayerRepository playerRepository ) {
            this.playerRepository = playerRepository;
            AuthToken = PlayerPrefs.GetString(TokenPrefKey, string.Empty);
        }

        public async UniTask<AuthResponse> LoginAsync( string email, string password ) {
            var response = await playerRepository.LoginAsync(new LoginRequest {
                email = email,
                password = password
            });

            if ( response != null && response.isSuccess ) {
                SetAuthenticatedUser(response.token, response.profile);
            }

            return response;
        }

        public async UniTask<AuthResponse> RegisterAsync( string email, string password ) {
            var response = await playerRepository.RegisterAsync(new RegisterRequest {
                email = email,
                password = password
            });

            if ( response != null && response.isSuccess ) {
                SetAuthenticatedUser(response.token, response.profile);
            }

            return response;
        }

        public void Logout() {
            AuthToken = string.Empty;
            CurrentUser = null;
            PlayerPrefs.DeleteKey(TokenPrefKey);
            PlayerPrefs.Save();
            OnUserLoggedOut?.Invoke();
        }

        public void SetAuthenticatedUser( string token, PlayerProfileData profile ) {
            AuthToken = token;
            CurrentUser = profile;
            PlayerPrefs.SetString(TokenPrefKey, token);
            PlayerPrefs.Save();
            OnUserAuthenticated?.Invoke(profile);
        }
    }
}
