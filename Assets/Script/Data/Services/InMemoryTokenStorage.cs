using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

namespace Assets.Script.Data.Services {
    public class InMemoryTokenStorage : ITokenStorage {
        private string refreshToken;

        public UniTask SaveRefreshTokenAsync( string token ) {
            refreshToken = token;
            return UniTask.CompletedTask;
        }

        public UniTask<string> LoadRefreshTokenAsync() {
            return UniTask.FromResult(refreshToken);
        }

        public UniTask ClearAsync() {
            refreshToken = null;
            return UniTask.CompletedTask;
        }
    }

}