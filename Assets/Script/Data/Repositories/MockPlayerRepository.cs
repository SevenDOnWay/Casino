using Assets.Script.Data.Models;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Script.Data.Repositories {
    /// <summary>
    /// In-memory mock repository for local editor play and offline testing.
    /// </summary>
    public class MockPlayerRepository : IPlayerRepository {
        private readonly Dictionary<string, (string password, PlayerProfileData profile)> users = new();
        private int idCounter = 1;

        public MockPlayerRepository() {
            // Seed a test user
            var defaultUser = new PlayerProfileData {
                id = "mock_user_1",
                email = "player@test.com",
                displayName = "AcePlayer",
                avatarId = 0,
                money = 15000,
                level = 1,
                exp = 0,
                friendIds = new List<string>()
            };
            users[defaultUser.email.ToLower()] = ("password123", defaultUser);
        }

        public async UniTask<AuthResponse> LoginAsync( LoginRequest request ) {
            await UniTask.Yield();

            if ( string.IsNullOrWhiteSpace(request.email) || string.IsNullOrWhiteSpace(request.password) ) {
                return new AuthResponse { isSuccess = false, errorMessage = "Email and password are required." };
            }

            string key = request.email.Trim().ToLower();
            if ( users.TryGetValue(key, out var record) ) {
                if ( record.password == request.password ) {
                    return new AuthResponse {
                        isSuccess = true,
                        token = $"mock_token_{record.profile.id}",
                        profile = CloneProfile(record.profile)
                    };
                }
            }

            return new AuthResponse { isSuccess = false, errorMessage = "Invalid email or password." };
        }

        public async UniTask<AuthResponse> RegisterAsync( RegisterRequest request ) {
            await UniTask.Yield();

            if ( string.IsNullOrWhiteSpace(request.email) || string.IsNullOrWhiteSpace(request.password) ) {
                return new AuthResponse { isSuccess = false, errorMessage = "Email and password are required." };
            }

            string key = request.email.Trim().ToLower();
            if ( users.ContainsKey(key) ) {
                return new AuthResponse { isSuccess = false, errorMessage = "An account with this email already exists." };
            }

            int newId = ++idCounter;
            string prefix = request.email.Split('@')[0];
            string autoDisplayName = string.IsNullOrEmpty(prefix) ? $"Player_{newId}" : prefix;

            var newProfile = new PlayerProfileData {
                id = $"mock_user_{newId}",
                email = request.email.Trim(),
                displayName = autoDisplayName,
                avatarId = 0,
                money = 10000,
                level = 1,
                exp = 0,
                friendIds = new List<string>()
            };

            users[key] = (request.password, newProfile);

            return new AuthResponse {
                isSuccess = true,
                token = $"mock_token_{newProfile.id}",
                profile = CloneProfile(newProfile)
            };
        }

        public async UniTask<PlayerProfileData> GetProfileAsync( string token ) {
            await UniTask.Yield();
            var user = FindUserByToken(token);
            return user != null ? CloneProfile(user) : null;
        }

        public async UniTask<PlayerProfileData> UpdateProfileAsync( string token, UpdateProfileRequest request ) {
            await UniTask.Yield();
            var user = FindUserByToken(token);
            if ( user == null ) return null;

            if ( !string.IsNullOrWhiteSpace(request.displayName) ) {
                user.displayName = request.displayName.Trim();
            }
            user.avatarId = request.avatarId;

            return CloneProfile(user);
        }

        public async UniTask<List<PlayerProfileData>> GetFriendsAsync( string token ) {
            await UniTask.Yield();
            var user = FindUserByToken(token);
            if ( user == null ) return new List<PlayerProfileData>();

            var friends = new List<PlayerProfileData>();
            foreach ( var fid in user.friendIds ) {
                var match = users.Values.FirstOrDefault(u => u.profile.id == fid);
                if ( match.profile != null ) {
                    friends.Add(CloneProfile(match.profile));
                }
            }
            return friends;
        }

        public async UniTask<bool> AddFriendAsync( string token, string friendEmailOrName ) {
            await UniTask.Yield();
            var user = FindUserByToken(token);
            if ( user == null ) return false;

            var match = users.Values.FirstOrDefault(u =>
                u.profile.email.Equals(friendEmailOrName, StringComparison.OrdinalIgnoreCase) ||
                u.profile.displayName.Equals(friendEmailOrName, StringComparison.OrdinalIgnoreCase)
            );

            if ( match.profile != null && match.profile.id != user.id ) {
                if ( !user.friendIds.Contains(match.profile.id) ) {
                    user.friendIds.Add(match.profile.id);
                    return true;
                }
            }

            return false;
        }

        public async UniTask<bool> RecordMatchResultAsync( string token, MatchResultReportRequest request ) {
            await UniTask.Yield();
            var user = FindUserByToken(token);
            if ( user == null ) return false;

            var playerRecord = request.results?.FirstOrDefault(r => r.userId == user.id);
            if ( playerRecord != null ) {
                user.money = Math.Max(0, user.money + playerRecord.moneyEarned);
                user.exp += playerRecord.expEarned;
                user.level = 1 + (user.exp / 100);

                user.matchHistory.Insert(0, new MatchHistoryItem {
                    matchId = request.matchId,
                    playedAt = DateTime.UtcNow.ToString("o"),
                    rank = playerRecord.rank,
                    moneyEarned = playerRecord.moneyEarned,
                    expEarned = playerRecord.expEarned,
                    players = request.results
                });
            }

            return true;
        }

        public async UniTask<PlayerProfileData> GetByIdAsync( string id ) {
            await UniTask.Yield();
            var match = users.Values.FirstOrDefault(u => u.profile.id == id);
            return match.profile != null ? CloneProfile(match.profile) : null;
        }

        public async UniTask<bool> UpdateAsync( string id, PlayerProfileData data ) {
            await UniTask.Yield();
            var match = users.Values.FirstOrDefault(u => u.profile.id == id);
            if ( match.profile != null ) {
                match.profile.displayName = data.displayName;
                match.profile.avatarId = data.avatarId;
                match.profile.money = data.money;
                match.profile.level = data.level;
                match.profile.exp = data.exp;
                return true;
            }
            return false;
        }

        private PlayerProfileData FindUserByToken( string token ) {
            if ( string.IsNullOrEmpty(token) ) return null;
            string userId = token.Replace("mock_token_", "");
            var match = users.Values.FirstOrDefault(u => u.profile.id == userId);
            return match.profile;
        }

        private PlayerProfileData CloneProfile( PlayerProfileData source ) {
            if ( source == null ) return null;
            return new PlayerProfileData {
                id = source.id,
                email = source.email,
                displayName = source.displayName,
                avatarId = source.avatarId,
                money = source.money,
                level = source.level,
                exp = source.exp,
                friendIds = new List<string>(source.friendIds ?? new List<string>()),
                matchHistory = new List<MatchHistoryItem>(source.matchHistory ?? new List<MatchHistoryItem>())
            };
        }
    }
}
