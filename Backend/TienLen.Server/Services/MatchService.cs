using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;

namespace TienLen.Server.Services {
    public class MatchService : IMatchService {
        private readonly IMongoDbContext db;

        public MatchService( IMongoDbContext db ) {
            this.db = db;
        }

        public async Task<bool> RecordMatchResultAsync( MatchResultReportDto report ) {
            if ( report == null || report.Results == null || report.Results.Count == 0 ) {
                return false;
            }

            // 1. Create match record document
            var matchDoc = new MatchRecordDocument {
                MatchId = string.IsNullOrWhiteSpace(report.MatchId) ? Guid.NewGuid().ToString("N") : report.MatchId,
                PlayedAt = DateTime.UtcNow,
                Results = report.Results.Select(r => new PlayerMatchResultDoc {
                    UserId = r.UserId,
                    DisplayName = r.DisplayName,
                    AvatarId = r.AvatarId,
                    Rank = r.Rank,
                    MoneyEarned = r.MoneyEarned,
                    ExpEarned = r.ExpEarned
                }).ToList()
            };

            await db.Matches.InsertOneAsync(matchDoc);

            // 2. Parallel atomic updates for each player in the match:
            // Uses MongoDB $inc operator so concurrent game endings never corrupt or race condition wallet balances!
            var updateTasks = report.Results.Select(async p => {
                if ( string.IsNullOrWhiteSpace(p.UserId) ) return;

                var filter = Builders<UserDocument>.Filter.Eq(u => u.Id, p.UserId);

                // Atomic increment of money and exp
                var update = Builders<UserDocument>.Update
                    .Inc(u => u.Money, p.MoneyEarned)
                    .Inc(u => u.Exp, p.ExpEarned)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var updatedUser = await db.Users.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After }
                );

                // Recalculate level if needed based on updated EXP (1 level per 100 EXP)
                if ( updatedUser != null ) {
                    int expectedLevel = Math.Max(1, 1 + (updatedUser.Exp / 100));
                    if ( updatedUser.Level != expectedLevel ) {
                        await db.Users.UpdateOneAsync(
                            filter,
                            Builders<UserDocument>.Update.Set(u => u.Level, expectedLevel)
                        );
                    }
                }
            });

            await Task.WhenAll(updateTasks);
            return true;
        }
    }
}
