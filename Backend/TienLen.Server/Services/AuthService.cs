using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TienLen.Server.Data;
using TienLen.Server.DTOs;
using TienLen.Server.Models;

namespace TienLen.Server.Services {
    public class AuthService : IAuthService {
        private readonly IDataBaseContext db;
        private readonly IConfiguration configuration;
        private readonly IUserService userService;

        private static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan DefaultRefreshTokenLifetime = TimeSpan.FromDays(30);

        public AuthService( IDataBaseContext db, IConfiguration configuration, IUserService userService ) {
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

            var accessLifetime = GetAccessTokenLifetime();
            string token = GenerateJwtToken(newUser.Id, newUser.Email, accessLifetime);
            var (refreshToken, _) = await CreateRefreshSessionAsync(newUser.Id);
            var profile = await userService.GetProfileAsync(newUser.Id);

            return new AuthResponseDto {
                IsSuccess = true,
                AccessToken = token,
                RefreshToken = refreshToken,
                AccessTokenExpiresAtUtc = DateTime.UtcNow.Add(accessLifetime),
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

            var accessLifetime = GetAccessTokenLifetime();
            string token = GenerateJwtToken(user.Id, user.Email, accessLifetime);
            var (refreshToken, _) = await CreateRefreshSessionAsync(user.Id);
            var profile = await userService.GetProfileAsync(user.Id);

            return new AuthResponseDto {
                IsSuccess = true,
                AccessToken = token,
                RefreshToken = refreshToken,
                AccessTokenExpiresAtUtc = DateTime.UtcNow.Add(accessLifetime),
                Profile = profile
            };
        }

        public async Task<TokenRefreshResponseDto?> RefreshTokenAsync( RefreshTokenRequestDto request ) {
            if ( request == null || string.IsNullOrWhiteSpace(request.RefreshToken) ) {
                return new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Refresh token is required."
                };
            }

            string rawToken = request.RefreshToken.Trim();
            string tokenHash = HashToken(rawToken);

            if ( db.RefreshSessions == null ) {
                return new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Refresh session service is not configured."
                };
            }

            // Find session by current token hash or consumed token hash
            var sessionCursor = await db.RefreshSessions.FindAsync(
                s => s.CurrentTokenHash == tokenHash || s.ConsumedTokenHashes.Contains(tokenHash)
            );
            var session = await sessionCursor.FirstOrDefaultAsync();

            if ( session == null ) {
                return new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Invalid refresh token."
                };
            }

            if ( session.RevokedAtUtc != null ) {
                return new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Session has been revoked."
                };
            }

            // Replay attack check: token was already consumed
            if ( session.ConsumedTokenHashes != null && session.ConsumedTokenHashes.Contains(tokenHash) ) {
                // Check 15s grace window for concurrent requests/retries
                bool withinGraceWindow = session.LastUsedAtUtc.HasValue &&
                    (DateTime.UtcNow - session.LastUsedAtUtc.Value).TotalSeconds <= 15;

                if ( !withinGraceWindow ) {
                    // Revoke session completely upon replay attack detection
                    var revokeUpdate = Builders<RefreshSessionDocument>.Update
                        .Set(s => s.RevokedAtUtc, DateTime.UtcNow);
                    await db.RefreshSessions.UpdateOneAsync(s => s.Id == session.Id, revokeUpdate);

                    return new TokenRefreshResponseDto {
                        IsSuccess = false,
                        ErrorMessage = "Refresh token reuse detected. Session has been revoked."
                    };
                }
            }

            if ( session.ExpiresAtUtc <= DateTime.UtcNow ) {
                return new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "Refresh token has expired."
                };
            }

            // Fetch user for claims
            var userCursor = await db.Users.FindAsync(u => u.Id == session.UserId);
            var user = await userCursor.FirstOrDefaultAsync();
            if ( user == null ) {
                return new TokenRefreshResponseDto {
                    IsSuccess = false,
                    ErrorMessage = "User associated with session was not found."
                };
            }

            // Token Rotation: generate new refresh token and hash
            string newRawToken = GenerateSecureTokenString();
            string newTokenHash = HashToken(newRawToken);

            var accessLifetime = GetAccessTokenLifetime();
            string newAccessToken = GenerateJwtToken(user.Id, user.Email, accessLifetime);
            DateTime accessExpiresAt = DateTime.UtcNow.Add(accessLifetime);

            var update = Builders<RefreshSessionDocument>.Update
                .Push(s => s.ConsumedTokenHashes, tokenHash)
                .Set(s => s.CurrentTokenHash, newTokenHash)
                .Set(s => s.LastUsedAtUtc, DateTime.UtcNow);

            await db.RefreshSessions.UpdateOneAsync(s => s.Id == session.Id, update);

            return new TokenRefreshResponseDto {
                IsSuccess = true,
                AccessToken = newAccessToken,
                RefreshToken = newRawToken,
                ExpiresAt = accessExpiresAt
            };
        }

        public async Task<bool> RevokeRefreshTokenAsync( LogoutRequestDto request ) {
            if ( request == null || string.IsNullOrWhiteSpace(request.RefreshToken) ) {
                return false;
            }

            if ( db.RefreshSessions == null ) return false;

            string rawToken = request.RefreshToken.Trim();
            string tokenHash = HashToken(rawToken);

            var update = Builders<RefreshSessionDocument>.Update.Set(s => s.RevokedAtUtc, DateTime.UtcNow);
            var result = await db.RefreshSessions.UpdateOneAsync(
                s => s.CurrentTokenHash == tokenHash || s.ConsumedTokenHashes.Contains(tokenHash),
                update
            );

            return result.ModifiedCount > 0;
        }

        public string GenerateJwtToken( string userId, string email, TimeSpan? expiry = null ) {
            var jwtKey = configuration["Jwt:Key"] ?? "TienLenSecretSuperSecureKey2026!@#$%^&*()";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var lifetime = expiry ?? GetAccessTokenLifetime();

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"] ?? "TienLenServer",
                audience: configuration["Jwt:Audience"] ?? "TienLenClient",
                claims: claims,
                expires: DateTime.UtcNow.Add(lifetime),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<(string rawRefreshToken, DateTime expiresAt)> CreateRefreshSessionAsync( string userId ) {
            string rawToken = GenerateSecureTokenString();
            string hash = HashToken(rawToken);
            DateTime expiresAt = DateTime.UtcNow.Add(DefaultRefreshTokenLifetime);

            var session = new RefreshSessionDocument {
                SessionId = Guid.NewGuid().ToString("N"),
                UserId = userId,
                CurrentTokenHash = hash,
                ConsumedTokenHashes = new List<string>(),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAt,
                LastUsedAtUtc = DateTime.UtcNow
            };

            if ( db.RefreshSessions != null ) {
                await db.RefreshSessions.InsertOneAsync(session);
            }

            return (rawToken, expiresAt);
        }

        private TimeSpan GetAccessTokenLifetime() {
            if ( double.TryParse(configuration["Jwt:AccessTokenLifetimeMinutes"], out double mins) && mins > 0 ) {
                return TimeSpan.FromMinutes(mins);
            }
            return DefaultAccessTokenLifetime;
        }

        public static string GenerateSecureTokenString() {
            byte[] bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static string HashToken( string token ) {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }
    }
}
