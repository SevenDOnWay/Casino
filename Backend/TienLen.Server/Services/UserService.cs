using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;

namespace TienLen.Server.Services {
    public class UserService : IUserService {
        private readonly IMongoDbContext db;

        public UserService( IMongoDbContext db ) {
            this.db = db;
        }

        public async Task<UserProfileDto?> GetProfileAsync( string userId ) {
            var userCursor = await db.Users.FindAsync(u => u.Id == userId);
            var user = await userCursor.FirstOrDefaultAsync();
            if ( user == null ) return null;

            // Fetch latest matches for user
            var matchCursor = await db.Matches.FindAsync(
                m => m.Results.Any(r => r.UserId == userId),
                new FindOptions<MatchRecordDocument, MatchRecordDocument> {
                    Limit = 10,
                    Sort = Builders<MatchRecordDocument>.Sort.Descending(m => m.PlayedAt)
                }
            );
            var matchDocs = await matchCursor.ToListAsync();

            var matchHistory = matchDocs.Select(m => {
                var myResult = m.Results.FirstOrDefault(r => r.UserId == userId);
                return new MatchHistoryItemDto {
                    MatchId = m.MatchId,
                    PlayedAt = m.PlayedAt.ToString("o"),
                    Rank = myResult?.Rank ?? 0,
                    MoneyEarned = myResult?.MoneyEarned ?? 0,
                    ExpEarned = myResult?.ExpEarned ?? 0,
                    Players = m.Results.Select(r => new MatchPlayerSummaryDto {
                        UserId = r.UserId,
                        DisplayName = r.DisplayName,
                        AvatarId = r.AvatarId,
                        Rank = r.Rank,
                        MoneyEarned = r.MoneyEarned,
                        ExpEarned = r.ExpEarned
                    }).ToList()
                };
            }).ToList();

            return MapToDto(user, matchHistory);
        }

        public async Task<UserProfileDto?> UpdateProfileAsync( string userId, UpdateProfileRequestDto request ) {
            var updateBuilder = Builders<UserDocument>.Update;
            var updates = new List<UpdateDefinition<UserDocument>>();

            if ( !string.IsNullOrWhiteSpace(request.DisplayName) ) {
                updates.Add(updateBuilder.Set(u => u.DisplayName, request.DisplayName.Trim()));
            }

            if ( request.AvatarId.HasValue ) {
                updates.Add(updateBuilder.Set(u => u.AvatarId, request.AvatarId.Value));
            }

            if ( updates.Count == 0 ) {
                return await GetProfileAsync(userId);
            }

            updates.Add(updateBuilder.Set(u => u.UpdatedAt, DateTime.UtcNow));
            var combinedUpdate = updateBuilder.Combine(updates);

            var updatedUser = await db.Users.FindOneAndUpdateAsync(
                u => u.Id == userId,
                combinedUpdate,
                new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After }
            );

            return updatedUser != null ? await GetProfileAsync(userId) : null;
        }

        public async Task<List<UserProfileDto>> GetFriendsAsync( string userId ) {
            var userCursor = await db.Users.FindAsync(u => u.Id == userId);
            var user = await userCursor.FirstOrDefaultAsync();
            if ( user == null || user.FriendIds == null || user.FriendIds.Count == 0 ) {
                return new List<UserProfileDto>();
            }

            var friendsCursor = await db.Users.FindAsync(u => user.FriendIds.Contains(u.Id));
            var friendDocs = await friendsCursor.ToListAsync();

            return friendDocs.Select(f => MapToDto(f, new List<MatchHistoryItemDto>())).ToList();
        }

        public async Task<bool> AddFriendAsync( string userId, string friendEmailOrName ) {
            string query = friendEmailOrName.Trim();
            var friendCursor = await db.Users.FindAsync(u =>
                u.Email == query.ToLowerInvariant() ||
                u.DisplayName == query
            );
            var friend = await friendCursor.FirstOrDefaultAsync();

            if ( friend == null || friend.Id == userId ) return false;

            var update = Builders<UserDocument>.Update.AddToSet(u => u.FriendIds, friend.Id);
            var result = await db.Users.UpdateOneAsync(u => u.Id == userId, update);

            return result.ModifiedCount > 0;
        }

        private static UserProfileDto MapToDto( UserDocument doc, List<MatchHistoryItemDto> matchHistory ) {
            return new UserProfileDto {
                Id = doc.Id,
                Email = doc.Email,
                DisplayName = doc.DisplayName,
                AvatarId = doc.AvatarId,
                Money = doc.Money,
                Level = doc.Level,
                Exp = doc.Exp,
                FriendIds = doc.FriendIds ?? new(),
                MatchHistory = matchHistory
            };
        }
    }
}
