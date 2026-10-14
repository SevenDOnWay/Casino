using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TienLen.Server.Models;

public class RefreshSessionDocument {
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    // Hash of the currently valid refresh token.
    [BsonElement("currentTokenHash")]
    public string CurrentTokenHash { get; set; } = string.Empty;

    // Hashes of previously consumed tokens for replay detection.
    [BsonElement("consumedTokenHashes")]
    public List<string> ConsumedTokenHashes { get; set; } = new();

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }
    
    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [BsonElement("revokedAtUtc")]
    public DateTime? RevokedAtUtc { get; set; }

    [BsonElement("lastUsedAtUtc")]
    public DateTime? LastUsedAtUtc { get; set; }
}
