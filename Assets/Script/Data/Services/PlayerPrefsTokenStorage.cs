using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Script.Data.Services {
    /// <summary>
    /// Persists refresh tokens across application restarts using Unity's PlayerPrefs.
    /// </summary>
    public class PlayerPrefsTokenStorage : ITokenStorage {
        private const string PrefKey = "TIENLEN_AUTH_REFRESH_TOKEN";

        public UniTask SaveRefreshTokenAsync( string token ) {
            if ( string.IsNullOrEmpty(token) ) {
                PlayerPrefs.DeleteKey(PrefKey);
            }
            else {
                PlayerPrefs.SetString(PrefKey, token);
            }
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask<string> LoadRefreshTokenAsync() {
            string token = PlayerPrefs.GetString(PrefKey, string.Empty);
            return UniTask.FromResult(token);
        }

        public UniTask ClearAsync() {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }
    }
}
