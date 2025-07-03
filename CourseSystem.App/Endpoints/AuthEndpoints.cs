using CourseSystem.Data;
using CourseSystem.Data.Models;
using CourseSystem.Data.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CourseSystem.App.Endpoints;

public static class AuthEndpoints
{
    public static async Task<IResult> LoginEndpoint(
        [FromBody] LoginRequest request,
        CourseSystemDbContext dbContext,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Email i hasło są wymagane." });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user == null || !VerifyPassword(request.Password, user.Password))
        {
            return Results.Json(
                new { message = "Nieprawidłowy email lub hasło." },
                statusCode: 401);
        }

        // Fix for IDE0090: Simplify 'new' expression
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
            IsPersistent = request.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return Results.Ok(new
        {
            message = "Zalogowano pomyślnie.",
            user = new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                role = user.Role
            }
        });
    }

    public static async Task<IResult> RegisterEndpoint(
        [FromBody] RegisterRequest request,
        CourseSystemDbContext dbContext)
    {
        // Walidacja danych wejściowych
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            return Results.BadRequest(new { message = "Wszystkie pola są wymagane." });
        }

        // Sprawdzenie czy email już istnieje
        var existingUser = await dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (existingUser)
        {
            return Results.Conflict(new { message = "Użytkownik z tym adresem email już istnieje." });
        }

        // Walidacja hasła
        if (request.Password.Length < 6)
        {
            return Results.BadRequest(new { message = "Hasło musi mieć co najmniej 6 znaków." });
        }

        // Walidacja emaila
        if (!IsValidEmail(request.Email))
        {
            return Results.BadRequest(new { message = "Nieprawidłowy format adresu email." });
        }

        // Tworzenie nowego użytkownika
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            Password = HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = Enum.TryParse<UserRole>(request.Role, true, out var role)
            ? role
            : UserRole.Student, 
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Rejestracja zakończona pomyślnie.",
                userId = user.Id
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: "Wystąpił błąd podczas rejestracji.",
                statusCode: 500);
        }
    }

    public static async Task<IResult> LogoutEndpoint(HttpContext httpContext)
    {
        try
        {
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return Results.Ok(new
                {
                    message = "Wylogowano pomyślnie.",
                    success = true
                });
            }

            return Results.Ok(new
            {
                message = "Użytkownik nie był zalogowany.",
                success = false
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas wylogowywania: {ex.Message}",
                statusCode: 500
            );
        }
    }

    public static IResult CheckAuthEndpoint(HttpContext httpContext)
    {
        try
        {
            var user = httpContext.User;

            // Uproszczona logika sprawdzania
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return Results.Json(new
                {
                    isAuthenticated = false,
                    message = "Użytkownik niezalogowany"
                }, statusCode: 401);
            }

            // Pobierz claims z null-checking
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = user.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = user.FindFirst(ClaimTypes.Surname)?.Value;
            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            // Sprawdź czy mamy wymagane dane
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                return Results.Json(new
                {
                    isAuthenticated = false,
                    message = "Brak wymaganych danych użytkownika"
                }, statusCode: 401);
            }

            return Results.Ok(new
            {
                isAuthenticated = true,
                user = new
                {
                    id = userId,
                    email = email,
                    firstName = firstName ?? "",
                    lastName = lastName ?? "",
                    role = role ?? "Student"
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas sprawdzania autoryzacji: {ex.Message}",
                statusCode: 500
            );
        }
    }

    // Metody pomocnicze do hashowania i weryfikacji hasła
    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }

    private static bool IsValidEmail(string email)
    {
        return new EmailAddressAttribute().IsValid(email);
    }
}

// DTOs dla żądań
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Role { get; set; }
}

