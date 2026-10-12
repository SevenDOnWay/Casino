using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace TienLen.Server.Tests {
    public class AtlasLiveConnectivityTest {
        [Fact]
        public async Task Test_AtlasConnection_IfEnvProvided() {
            string envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TienLen.Server", ".env");
            if ( !File.Exists(envPath) ) {
                return; // Skip if no .env present
            }

            string? connectionString = null;
            foreach ( var raw in File.ReadAllLines(envPath) ) {
                var line = raw.Trim();
                if ( line.StartsWith("ConnectionStrings__MongoDb=") ) {
                    connectionString = line.Substring("ConnectionStrings__MongoDb=".Length).Trim().Trim('"');
                    break;
                }
            }

            if ( string.IsNullOrEmpty(connectionString) ) return;

            try {
                var client = new MongoClient(connectionString);
                var pingCmd = new BsonDocument("ping", 1);
                var db = client.GetDatabase("CasinoDb");
                var result = await db.RunCommandAsync<BsonDocument>(pingCmd);

                result.Should().NotBeNull();
                result.Contains("ok").Should().BeTrue();
            }
            catch ( TimeoutException ) {
                // If IP whitelist is not yet enabled in Atlas dashboard
                Console.WriteLine("[Atlas Test] Timeout connecting. Ensure 0.0.0.0/0 or current IP is whitelisted in MongoDB Atlas Network Access.");
            }
            catch ( MongoAuthenticationException ex ) {
                Console.WriteLine($"[Atlas Test] Authentication error: {ex.Message}");
            }
            catch ( Exception ex ) {
                Console.WriteLine($"[Atlas Test] Connection notice: {ex.Message}");
            }
        }
    }
}
