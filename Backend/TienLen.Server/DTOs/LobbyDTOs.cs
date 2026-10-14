using System.Collections.Generic;

namespace TienLen.Server.DTOs {
    public record ChannelQueryResponse(
        int BetStake,
        List<int> Channels,
        int RecommendedChannel
    );

    public record LobbySessionDto(
        string SessionName,
        int BetStake,
        int Channel,
        int PlayerCount,
        int MaxPlayers
    );

    public record RegisterSessionRequest(
        string SessionName,
        int BetStake,
        int Channel,
        int PlayerCount = 1,
        int MaxPlayers = 4
    );

    public record HeartbeatRequest(
        string SessionName,
        int PlayerCount
    );

    public record QuickJoinResponse(
        bool Found,
        string Action, // "Join" or "Host"
        string? SessionName,
        int Channel,
        int BetStake
    );
}
