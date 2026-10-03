using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TienLen.Server.Models;

namespace TienLen.Server.Data {
    public class MongoDbContext : IMongoDbContext {
        private readonly IMongoDatabase database;

        public MongoDbContext( IConfiguration configuration ) {
            string connectionString = configuration.GetConnectionString("MongoDb")
                ?? "mongodb://localhost:27017";
            string databaseName = configuration["MongoDb:DatabaseName"] ?? "TienLenDb";

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
    }
}
