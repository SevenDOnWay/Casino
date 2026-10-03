using System.Collections.Generic;
using System.Threading.Tasks;
using TienLen.Server.DTOs;

namespace TienLen.Server.Services {
    public interface IUserService {
        Task<UserProfileDto?> GetProfileAsync( string userId );
        Task<UserProfileDto?> UpdateProfileAsync( string userId, UpdateProfileRequestDto request );
        Task<List<UserProfileDto>> GetFriendsAsync( string userId );
        Task<bool> AddFriendAsync( string userId, string friendEmailOrName );
    }
}
