using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

namespace Assets.Script.Data.Services {
    public interface ITokenStorage {
        UniTask SaveRefreshTokenAsync( string token );
        UniTask<string> LoadRefreshTokenAsync();
        UniTask ClearAsync();

    }
}