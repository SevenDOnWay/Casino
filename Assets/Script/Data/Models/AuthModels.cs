using System;
using System.Collections.Generic;

namespace Assets.Script.Data.Models {
    [Serializable]
    public abstract class AuthRequest {
        public string email;
        public string password;
    }

    [Serializable]
    public class LoginRequest : AuthRequest { }

    [Serializable]
    public class RegisterRequest : AuthRequest { }

    [Serializable]
    public class UpdateProfileRequest {
        public string displayName;
        public int avatarId;
    }

    [Serializable]
    public class AddFriendRequest {
        public string friendEmailOrName;
    }

    [Serializable]
    public class AuthResponse {
        public bool isSuccess;
        public string token;
        public string errorMessage;
        public PlayerProfileData profile;
    }

    [Serializable]
    public class ApiResponse<T> {
        public bool isSuccess;
        public string message;
        public T data;
    }
}
