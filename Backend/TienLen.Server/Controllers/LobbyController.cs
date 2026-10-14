using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using TienLen.Server.DTOs;
using TienLen.Server.Services;

namespace TienLen.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class LobbyController : ControllerBase {
        private readonly ILobbyService lobbyService;

        public LobbyController( ILobbyService lobbyService ) {
            this.lobbyService = lobbyService;
        }

        /// <summary>
        /// Retrieves the list of available channels for a given bet stake and the recommended channel.
        /// </summary>
        [HttpGet("channels")]
        public async Task<IActionResult> GetChannels( [FromQuery] int betStake ) {
            if ( betStake <= 0 ) {
                return BadRequest(ApiResponse<ChannelQueryResponse>.Fail("Invalid bet stake."));
            }

            var result = await lobbyService.GetChannelsAsync(betStake);
            return Ok(ApiResponse<ChannelQueryResponse>.Success(result));
        }

        /// <summary>
        /// Retrieves active tables in a specific bet shard and channel shard.
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions( [FromQuery] int betStake, [FromQuery] int channel = 1 ) {
            if ( betStake <= 0 || channel <= 0 ) {
                return BadRequest(ApiResponse<object>.Fail("Invalid bet stake or channel."));
            }

            var sessions = await lobbyService.GetSessionsAsync(betStake, channel);
            return Ok(ApiResponse<object>.Success(sessions));
        }

        /// <summary>
        /// Registers a newly created Photon Fusion table into the lobby directory.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterSession( [FromBody] RegisterSessionRequest request ) {
            if ( request == null || string.IsNullOrWhiteSpace(request.SessionName) || request.BetStake <= 0 ) {
                return BadRequest(ApiResponse<bool>.Fail("Invalid session registration payload."));
            }

            string hostUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                             ?? User.FindFirstValue("sub") 
                             ?? "guest_host";

            bool success = await lobbyService.RegisterSessionAsync(hostUserId, request);
            if ( !success ) {
                return StatusCode(500, ApiResponse<bool>.Fail("Failed to register session."));
            }

            return Ok(ApiResponse<bool>.Success(true, "Session registered successfully."));
        }

        /// <summary>
        /// Extends the session lease by 30 seconds and updates live player count.
        /// </summary>
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat( [FromBody] HeartbeatRequest request ) {
            if ( request == null || string.IsNullOrWhiteSpace(request.SessionName) ) {
                return BadRequest(ApiResponse<bool>.Fail("Invalid heartbeat payload."));
            }

            bool acknowledged = await lobbyService.HeartbeatAsync(request.SessionName, request.PlayerCount);
            if ( !acknowledged ) {
                return NotFound(ApiResponse<bool>.Fail("Session not found or expired."));
            }

            return Ok(ApiResponse<bool>.Success(true, "Heartbeat acknowledged."));
        }

        /// <summary>
        /// Unregisters a session when a table closes normally.
        /// </summary>
        [HttpDelete("sessions/{sessionName}")]
        public async Task<IActionResult> UnregisterSession( [FromRoute] string sessionName ) {
            if ( string.IsNullOrWhiteSpace(sessionName) ) {
                return BadRequest(ApiResponse<bool>.Fail("Session name cannot be empty."));
            }

            bool deleted = await lobbyService.UnregisterSessionAsync(sessionName);
            return Ok(ApiResponse<bool>.Success(deleted, deleted ? "Session removed." : "Session not found."));
        }

        /// <summary>
        /// Matchmaking Quick Join: finds the best available table or returns optimal channel to host.
        /// </summary>
        [HttpGet("quick-join")]
        public async Task<IActionResult> QuickJoin( [FromQuery] int betStake ) {
            if ( betStake <= 0 ) {
                return BadRequest(ApiResponse<QuickJoinResponse>.Fail("Invalid bet stake."));
            }

            var decision = await lobbyService.QuickJoinAsync(betStake);
            return Ok(ApiResponse<QuickJoinResponse>.Success(decision));
        }
    }
}
