using FluentAssertions;
using Moq;
using MongoDB.Driver;
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
    public class ParallelMatchServiceTests {
        [Fact]
        public async Task ConcurrentMatchReports_ExecuteInParallelWithoutRaceConditions() {
            // Arrange
            var mockDb = new Mock<IMongoDbContext>();
            var mockUserCollection = new Mock<IMongoCollection<UserDocument>>();
            var mockMatchCollection = new Mock<IMongoCollection<MatchRecordDocument>>();

            var inMemoryUsers = new Dictionary<string, UserDocument> {
                { "player_1", new UserDocument { Id = "player_1", DisplayName = "Alice", Money = 10000, Exp = 0, Level = 1 } },
                { "player_2", new UserDocument { Id = "player_2", DisplayName = "Bob", Money = 10000, Exp = 0, Level = 1 } },
                { "player_3", new UserDocument { Id = "player_3", DisplayName = "Charlie", Money = 10000, Exp = 0, Level = 1 } },
                { "player_4", new UserDocument { Id = "player_4", DisplayName = "David", Money = 10000, Exp = 0, Level = 1 } }
            };

            var matchRecords = new List<MatchRecordDocument>();
            object stateLock = new object();

            mockMatchCollection.Setup(c => c.InsertOneAsync(
                It.IsAny<MatchRecordDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<MatchRecordDocument, InsertOneOptions, CancellationToken>(( doc, _, _ ) => {
                    lock ( stateLock ) {
                        matchRecords.Add(doc);
                    }
                })
                .Returns(Task.CompletedTask);

            mockUserCollection.Setup(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<UpdateDefinition<UserDocument>>(),
                It.IsAny<FindOneAndUpdateOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(inMemoryUsers.Values.First());

            mockDb.Setup(d => d.Users).Returns(mockUserCollection.Object);
            mockDb.Setup(d => d.Matches).Returns(mockMatchCollection.Object);

            var matchService = new MatchService(mockDb.Object);

            // Act - Fire 50 parallel match outcome settlements concurrently
            int parallelMatches = 50;
            var tasks = new List<Task<bool>>();

            for ( int i = 0; i < parallelMatches; i++ ) {
                int matchIndex = i;
                var report = new MatchResultReportDto {
                    MatchId = $"match_batch_{matchIndex}",
                    Results = new List<MatchPlayerSummaryDto> {
                        new MatchPlayerSummaryDto { UserId = "player_1", Rank = 1, MoneyEarned = 1000, ExpEarned = 50 },
                        new MatchPlayerSummaryDto { UserId = "player_2", Rank = 2, MoneyEarned = -300, ExpEarned = 15 },
                        new MatchPlayerSummaryDto { UserId = "player_3", Rank = 3, MoneyEarned = -300, ExpEarned = 15 },
                        new MatchPlayerSummaryDto { UserId = "player_4", Rank = 4, MoneyEarned = -400, ExpEarned = 10 }
                    }
                };

                tasks.Add(Task.Run(() => matchService.RecordMatchResultAsync(report)));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(parallelMatches);
            results.Should().AllSatisfy(r => r.Should().BeTrue());
            matchRecords.Should().HaveCount(parallelMatches);
        }
    }
}
