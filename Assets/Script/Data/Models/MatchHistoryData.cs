using System;
using System.Collections.Generic;

namespace Assets.Script.Data.Models {
    [Serializable]
    public class MatchHistoryItem {
        public string matchId;
        public string playedAt;
        public int rank;
        public long moneyEarned;
        public int expEarned;
        public List<MatchPlayerSummary> players = new List<MatchPlayerSummary>();
    }

    [Serializable]
    public class MatchPlayerSummary {
        public string userId;
        public string displayName;
        public int avatarId;
        public int rank;
        public long moneyEarned;
        public int expEarned;
    }

    [Serializable]
    public class MatchResultReportRequest {
        public string matchId;
        public List<MatchPlayerSummary> results = new List<MatchPlayerSummary>();
    }
}
