using Cysharp.Threading.Tasks;

namespace Assets.Script.Data.Repositories {
    public interface IDataRepository<T> {
        UniTask<T> GetByIdAsync( string id );
        UniTask<bool> UpdateAsync( string id, T data );
    }
}
