using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TienLen.Server.Models {
    public class MatchRecordDocument {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("matchId")]
        public string MatchId { get; set; } = string.Empty;

        [BsonElement("playedAt")]
        public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("results")]
        public List<PlayerMatchResultDoc> Results { get; set; } = new();
    }

    public class PlayerMatchResultDoc {
        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [BsonElement("avatarId")]
        public int AvatarId { get; set; }

        [BsonElement("rank")]
        public int Rank { get; set; }

        [BsonElement("moneyEarned")]
        public long MoneyEarned { get; set; }

        [BsonElement("expEarned")]
        public int ExpEarned { get; set; }
    }
}
