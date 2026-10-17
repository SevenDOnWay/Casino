using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
    public class AuthServiceTests {
        private readonly Mock<IDataBaseContext> mockDb;
        private readonly Mock<IMongoCollection<UserDocument>> mockUserCollection;
        private readonly Mock<IMongoCollection<RefreshSessionDocument>> mockRefreshCollection;
        private readonly Mock<IUserService> mockUserService;
        private readonly IConfiguration configuration;
        private readonly List<UserDocument> inMemoryUsers;
        private readonly List<RefreshSessionDocument> inMemorySessions;

        public AuthServiceTests() {
            mockDb = new Mock<IDataBaseContext>();
            mockUserCollection = new Mock<IMongoCollection<UserDocument>>();
            mockRefreshCollection = new Mock<IMongoCollection<RefreshSessionDocument>>();
            mockUserService = new Mock<IUserService>();
            inMemoryUsers = new List<UserDocument>();
            inMemorySessions = new List<RefreshSessionDocument>();

            var inMemorySettings = new Dictionary<string, string?> {
                {"Jwt:Key", "TienLenSecretSuperSecureKey2026!@#$%^&*()"},
                {"Jwt:Issuer", "TienLenServer"},
                {"Jwt:Audience", "TienLenClient"},
                {"Jwt:AccessTokenLifetimeMinutes", "15"}
            };
            configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            // Setup user insert
            mockUserCollection.Setup(c => c.InsertOneAsync(
                It.IsAny<UserDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<UserDocument, InsertOneOptions, CancellationToken>(( doc, _, _ ) => {
                    doc.Id = "mock_obj_id_" + (inMemoryUsers.Count + 1);
                    inMemoryUsers.Add(doc);
                })
                .Returns(Task.CompletedTask);

            // Setup session insert
            mockRefreshCollection.Setup(c => c.InsertOneAsync(
                It.IsAny<RefreshSessionDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<RefreshSessionDocument, InsertOneOptions, CancellationToken>(( doc, _, _ ) => {
                    inMemorySessions.Add(doc);
                })
                .Returns(Task.CompletedTask);

            mockDb.Setup(d => d.Users).Returns(mockUserCollection.Object);
            mockDb.Setup(d => d.RefreshSessions).Returns(mockRefreshCollection.Object);
        }

        [Fact]
        public async Task Register_WithEmailAndPasswordOnly_SetsDefaultPlayerStatsAndTokens() {
            // Arrange
            var mockCursor = CreateAsyncCursor(new List<UserDocument>());
            mockUserCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<FindOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCursor.Object);

            mockUserService.Setup(s => s.GetProfileAsync(It.IsAny<string>()))
                .ReturnsAsync(( string id ) => {
                    var u = inMemoryUsers.Find(x => x.Id == id);
                    if ( u == null ) return null;
                    return new UserProfileDto {
                        Id = u.Id,
                        Email = u.Email,
                        DisplayName = u.DisplayName,
                        AvatarId = u.AvatarId,
                        Money = u.Money,
                        Level = u.Level,
                        Exp = u.Exp
                    };
                });

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            var result = await authService.RegisterAsync(new RegisterRequestDto {
                Email = "cardmaster@tienlen.com",
                Password = "SuperSecretPassword123"
            });

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.Profile.Should().NotBeNull();
            result.Profile!.Email.Should().Be("cardmaster@tienlen.com");
            result.Profile.DisplayName.Should().Be("cardmaster");
            result.Profile.AvatarId.Should().Be(0);
            result.Profile.Money.Should().Be(10000);
            result.Profile.Level.Should().Be(1);
            inMemorySessions.Should().HaveCount(1);
        }

        [Fact]
        public async Task Register_WithExistingEmail_ReturnsFailure() {
            // Arrange
            var existingUser = new UserDocument {
                Id = "existing_1",
                Email = "existing@tienlen.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("any_password")
            };
            var mockCursor = CreateAsyncCursor(new List<UserDocument> { existingUser });
            mockUserCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<FindOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCursor.Object);

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            var result = await authService.RegisterAsync(new RegisterRequestDto {
                Email = "existing@tienlen.com",
                Password = "NewPassword123"
            });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("already exists");
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsSuccessAndTokens() {
            // Arrange
            string rawPassword = "ValidPassword123";
            var user = new UserDocument {
                Id = "user_123",
                Email = "valid@tienlen.com",
                DisplayName = "DragonSlayer",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword),
                AvatarId = 2,
                Money = 25000,
                Level = 3
            };

            var mockCursor = CreateAsyncCursor(new List<UserDocument> { user });
            mockUserCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<FindOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCursor.Object);

            mockUserService.Setup(s => s.GetProfileAsync("user_123"))
                .ReturnsAsync(new UserProfileDto {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    AvatarId = user.AvatarId,
                    Money = user.Money,
                    Level = user.Level
                });

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            var result = await authService.LoginAsync(new LoginRequestDto {
                Email = "valid@tienlen.com",
                Password = rawPassword
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.Profile!.DisplayName.Should().Be("DragonSlayer");
            result.Profile.Money.Should().Be(25000);
            inMemorySessions.Should().HaveCount(1);
        }

        [Fact]
        public async Task RefreshToken_WithValidToken_ReturnsRotatedTokens() {
            // Arrange
            string rawToken = "my_initial_refresh_token_123";
            string tokenHash = AuthService.HashToken(rawToken);

            var session = new RefreshSessionDocument {
                SessionId = "sess_1",
                UserId = "user_abc",
                CurrentTokenHash = tokenHash,
                ConsumedTokenHashes = new List<string>(),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                LastUsedAtUtc = DateTime.UtcNow
            };
            inMemorySessions.Add(session);

            var user = new UserDocument {
                Id = "user_abc",
                Email = "user_abc@tienlen.com"
            };

            // Setup session query
            mockRefreshCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<FindOptions<RefreshSessionDocument, RefreshSessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAsyncCursor(new List<RefreshSessionDocument> { session }).Object);

            // Setup user query
            mockUserCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<FindOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAsyncCursor(new List<UserDocument> { user }).Object);

            // Setup session update
            mockRefreshCollection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            var result = await authService.RefreshTokenAsync(new RefreshTokenRequestDto {
                RefreshToken = rawToken
            });

            // Assert
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBe(rawToken); // Rotated to a new value
        }

        [Fact]
        public async Task RefreshToken_WithExpiredToken_Fails() {
            // Arrange
            string rawToken = "expired_token";
            string tokenHash = AuthService.HashToken(rawToken);

            var expiredSession = new RefreshSessionDocument {
                SessionId = "sess_expired",
                UserId = "user_expired",
                CurrentTokenHash = tokenHash,
                ConsumedTokenHashes = new List<string>(),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-31),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
            };

            mockRefreshCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<FindOptions<RefreshSessionDocument, RefreshSessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAsyncCursor(new List<RefreshSessionDocument> { expiredSession }).Object);

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            var result = await authService.RefreshTokenAsync(new RefreshTokenRequestDto {
                RefreshToken = rawToken
            });

            // Assert
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("expired");
        }

        [Fact]
        public async Task RefreshToken_WithReusedTokenOutsideGracePeriod_RevokesAndFails() {
            // Arrange
            string reusedToken = "stolen_consumed_token";
            string tokenHash = AuthService.HashToken(reusedToken);

            var compromisedSession = new RefreshSessionDocument {
                SessionId = "sess_hacked",
                UserId = "user_hacked",
                CurrentTokenHash = "latest_hash_value",
                ConsumedTokenHashes = new List<string> { tokenHash },
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(28),
                LastUsedAtUtc = DateTime.UtcNow.AddMinutes(-5) // 5 minutes ago, well outside 15s grace window
            };

            mockRefreshCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<FindOptions<RefreshSessionDocument, RefreshSessionDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAsyncCursor(new List<RefreshSessionDocument> { compromisedSession }).Object);

            mockRefreshCollection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            var result = await authService.RefreshTokenAsync(new RefreshTokenRequestDto {
                RefreshToken = reusedToken
            });

            // Assert
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("reuse detected");
        }

        [Fact]
        public async Task RevokeRefreshToken_RevokesSession() {
            // Arrange
            string rawToken = "token_to_logout";

            mockRefreshCollection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateDefinition<RefreshSessionDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var authService = new AuthService(mockDb.Object, configuration, mockUserService.Object);

            // Act
            bool revoked = await authService.RevokeRefreshTokenAsync(new LogoutRequestDto {
                RefreshToken = rawToken
            });

            // Assert
            revoked.Should().BeTrue();
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
