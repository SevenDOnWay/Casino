using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;
using TienLen.Server.Services;
using Xunit;

namespace TienLen.Server.Tests {
    public class AuthServiceTests {
        private readonly Mock<IMongoDbContext> mockDb;
        private readonly Mock<IMongoCollection<UserDocument>> mockUserCollection;
        private readonly Mock<IUserService> mockUserService;
        private readonly IConfiguration configuration;
        private readonly List<UserDocument> inMemoryUsers;

        public AuthServiceTests() {
            mockDb = new Mock<IMongoDbContext>();
            mockUserCollection = new Mock<IMongoCollection<UserDocument>>();
            mockUserService = new Mock<IUserService>();
            inMemoryUsers = new List<UserDocument>();

            var inMemorySettings = new Dictionary<string, string?> {
                {"Jwt:Key", "TienLenSecretSuperSecureKey2026!@#$%^&*()"},
                {"Jwt:Issuer", "TienLenServer"},
                {"Jwt:Audience", "TienLenClient"}
            };
            configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            // Setup insert
            mockUserCollection.Setup(c => c.InsertOneAsync(
                It.IsAny<UserDocument>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
                .Callback<UserDocument, InsertOneOptions, CancellationToken>(( doc, _, _ ) => {
                    doc.Id = "mock_obj_id_" + (inMemoryUsers.Count + 1);
                    inMemoryUsers.Add(doc);
                })
                .Returns(Task.CompletedTask);

            mockDb.Setup(d => d.Users).Returns(mockUserCollection.Object);
        }

        [Fact]
        public async Task Register_WithEmailAndPasswordOnly_SetsDefaultPlayerStats() {
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
            result.Token.Should().NotBeNullOrEmpty();
            result.Profile.Should().NotBeNull();
            result.Profile!.Email.Should().Be("cardmaster@tienlen.com");
            result.Profile.DisplayName.Should().Be("cardmaster"); // default from email
            result.Profile.AvatarId.Should().Be(0);               // default avatar
            result.Profile.Money.Should().Be(10000);             // default starting money
            result.Profile.Level.Should().Be(1);                 // default level 1
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
        public async Task Login_WithValidCredentials_ReturnsSuccessAndJwt() {
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
            result.Token.Should().NotBeNullOrEmpty();
            result.Profile!.DisplayName.Should().Be("DragonSlayer");
            result.Profile.Money.Should().Be(25000);
        }

        private static Mock<IAsyncCursor<T>> CreateAsyncCursor<T>( List<T> items ) {
            var mockCursor = new Mock<IAsyncCursor<T>>();
            var enumerator = items.GetEnumerator();

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
