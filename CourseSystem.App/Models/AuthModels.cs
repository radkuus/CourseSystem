namespace CourseSystem.App.Models
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Message { get; set; } = string.Empty;
        public UserInfo User { get; set; } = new();
    }

    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    public class AuthCheckResult
    {
        public bool IsAuthenticated { get; set; }
        public UserInfo? User { get; set; }
    }
}