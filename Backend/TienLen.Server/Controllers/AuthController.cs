using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TienLen.Server.DTOs;
using TienLen.Server.Services;

namespace TienLen.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase {
        private readonly IAuthService authService;

        public AuthController( IAuthService authService ) {
            this.authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register( [FromBody] RegisterRequestDto request ) {
            if ( !ModelState.IsValid ) {
                return BadRequest(new AuthResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid email or password format."
                });
            }

            var result = await authService.RegisterAsync(request);
            if ( !result.IsSuccess ) {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login( [FromBody] LoginRequestDto request ) {
            if ( !ModelState.IsValid ) {
                return BadRequest(new AuthResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid login credentials."
                });
            }

            var result = await authService.LoginAsync(request);
            if ( !result.IsSuccess ) {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh( [FromBody] RefreshTokenRequestDto request ) {
            if ( !ModelState.IsValid ) {
                return BadRequest(new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid refresh token request."
                });
            }

            var result = await authService.RefreshTokenAsync(request);
            if ( result == null || !result.IsSuccess ) {
                return Unauthorized(result ?? new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid, expired, or revoked refresh token."
                });
            }

            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout( [FromBody] LogoutRequestDto request ) {
            if ( !ModelState.IsValid ) {
                return BadRequest(new ApiResponse<bool> {
                    IsSuccess = false,
                    Message = "Invalid logout request."
                });
            }

            bool revoked = await authService.RevokeRefreshTokenAsync(request);
            return Ok(ApiResponse<bool>.Success(revoked, "Logged out successfully."));
        }
    }
}
