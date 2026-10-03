using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TienLen.Server.Models {
    public class UserDocument {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [BsonElement("avatarId")]
        public int AvatarId { get; set; } = 0;

        [BsonElement("money")]
        public long Money { get; set; } = 10000;

        [BsonElement("level")]
        public int Level { get; set; } = 1;

        [BsonElement("exp")]
        public int Exp { get; set; } = 0;

        [BsonElement("friendIds")]
        public List<string> FriendIds { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
