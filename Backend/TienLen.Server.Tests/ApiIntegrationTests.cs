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
        private readonly Mock<IDataBaseContext> mockDb;
        private readonly Mock<IMongoCollection<UserDocument>> mockUsers;
        private readonly Mock<IMongoCollection<MatchRecordDocument>> mockMatches;
        private readonly Mock<IMongoCollection<RefreshSessionDocument>> mockRefreshSessions;
        private readonly List<UserDocument> databaseUsers;
        private readonly List<RefreshSessionDocument> databaseSessions;

        public ApiIntegrationTests( WebApplicationFactory<Program> factory ) {
            mockDb = new Mock<IDataBaseContext>();
            mockUsers = new Mock<IMongoCollection<UserDocument>>();
            mockMatches = new Mock<IMongoCollection<MatchRecordDocument>>();
            mockRefreshSessions = new Mock<IMongoCollection<RefreshSessionDocument>>();
            databaseUsers = new List<UserDocument>();
            databaseSessions = new List<RefreshSessionDocument>();

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

            mockRefreshSessions.Setup(c => c.InsertOneAsync(
                It.IsAny<RefreshSessionDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<RefreshSessionDocument, InsertOneOptions, CancellationToken>(( doc, _, _ ) => {
                    databaseSessions.Add(doc);
                })
                .Returns(Task.CompletedTask);

            mockRefreshSessions.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<FindOptions<RefreshSessionDocument, RefreshSessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(( FilterDefinition<RefreshSessionDocument> f, FindOptions<RefreshSessionDocument, RefreshSessionDocument> o, CancellationToken ct ) => {
                    return CreateAsyncCursor(new List<RefreshSessionDocument>(databaseSessions)).Object;
                });

            mockRefreshSessions.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            mockDb.Setup(d => d.Users).Returns(mockUsers.Object);
            mockDb.Setup(d => d.Matches).Returns(mockMatches.Object);
            mockDb.Setup(d => d.RefreshSessions).Returns(mockRefreshSessions.Object);

            var dbAsDatabase = mockDb.As<IDatabaseContext>();
#pragma warning disable CS0618
            var dbAsMongo = mockDb.As<IMongoDbContext>();
#pragma warning restore CS0618

            this.factory = factory.WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    services.AddSingleton(mockDb.Object);
                    services.AddSingleton<IDatabaseContext>(dbAsDatabase.Object);
#pragma warning disable CS0618
                    services.AddSingleton<IMongoDbContext>(dbAsMongo.Object);
#pragma warning restore CS0618
                });
            });
        }

        [Fact]
        public async Task Register_WithEmailAndPassword_ReturnsOkWithDefaultsAndTokens() {
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto {
                Email = "integration@tienlen.com",
                Password = "Password123!"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
            auth.Should().NotBeNull();
            auth!.IsSuccess.Should().BeTrue();
            auth.AccessToken.Should().NotBeNullOrEmpty();
            auth.RefreshToken.Should().NotBeNullOrEmpty();
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

        [Fact]
        public async Task Refresh_WithValidToken_ReturnsRotatedToken() {
            var client = factory.CreateClient();

            // 1. Register to obtain initial token pair
            var regResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto {
                Email = "refresher@tienlen.com",
                Password = "Password123!"
            });

            regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            auth.Should().NotBeNull();
            auth!.RefreshToken.Should().NotBeNullOrEmpty();

            // 2. Call refresh endpoint
            var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto {
                RefreshToken = auth.RefreshToken
            });

            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var refreshed = await refreshResponse.Content.ReadFromJsonAsync<TokenRefreshResponseDto>();
            refreshed.Should().NotBeNull();
            refreshed!.IsSuccess.Should().BeTrue();
            refreshed.AccessToken.Should().NotBeNullOrEmpty();
            refreshed.RefreshToken.Should().NotBeNullOrEmpty();
            refreshed.RefreshToken.Should().NotBe(auth.RefreshToken);
        }

        [Fact]
        public async Task Logout_WithRefreshToken_ReturnsSuccess() {
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequestDto {
                RefreshToken = "some_valid_or_dummy_token"
            });

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
