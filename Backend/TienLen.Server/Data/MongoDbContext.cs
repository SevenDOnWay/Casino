using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TienLen.Server.Models;

namespace TienLen.Server.Data {
#pragma warning disable CS0618
    public class MongoDbContext : IDataBaseContext, IDatabaseContext, IMongoDbContext {
#pragma warning restore CS0618
        private readonly IMongoDatabase database;

        public MongoDbContext( IConfiguration configuration ) {
            string connectionString = configuration.GetConnectionString("MongoDb")
                ?? configuration["ConnectionStrings:MongoDb"]
                ?? configuration["ConnectionStrings__MongoDb"]
                ?? "mongodb://localhost:27017";

            string databaseName = configuration["MongoDb:DatabaseName"]
                ?? configuration["MongoDb__DatabaseName"]
                ?? "CasinoDb";

            var client = new MongoClient(connectionString);
            database = client.GetDatabase(databaseName);
        }

        public MongoDbContext( IMongoDatabase database ) {
            this.database = database;
        }

        public IMongoCollection<UserDocument> Users =>
            database.GetCollection<UserDocument>("users");

        public IMongoCollection<MatchRecordDocument> Matches =>
            database.GetCollection<MatchRecordDocument>("match_history");

        public IMongoCollection<RefreshSessionDocument> RefreshSessions =>
            database.GetCollection<RefreshSessionDocument>("refresh_sessions");

        public IMongoCollection<LobbySessionDocument> LobbySessions =>
            database.GetCollection<LobbySessionDocument>("lobby_sessions");

        public async System.Threading.Tasks.Task EnsureIndexesAsync() {
            try {
                // 1. Compound index for bet + channel queries
                var compoundIndex = new CreateIndexModel<LobbySessionDocument>(
                    Builders<LobbySessionDocument>.IndexKeys
                        .Ascending(s => s.BetStake)
                        .Ascending(s => s.Channel)
                );

                // 2. Unique index on SessionName
                var uniqueIndex = new CreateIndexModel<LobbySessionDocument>(
                    Builders<LobbySessionDocument>.IndexKeys.Ascending(s => s.SessionName),
                    new CreateIndexOptions { Unique = true }
                );

                // 3. TTL Index on ExpireAt (expire immediately when ExpireAt < now)
                var ttlIndex = new CreateIndexModel<LobbySessionDocument>(
                    Builders<LobbySessionDocument>.IndexKeys.Ascending(s => s.ExpireAt),
                    new CreateIndexOptions { ExpireAfter = System.TimeSpan.Zero }
                );

                await LobbySessions.Indexes.CreateManyAsync(new[] { compoundIndex, uniqueIndex, ttlIndex });
            }
            catch ( System.Exception ex ) {
                System.Console.WriteLine($"[MongoDbContext] Index creation notice: {ex.Message}");
            }
        }
    }
}
