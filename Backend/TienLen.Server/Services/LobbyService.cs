using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;

namespace TienLen.Server.Services {
    public class LobbyService : ILobbyService {
        private readonly IDataBaseContext databaseContext;
        public const int MaxTablesPerChannel = 30;

        public LobbyService( IDataBaseContext databaseContext ) {
            this.databaseContext = databaseContext;
        }

        public async Task<ChannelQueryResponse> GetChannelsAsync( int betStake ) {
            // Find all active sessions for this bet tier
            var filter = Builders<LobbySessionDocument>.Filter.Eq(s => s.BetStake, betStake);
            var sessions = await databaseContext.LobbySessions.Find(filter).ToListAsync();

            // Group tables by channel
            var channelCounts = sessions
                .GroupBy(s => s.Channel)
                .ToDictionary(g => g.Key, g => g.Count());

            int maxChannel = channelCounts.Keys.Count > 0 ? channelCounts.Keys.Max() : 1;
            int recommendedChannel = 1;

            // Find lowest channel with capacity (< 30 tables)
            for ( int ch = 1; ch <= maxChannel; ch++ ) {
                int count = channelCounts.TryGetValue(ch, out int c) ? c : 0;
                if ( count < MaxTablesPerChannel ) {
                    recommendedChannel = ch;
                    break;
                }
            }

            // If all existing channels are full, auto-shard: create/recommend next channel
            if ( channelCounts.TryGetValue(recommendedChannel, out int recCount) && recCount >= MaxTablesPerChannel ) {
                recommendedChannel = maxChannel + 1;
            }

            // Build channel list [1 ... max(maxChannel, recommendedChannel)]
            int totalChannels = Math.Max(maxChannel, recommendedChannel);
            var channelsList = Enumerable.Range(1, totalChannels).ToList();

            return new ChannelQueryResponse(
                BetStake: betStake,
                Channels: channelsList,
                RecommendedChannel: recommendedChannel
            );
        }

        public async Task<List<LobbySessionDto>> GetSessionsAsync( int betStake, int channel ) {
            var filter = Builders<LobbySessionDocument>.Filter.And(
                Builders<LobbySessionDocument>.Filter.Eq(s => s.BetStake, betStake),
                Builders<LobbySessionDocument>.Filter.Eq(s => s.Channel, channel)
            );

            var sessions = await databaseContext.LobbySessions.Find(filter).ToListAsync();

            return sessions.Select(s => new LobbySessionDto(
                SessionName: s.SessionName,
                BetStake: s.BetStake,
                Channel: s.Channel,
                PlayerCount: s.PlayerCount,
                MaxPlayers: s.MaxPlayers
            )).ToList();
        }

        public async Task<bool> RegisterSessionAsync( string hostUserId, RegisterSessionRequest request ) {
            if ( string.IsNullOrWhiteSpace(request.SessionName) || request.BetStake <= 0 ) {
                return false;
            }

            var filter = Builders<LobbySessionDocument>.Filter.Eq(s => s.SessionName, request.SessionName);
            var update = Builders<LobbySessionDocument>.Update
                .Set(s => s.SessionName, request.SessionName)
                .Set(s => s.BetStake, request.BetStake)
                .Set(s => s.Channel, Math.Max(1, request.Channel))
                .Set(s => s.PlayerCount, Math.Max(1, request.PlayerCount))
                .Set(s => s.MaxPlayers, request.MaxPlayers > 0 ? request.MaxPlayers : 4)
                .Set(s => s.HostUserId, hostUserId ?? string.Empty)
                .Set(s => s.ExpireAt, DateTime.UtcNow.AddSeconds(30))
                .SetOnInsert(s => s.CreatedAt, DateTime.UtcNow);

            var options = new UpdateOptions { IsUpsert = true };
            var result = await databaseContext.LobbySessions.UpdateOneAsync(filter, update, options);

            return result.IsAcknowledged;
        }

        public async Task<bool> HeartbeatAsync( string sessionName, int playerCount ) {
            if ( string.IsNullOrWhiteSpace(sessionName) ) return false;

            var filter = Builders<LobbySessionDocument>.Filter.Eq(s => s.SessionName, sessionName);
            var update = Builders<LobbySessionDocument>.Update
                .Set(s => s.ExpireAt, DateTime.UtcNow.AddSeconds(30))
                .Set(s => s.PlayerCount, playerCount);

            var result = await databaseContext.LobbySessions.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> UnregisterSessionAsync( string sessionName ) {
            if ( string.IsNullOrWhiteSpace(sessionName) ) return false;

            var filter = Builders<LobbySessionDocument>.Filter.Eq(s => s.SessionName, sessionName);
            var result = await databaseContext.LobbySessions.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }

        public async Task<QuickJoinResponse> QuickJoinAsync( int betStake ) {
            // Find open tables with room for players (< 4 players)
            var filter = Builders<LobbySessionDocument>.Filter.And(
                Builders<LobbySessionDocument>.Filter.Eq(s => s.BetStake, betStake),
                Builders<LobbySessionDocument>.Filter.Where(s => s.PlayerCount < s.MaxPlayers)
            );

            // Prefer fullest room first (e.g. 3/4 players) to fill matches quickly
            var candidate = await databaseContext.LobbySessions
                .Find(filter)
                .SortByDescending(s => s.PlayerCount)
                .FirstOrDefaultAsync();

            if ( candidate != null ) {
                return new QuickJoinResponse(
                    Found: true,
                    Action: "Join",
                    SessionName: candidate.SessionName,
                    Channel: candidate.Channel,
                    BetStake: candidate.BetStake
                );
            }

            // No open room found: auto-allocate an optimal channel for hosting
            var channels = await GetChannelsAsync(betStake);
            return new QuickJoinResponse(
                Found: false,
                Action: "Host",
                SessionName: null,
                Channel: channels.RecommendedChannel,
                BetStake: betStake
            );
        }
    }
}
