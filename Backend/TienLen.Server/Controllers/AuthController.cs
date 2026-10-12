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
    }
}
