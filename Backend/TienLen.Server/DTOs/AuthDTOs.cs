using System.ComponentModel.DataAnnotations;

namespace TienLen.Server.DTOs {
    public class LoginRequestDto {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequestDto {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto {
        public bool IsSuccess { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public UserProfileDto? Profile { get; set; }
    }

    public class ApiResponse<T> {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Success( T data, string message = "" ) =>
            new() { IsSuccess = true, Message = message, Data = data };

        public static ApiResponse<T> Fail( string message ) =>
            new() { IsSuccess = false, Message = message, Data = default };
    }
}
