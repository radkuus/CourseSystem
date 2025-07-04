using CourseSystem.Data;
using CourseSystem.Data.Models;
using CourseSystem.Data.Models.Enums;
using CourseSystem.App.Models; // Dodaj to
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CourseSystem.App.Services
{
    public class AuthenticationService
    {
        private readonly CourseSystemDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationService(CourseSystemDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResult { Success = false, ErrorMessage = "Email i hasło są wymagane." };
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return new AuthResult { Success = false, ErrorMessage = "Nieprawidłowy email lub hasło." };
            }

            // Zaloguj użytkownika
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                    new(ClaimTypes.GivenName, user.FirstName),
                    new(ClaimTypes.Surname, user.LastName),
                    new(ClaimTypes.Role, user.Role.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };

                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
            }

            return new AuthResult
            {
                Success = true,
                User = new UserInfo
                {
                    Id = user.Id.ToString(),
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role.ToString()
                }
            };
        }

        public async Task<AuthResult> RegisterAsync(string email, string password, string firstName, string lastName, string role)
        {
            // Walidacja danych wejściowych
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName))
            {
                return new AuthResult { Success = false, ErrorMessage = "Wszystkie pola są wymagane." };
            }

            // Sprawdzenie czy email już istnieje
            var existingUser = await _dbContext.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower());

            if (existingUser)
            {
                return new AuthResult { Success = false, ErrorMessage = "Użytkownik z tym adresem email już istnieje." };
            }

            // Walidacja hasła
            if (password.Length < 6)
            {
                return new AuthResult { Success = false, ErrorMessage = "Hasło musi mieć co najmniej 6 znaków." };
            }

            // Tworzenie nowego użytkownika
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Role = Enum.TryParse<UserRole>(role, true, out var userRole) ? userRole : UserRole.Student,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                return new AuthResult { Success = true };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = "Wystąpił błąd podczas rejestracji." };
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    // Wykonaj request do endpointu wylogowania
                    using var client = new HttpClient();
                    var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

                    // Dodaj ciasteczka do requestu
                    client.DefaultRequestHeaders.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());

                    var response = await client.PostAsync($"{baseUrl}/api/auth/logout", null);

                    return response.IsSuccessStatusCode;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd w LogoutAsync: {ex.Message}");
                return false;
            }
        }

        public AuthCheckResult CheckAuth()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return new AuthCheckResult { IsAuthenticated = false };
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = user.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = user.FindFirst(ClaimTypes.Surname)?.Value;
            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                return new AuthCheckResult { IsAuthenticated = false };
            }

            return new AuthCheckResult
            {
                IsAuthenticated = true,
                User = new UserInfo
                {
                    Id = userId,
                    Email = email,
                    FirstName = firstName ?? "",
                    LastName = lastName ?? "",
                    Role = role ?? "Student"
                }
            };
        }
    }
}