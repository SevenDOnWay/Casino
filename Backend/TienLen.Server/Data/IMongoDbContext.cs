using MongoDB.Driver;
using TienLen.Server.Models;

namespace TienLen.Server.Data {
    public interface IMongoDbContext {
        IMongoCollection<UserDocument> Users { get; }
        IMongoCollection<MatchRecordDocument> Matches { get; }
    }
}
