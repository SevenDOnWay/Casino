using System.Collections.Generic;

namespace TienLen.Server.DTOs {
    public class UserProfileDto {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int AvatarId { get; set; }
        public long Money { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public List<string> FriendIds { get; set; } = new();
        public List<MatchHistoryItemDto> MatchHistory { get; set; } = new();
    }

    public class UpdateProfileRequestDto {
        public string? DisplayName { get; set; }
        public int? AvatarId { get; set; }
    }

    public class AddFriendRequestDto {
        public string FriendEmailOrName { get; set; } = string.Empty;
    }
}
