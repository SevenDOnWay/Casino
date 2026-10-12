using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;

namespace TienLen.Server.Services {
    public class AuthService : IAuthService {
        private readonly IMongoDbContext db;
        private readonly IConfiguration configuration;
        private readonly IUserService userService;

        public AuthService( IMongoDbContext db, IConfiguration configuration, IUserService userService ) {
            this.db = db;
            this.configuration = configuration;
            this.userService = userService;
        }

        public async Task<AuthResponseDto> RegisterAsync( RegisterRequestDto request ) {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();

            // Verify unique email
            var existingCursor = await db.Users.FindAsync(u => u.Email == normalizedEmail);
            var existing = await existingCursor.FirstOrDefaultAsync();
            if ( existing != null ) {
                return new AuthResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "An account with this email already exists."
                };
            }

            // Secure password hash
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Default display name from email prefix
            string prefix = normalizedEmail.Split('@')[0];
            string defaultDisplayName = string.IsNullOrWhiteSpace(prefix) ? "Player" : prefix;

            var newUser = new UserDocument {
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                DisplayName = defaultDisplayName,
                AvatarId = 0,
                Money = 10000,
                Level = 1,
                Exp = 0,
                FriendIds = new(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await db.Users.InsertOneAsync(newUser);

            string token = GenerateJwtToken(newUser.Id, newUser.Email);
            var profile = await userService.GetProfileAsync(newUser.Id);

            return new AuthResponseDto {
                IsSuccess = true,
                Token = token,
                Profile = profile
            };
        }

        public async Task<AuthResponseDto> LoginAsync( LoginRequestDto request ) {
            string normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var cursor = await db.Users.FindAsync(u => u.Email == normalizedEmail);
            var user = await cursor.FirstOrDefaultAsync();
            if ( user == null ) {
                return new AuthResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid email or password."
                };
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if ( !isPasswordValid ) {
                return new AuthResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid email or password."
                };
            }

            string token = GenerateJwtToken(user.Id, user.Email);
            var profile = await userService.GetProfileAsync(user.Id);

            return new AuthResponseDto {
                IsSuccess = true,
                Token = token,
                Profile = profile
            };
        }

        public string GenerateJwtToken( string userId, string email ) {
            var jwtKey = configuration["Jwt:Key"] ?? "TienLenSecretSuperSecureKey2026!@#$%^&*()";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"] ?? "TienLenServer",
                audience: configuration["Jwt:Audience"] ?? "TienLenClient",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
