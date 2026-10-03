using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;
using Xunit;

namespace TienLen.Server.Tests {
    public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>> {
        private readonly WebApplicationFactory<Program> factory;
        private readonly Mock<IMongoDbContext> mockDb;
        private readonly Mock<IMongoCollection<UserDocument>> mockUsers;
        private readonly Mock<IMongoCollection<MatchRecordDocument>> mockMatches;
        private readonly List<UserDocument> databaseUsers;

        public ApiIntegrationTests( WebApplicationFactory<Program> factory ) {
            mockDb = new Mock<IMongoDbContext>();
            mockUsers = new Mock<IMongoCollection<UserDocument>>();
            mockMatches = new Mock<IMongoCollection<MatchRecordDocument>>();
            databaseUsers = new List<UserDocument>();

            mockUsers.Setup(c => c.InsertOneAsync(
                It.IsAny<UserDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<UserDocument, InsertOneOptions, CancellationToken>(( doc, _, _ ) => {
                    doc.Id = "int_user_" + (databaseUsers.Count + 1);
                    databaseUsers.Add(doc);
                })
                .Returns(Task.CompletedTask);

            mockUsers.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<FindOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(( FilterDefinition<UserDocument> f, FindOptions<UserDocument, UserDocument> o, CancellationToken ct ) => {
                    return CreateAsyncCursor(new List<UserDocument>(databaseUsers)).Object;
                });

            mockMatches.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<MatchRecordDocument>>(),
                It.IsAny<FindOptions<MatchRecordDocument, MatchRecordDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(( FilterDefinition<MatchRecordDocument> f, FindOptions<MatchRecordDocument, MatchRecordDocument> o, CancellationToken ct ) => {
                    return CreateAsyncCursor(new List<MatchRecordDocument>()).Object;
                });

            mockMatches.Setup(c => c.InsertOneAsync(
                It.IsAny<MatchRecordDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockDb.Setup(d => d.Users).Returns(mockUsers.Object);
            mockDb.Setup(d => d.Matches).Returns(mockMatches.Object);

            this.factory = factory.WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    services.AddSingleton(mockDb.Object);
                });
            });
        }

        [Fact]
        public async Task Register_WithEmailAndPassword_ReturnsOkWithDefaults() {
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto {
                Email = "integration@tienlen.com",
                Password = "Password123!"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
            auth.Should().NotBeNull();
            auth!.IsSuccess.Should().BeTrue();
            auth.Token.Should().NotBeNullOrEmpty();
            auth.Profile.Should().NotBeNull();
            auth.Profile!.Email.Should().Be("integration@tienlen.com");
            auth.Profile.DisplayName.Should().Be("integration");
            auth.Profile.AvatarId.Should().Be(0);
            auth.Profile.Money.Should().Be(10000);
            auth.Profile.Level.Should().Be(1);
        }

        [Fact]
        public async Task ReportMatch_ReturnsOk() {
            var client = factory.CreateClient();

            var report = new MatchResultReportDto {
                MatchId = "match_live_01",
                Results = new List<MatchPlayerSummaryDto> {
                    new MatchPlayerSummaryDto {
                        UserId = "int_user_1",
                        DisplayName = "PlayerOne",
                        AvatarId = 0,
                        Rank = 1,
                        MoneyEarned = 1000,
                        ExpEarned = 50
                    }
                }
            };

            var response = await client.PostAsJsonAsync("/api/matches/report", report);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var apiRes = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            apiRes.Should().NotBeNull();
            apiRes!.IsSuccess.Should().BeTrue();
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
