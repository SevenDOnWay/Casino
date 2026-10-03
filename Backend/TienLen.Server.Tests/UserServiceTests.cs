using FluentAssertions;
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
    public class UserServiceTests {
        [Fact]
        public async Task UpdateProfile_CustomizesDisplayNameAndAvatarId() {
            // Arrange
            var mockDb = new Mock<IMongoDbContext>();
            var mockUserCollection = new Mock<IMongoCollection<UserDocument>>();
            var mockMatchCollection = new Mock<IMongoCollection<MatchRecordDocument>>();

            var user = new UserDocument {
                Id = "user_456",
                Email = "player@tienlen.com",
                DisplayName = "OldName",
                AvatarId = 0,
                Money = 10000,
                Level = 1,
                Exp = 0
            };

            mockUserCollection.Setup(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<UpdateDefinition<UserDocument>>(),
                It.IsAny<FindOneAndUpdateOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<UserDocument>, UpdateDefinition<UserDocument>, FindOneAndUpdateOptions<UserDocument, UserDocument>, CancellationToken>(
                    ( filter, update, options, ct ) => {
                        user.DisplayName = "CyberAce";
                        user.AvatarId = 5;
                    })
                .ReturnsAsync(user);

            var mockCursor = CreateAsyncCursor(new List<UserDocument> { user });
            mockUserCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserDocument>>(),
                It.IsAny<FindOptions<UserDocument, UserDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockCursor.Object);

            var mockMatchCursor = CreateAsyncCursor(new List<MatchRecordDocument>());
            mockMatchCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<MatchRecordDocument>>(),
                It.IsAny<FindOptions<MatchRecordDocument, MatchRecordDocument>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockMatchCursor.Object);

            mockDb.Setup(d => d.Users).Returns(mockUserCollection.Object);
            mockDb.Setup(d => d.Matches).Returns(mockMatchCollection.Object);

            var userService = new UserService(mockDb.Object);

            // Act
            var result = await userService.UpdateProfileAsync("user_456", new UpdateProfileRequestDto {
                DisplayName = "CyberAce",
                AvatarId = 5
            });

            // Assert
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("CyberAce");
            result.AvatarId.Should().Be(5);
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
