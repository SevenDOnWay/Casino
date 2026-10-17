using MongoDB.Driver;
using TienLen.Server.Models;

namespace TienLen.Server.Data {
    /// <summary>
    /// Database context abstraction. Designed to decouple application services
    /// from underlying database providers (MongoDB, PostgreSQL, MySQL, etc.).
    /// </summary>
    public interface IDataBaseContext {
        IMongoCollection<UserDocument> Users { get; }
        IMongoCollection<MatchRecordDocument> Matches { get; }
        IMongoCollection<RefreshSessionDocument> RefreshSessions { get; }
        IMongoCollection<LobbySessionDocument> LobbySessions { get; }
    }

    /// <summary>
    /// PascalCase alias for IDataBaseContext.
    /// </summary>
    public interface IDatabaseContext : IDataBaseContext { }
}
