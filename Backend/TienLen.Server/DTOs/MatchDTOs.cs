using System.Collections.Generic;

namespace TienLen.Server.DTOs {
    public class MatchPlayerSummaryDto {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int AvatarId { get; set; }
        public int Rank { get; set; }
        public long MoneyEarned { get; set; }
        public int ExpEarned { get; set; }
    }

    public class MatchResultReportDto {
        public string MatchId { get; set; } = string.Empty;
        public List<MatchPlayerSummaryDto> Results { get; set; } = new();
    }

    public class MatchHistoryItemDto {
        public string MatchId { get; set; } = string.Empty;
        public string PlayedAt { get; set; } = string.Empty;
        public int Rank { get; set; }
        public long MoneyEarned { get; set; }
        public int ExpEarned { get; set; }
        public List<MatchPlayerSummaryDto> Players { get; set; } = new();
    }
}
