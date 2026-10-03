using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using TienLen.Server.DTOs;
using TienLen.Server.Services;

namespace TienLen.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase {
        private readonly IUserService userService;

        public UserController( IUserService userService ) {
            this.userService = userService;
        }

        private string? CurrentUserId =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            User.FindFirst("sub")?.Value;

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile() {
            var userId = CurrentUserId;
            if ( string.IsNullOrEmpty(userId) ) {
                return Unauthorized(ApiResponse<UserProfileDto>.Fail("User unauthorized"));
            }

            var profile = await userService.GetProfileAsync(userId);
            if ( profile == null ) {
                return NotFound(ApiResponse<UserProfileDto>.Fail("User profile not found."));
            }

            return Ok(profile);
        }

        [HttpPost("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile( [FromBody] UpdateProfileRequestDto request ) {
            var userId = CurrentUserId;
            if ( string.IsNullOrEmpty(userId) ) {
                return Unauthorized(ApiResponse<UserProfileDto>.Fail("User unauthorized"));
            }

            var updated = await userService.UpdateProfileAsync(userId, request);
            if ( updated == null ) {
                return BadRequest(ApiResponse<UserProfileDto>.Fail("Unable to update profile."));
            }

            return Ok(updated);
        }

        [HttpGet("friends")]
        [Authorize]
        public async Task<IActionResult> GetFriends() {
            var userId = CurrentUserId;
            if ( string.IsNullOrEmpty(userId) ) {
                return Unauthorized();
            }

            var friends = await userService.GetFriendsAsync(userId);
            return Ok(friends);
        }

        [HttpPost("friends")]
        [Authorize]
        public async Task<IActionResult> AddFriend( [FromBody] AddFriendRequestDto request ) {
            var userId = CurrentUserId;
            if ( string.IsNullOrEmpty(userId) ) {
                return Unauthorized();
            }

            if ( string.IsNullOrWhiteSpace(request.FriendEmailOrName) ) {
                return BadRequest(ApiResponse<bool>.Fail("Friend username or email is required."));
            }

            bool success = await userService.AddFriendAsync(userId, request.FriendEmailOrName);
            if ( !success ) {
                return BadRequest(ApiResponse<bool>.Fail("Friend not found or already added."));
            }

            return Ok(ApiResponse<bool>.Success(true, "Friend added successfully."));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById( string id ) {
            var profile = await userService.GetProfileAsync(id);
            if ( profile == null ) return NotFound();
            return Ok(profile);
        }
    }
}
