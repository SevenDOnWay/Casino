using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TienLen.Server.Models {
    /// <summary>
    /// Represents a live game session indexed in the hierarchical lobby directory.
    /// Level 1: BetStake (Stake partition)
    /// Level 2: Channel (Load partition, max 30 tables)
    /// Level 3: SessionName (Individual Fusion Room)
    /// </summary>
    public class LobbySessionDocument {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("betStake")]
        public int BetStake { get; set; }

        [BsonElement("channel")]
        public int Channel { get; set; } = 1;

        [BsonElement("sessionName")]
        public string SessionName { get; set; } = string.Empty;

        [BsonElement("playerCount")]
        public int PlayerCount { get; set; } = 1;

        [BsonElement("maxPlayers")]
        public int MaxPlayers { get; set; } = 4;

        [BsonElement("hostUserId")]
        public string HostUserId { get; set; } = string.Empty;

        /// <summary>
        /// MongoDB TTL Index field: MongoDB automatically purges documents when ExpireAt &lt; DateTime.UtcNow.
        /// </summary>
        [BsonElement("expireAt")]
        public DateTime ExpireAt { get; set; } = DateTime.UtcNow.AddSeconds(30);

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
