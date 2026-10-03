using System;
using System.Collections.Generic;

namespace Assets.Script.Data.Models {
    [Serializable]
    public class PlayerProfileData {
        public string id;
        public string email;
        public string displayName;
        public int avatarId;
        public long money;
        public int level;
        public int exp;
        public List<string> friendIds = new List<string>();
        public List<MatchHistoryItem> matchHistory = new List<MatchHistoryItem>();
    }
}
