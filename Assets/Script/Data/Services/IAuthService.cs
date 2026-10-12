using Assets.Script.Data.Models;
using Cysharp.Threading.Tasks;
using System;

namespace Assets.Script.Data.Services {
    public interface IAuthService {
        bool IsAuthenticated { get; }
        string AuthToken { get; }
        PlayerProfileData CurrentUser { get; }

        event Action<PlayerProfileData> OnUserAuthenticated;
        event Action OnUserLoggedOut;

        UniTask<AuthResponse> LoginAsync( string email, string password );
        UniTask<AuthResponse> RegisterAsync( string email, string password );
        void Logout();
    }
}
