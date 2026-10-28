using FluentAssertions;
using Moq;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;
using TienLen.Server.Services;
using Xunit;

namespace TienLen.Server.Tests.Stress {
    [Trait("Category", "Stress")]
    public class LobbyStressTests {
        private readonly Mock<IDataBaseContext> mockDb;
        private readonly Mock<IMongoCollection<LobbySessionDocument>> mockLobbyCollection;
        private readonly ConcurrentDictionary<string, LobbySessionDocument> threadSafeSessions;
        private readonly LobbyService lobbyService;

        public LobbyStressTests() {
            mockDb = new Mock<IDataBaseContext>();
            mockLobbyCollection = new Mock<IMongoCollection<LobbySessionDocument>>();
            threadSafeSessions = new ConcurrentDictionary<string, LobbySessionDocument>();

            mockDb.Setup(db => db.LobbySessions).Returns(mockLobbyCollection.Object);

            // Thread-safe FindAsync mock (respecting PlayerCount descending sort)
            mockLobbyCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<LobbySessionDocument>>(),
                It.IsAny<FindOptions<LobbySessionDocument, LobbySessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(( FilterDefinition<LobbySessionDocument> filter, FindOptions<LobbySessionDocument, LobbySessionDocument> options, CancellationToken ct ) => {
                    var snapshot = threadSafeSessions.Values.OrderByDescending(s => s.PlayerCount).ToList();
                    return CreateAsyncCursor(snapshot).Object;
                });

            // Thread-safe UpdateOneAsync (Register / Heartbeat)
            mockLobbyCollection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<LobbySessionDocument>>(),
                It.IsAny<UpdateDefinition<LobbySessionDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            // Thread-safe DeleteOneAsync (Unregister)
            mockLobbyCollection.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<LobbySessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(1));

            lobbyService = new LobbyService(mockDb.Object);
        }

        [Fact]
        public async Task StressTest_120TablesRegistration_AutoShardsAcrossFourChannels() {
            int betStake = 5;
            int totalTables = 120; // 4 channels x 30 tables
            int concurrency = 20;

            // Concurrently populate 120 tables
            await Parallel.ForEachAsync(Enumerable.Range(1, totalTables), new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async ( i, ct ) => {
                int assignedChannel = ((i - 1) / 30) + 1;
                var session = new LobbySessionDocument {
                    SessionName = $"STRESS_ROOM_{i:D4}",
                    BetStake = betStake,
                    Channel = assignedChannel,
                    PlayerCount = 2,
                    MaxPlayers = 4,
                    ExpireAt = DateTime.UtcNow.AddSeconds(30)
                };

                threadSafeSessions[session.SessionName] = session;
                await lobbyService.RegisterSessionAsync($"host_{i}", new RegisterSessionRequest(
                    SessionName: session.SessionName,
                    BetStake: betStake,
                    Channel: assignedChannel,
                    PlayerCount: 2,
                    MaxPlayers: 4
                ));
            });

            threadSafeSessions.Count.Should().Be(120);

            // Sharding calculation under load
            var channelResult = await lobbyService.GetChannelsAsync(betStake);
            channelResult.Should().NotBeNull();
            channelResult.Channels.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
            channelResult.RecommendedChannel.Should().Be(5, "Channels 1 to 4 are completely saturated with 30 tables each, so auto-sharding should recommend Channel 5");

            // Verify channel 1 contains 30 tables
            var ch1Tables = threadSafeSessions.Values.Where(s => s.Channel == 1).ToList();
            ch1Tables.Count.Should().Be(30);

            // Simulate one table closing in Channel 2 (now Channel 2 has 29 tables)
            var roomToRemove = threadSafeSessions.Values.First(s => s.Channel == 2);
            threadSafeSessions.TryRemove(roomToRemove.SessionName, out _);

            var updatedResult = await lobbyService.GetChannelsAsync(betStake);
            updatedResult.RecommendedChannel.Should().Be(2, "Channel 2 now has vacancy (< 30 tables), so it must be recommended over Channel 5");
        }

        [Fact]
        public async Task StressTest_HighThroughputServerBrowser_CompletesWithoutDeadlock() {
            int betStake = 10;
            // Pre-seed 60 tables (2 channels)
            for ( int i = 1; i <= 60; i++ ) {
                int ch = ((i - 1) / 30) + 1;
                string room = $"BROWSE_ROOM_{i}";
                threadSafeSessions[room] = new LobbySessionDocument {
                    SessionName = room,
                    BetStake = betStake,
                    Channel = ch,
                    PlayerCount = 3,
                    MaxPlayers = 4
                };
            }

            int queryCount = 500;
            int concurrency = 25;
            var sw = Stopwatch.StartNew();

            await Parallel.ForEachAsync(Enumerable.Range(1, queryCount), new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async ( i, ct ) => {
                if ( i % 2 == 0 ) {
                    var channels = await lobbyService.GetChannelsAsync(betStake);
                    channels.Should().NotBeNull();
                    channels.Channels.Count.Should().BeGreaterThanOrEqualTo(2);
                }
                else {
                    var sessions = await lobbyService.GetSessionsAsync(betStake, (i % 2) + 1);
                    sessions.Should().NotBeNull();
                }
            });

            sw.Stop();
            // 500 in-memory queries should complete rapidly (typically < 1000ms)
            sw.ElapsedMilliseconds.Should().BeLessThan(3000, "500 concurrent browser queries must execute swiftly without lock contention");
        }

        [Fact]
        public async Task StressTest_ConcurrentQuickJoin_PicksOptimalTables() {
            int betStake = 25;
            // Setup 3 rooms with varying capacity
            threadSafeSessions["ROOM_A"] = new LobbySessionDocument {
                SessionName = "ROOM_A",
                BetStake = betStake,
                Channel = 1,
                PlayerCount = 1,
                MaxPlayers = 4
            };
            threadSafeSessions["ROOM_B"] = new LobbySessionDocument {
                SessionName = "ROOM_B",
                BetStake = betStake,
                Channel = 1,
                PlayerCount = 3, // Fullest open table
                MaxPlayers = 4
            };

            // Run 50 concurrent quick join requests
            var results = new ConcurrentBag<QuickJoinResponse>();
            await Parallel.ForEachAsync(Enumerable.Range(1, 50), new ParallelOptions { MaxDegreeOfParallelism = 10 }, async ( _, ct ) => {
                var qj = await lobbyService.QuickJoinAsync(betStake);
                results.Add(qj);
            });

            results.Count.Should().Be(50);
            results.Should().OnlyContain(r => r.Found == true && r.Action == "Join" && r.SessionName == "ROOM_B",
                "Quick-join under high concurrency must deterministically pick the fullest open room (3/4 players) to fill matches fastest");
        }

        private static Mock<IAsyncCursor<T>> CreateAsyncCursor<T>( List<T> items ) {
            var mockCursor = new Mock<IAsyncCursor<T>>();
            mockCursor.Setup(c => c.Current).Returns(items);
            mockCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            return mockCursor;
        }
    }
}
