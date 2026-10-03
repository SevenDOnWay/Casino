using System.Threading.Tasks;
using TienLen.Server.DTOs;

namespace TienLen.Server.Services {
    public interface IAuthService {
        Task<AuthResponseDto> RegisterAsync( RegisterRequestDto request );
        Task<AuthResponseDto> LoginAsync( LoginRequestDto request );
        string GenerateJwtToken( string userId, string email );
    }
}
