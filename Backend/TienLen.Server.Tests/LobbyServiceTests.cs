using FluentAssertions;
using Moq;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;
using TienLen.Server.Services;
using Xunit;

namespace TienLen.Server.Tests {
    public class LobbyServiceTests {
        private readonly Mock<IDataBaseContext> mockDb;
        private readonly Mock<IMongoCollection<LobbySessionDocument>> mockLobbyCollection;
        private readonly List<LobbySessionDocument> inMemorySessions;
        private readonly LobbyService lobbyService;

        public LobbyServiceTests() {
            mockDb = new Mock<IDataBaseContext>();
            mockLobbyCollection = new Mock<IMongoCollection<LobbySessionDocument>>();
            inMemorySessions = new List<LobbySessionDocument>();

            mockDb.Setup(db => db.LobbySessions).Returns(mockLobbyCollection.Object);

            mockLobbyCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<LobbySessionDocument>>(),
                It.IsAny<FindOptions<LobbySessionDocument, LobbySessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(( FilterDefinition<LobbySessionDocument> filter, FindOptions<LobbySessionDocument, LobbySessionDocument> options, CancellationToken ct ) => {
                    return CreateAsyncCursor(new List<LobbySessionDocument>(inMemorySessions)).Object;
                });

            mockLobbyCollection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<LobbySessionDocument>>(),
                It.IsAny<UpdateDefinition<LobbySessionDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            mockLobbyCollection.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<LobbySessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(1));

            lobbyService = new LobbyService(mockDb.Object);
        }

        [Fact]
        public async Task GetChannelsAsync_WhenNoSessions_ReturnsChannelOne() {
            var response = await lobbyService.GetChannelsAsync(5);

            response.Should().NotBeNull();
            response.BetStake.Should().Be(5);
            response.Channels.Should().ContainSingle().Which.Should().Be(1);
            response.RecommendedChannel.Should().Be(1);
        }

        [Fact]
        public async Task GetChannelsAsync_WhenChannelOneCrowded_AutoShardsToChannelTwo() {
            // Populate Channel 1 with 30 tables
            for ( int i = 0; i < 30; i++ ) {
                inMemorySessions.Add(new LobbySessionDocument {
                    SessionName = $"Room_{i}",
                    BetStake = 5,
                    Channel = 1,
                    PlayerCount = 2,
                    MaxPlayers = 4
                });
            }

            var response = await lobbyService.GetChannelsAsync(5);

            response.Should().NotBeNull();
            response.Channels.Should().BeEquivalentTo(new[] { 1, 2 });
            response.RecommendedChannel.Should().Be(2); // Auto-sharded to Channel 2!
        }

        [Fact]
        public async Task RegisterSessionAsync_ValidRequest_ReturnsTrue() {
            var request = new RegisterSessionRequest(
                SessionName: "TL-NEW1",
                BetStake: 10,
                Channel: 1,
                PlayerCount: 1,
                MaxPlayers: 4
            );

            bool success = await lobbyService.RegisterSessionAsync("host_123", request);
            success.Should().BeTrue();
        }

        [Fact]
        public async Task HeartbeatAsync_ValidSession_Acknowledges() {
            bool success = await lobbyService.HeartbeatAsync("TL-NEW1", 3);
            success.Should().BeTrue();
        }

        [Fact]
        public async Task UnregisterSessionAsync_ExistingSession_ReturnsTrue() {
            bool success = await lobbyService.UnregisterSessionAsync("TL-NEW1");
            success.Should().BeTrue();
        }

        [Fact]
        public async Task QuickJoinAsync_WhenTableHasSpace_ReturnsJoinAction() {
            inMemorySessions.Add(new LobbySessionDocument {
                SessionName = "TL-JOINABLE",
                BetStake = 20,
                Channel = 1,
                PlayerCount = 3,
                MaxPlayers = 4
            });

            var response = await lobbyService.QuickJoinAsync(20);

            response.Should().NotBeNull();
            response.Found.Should().BeTrue();
            response.Action.Should().Be("Join");
            response.SessionName.Should().Be("TL-JOINABLE");
            response.Channel.Should().Be(1);
        }

        [Fact]
        public async Task QuickJoinAsync_WhenNoTableHasSpace_ReturnsHostAction() {
            var response = await lobbyService.QuickJoinAsync(50);

            response.Should().NotBeNull();
            response.Found.Should().BeFalse();
            response.Action.Should().Be("Host");
            response.SessionName.Should().BeNull();
            response.Channel.Should().Be(1);
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
