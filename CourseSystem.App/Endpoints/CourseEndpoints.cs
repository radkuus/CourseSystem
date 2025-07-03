using CourseSystem.Data;
using CourseSystem.Data.Models;
using CourseSystem.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CourseSystem.App.Endpoints;

public static class CourseEndpoints
{
    // Stałe dla ról - zamiast modyfikować enum
    private static class Roles
    {
        public const string Student = "Student";
        public const string Teacher = "Teacher";
        public const string Admin = "Admin"; // Dodatkowa rola nieobecna w enumie
    }

    // Helper do sprawdzania czy użytkownik ma rolę Admin
    private static bool IsAdmin(ClaimsPrincipal user)
    {
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
        return userRole == Roles.Admin;
    }

    // Helper do sprawdzania czy rola istnieje w enumie UserRole
    private static bool IsValidEnumRole(string role)
    {
        return Enum.TryParse<UserRole>(role, out _);
    }

    // Endpoint do tworzenia kursu - tylko dla Teacher
    public static async Task<IResult> CreateCourseEndpoint(
        [FromBody] CreateCourseRequest request,
        HttpContext httpContext,
        CourseSystemDbContext dbContext)
    {
        try
        {
            var user = httpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                return Results.Json(new { message = "Użytkownik niezalogowany" }, statusCode: 401);
            }

            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != Roles.Teacher)
            {
                return Results.Json(new { message = "Tylko nauczyciele mogą tworzyć kursy" }, statusCode: 403);
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
            }

            // Walidacja danych
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.Json(new { message = "Nazwa kursu jest wymagana" }, statusCode: 400);
            }

            if (request.AcademicYear < 2020 || request.AcademicYear > 2030)
            {
                return Results.Json(new { message = "Nieprawidłowy rok akademicki" }, statusCode: 400);
            }

            var course = new Course
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                OwnerId = userGuid,
                AcademicYear = request.AcademicYear,
                Status = CourseStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Courses.Add(course);
            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Kurs został utworzony pomyślnie",
                courseId = course.Id,
                course = new
                {
                    id = course.Id,
                    name = course.Name,
                    description = course.Description,
                    academicYear = course.AcademicYear,
                    status = course.Status.ToString(),
                    createdAt = course.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas tworzenia kursu: {ex.Message}",
                statusCode: 500
            );
        }
    }

    // Endpoint do edytowania kursu - tylko dla właściciela
    public static async Task<IResult> UpdateCourseEndpoint(
        Guid id,
        [FromBody] UpdateCourseRequest request,
        HttpContext httpContext,
        CourseSystemDbContext dbContext)
    {
        try
        {
            var user = httpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                return Results.Json(new { message = "Użytkownik niezalogowany" }, statusCode: 401);
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
            }

            var course = await dbContext.Courses
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                return Results.Json(new { message = "Kurs nie został znaleziony" }, statusCode: 404);
            }

            // Sprawdzenie czy użytkownik jest właścicielem kursu lub adminem
            if (course.OwnerId != userGuid && !IsAdmin(user))
            {
                return Results.Json(new { message = "Tylko właściciel kursu lub administrator może go edytować" }, statusCode: 403);
            }

            // Aktualizacja danych
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                course.Name = request.Name.Trim();
            }

            if (request.Description != null)
            {
                course.Description = request.Description.Trim();
            }

            if (request.AcademicYear.HasValue)
            {
                if (request.AcademicYear.Value < 2020 || request.AcademicYear.Value > 2030)
                {
                    return Results.Json(new { message = "Nieprawidłowy rok akademicki" }, statusCode: 400);
                }
                course.AcademicYear = request.AcademicYear.Value;
            }

            if (request.Status.HasValue)
            {
                course.Status = (CourseStatus)request.Status.Value;  // Rzutowanie int na enum
            }

            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Kurs został zaktualizowany pomyślnie",
                course = new
                {
                    id = course.Id,
                    name = course.Name,
                    description = course.Description,
                    academicYear = course.AcademicYear,
                    status = course.Status.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas aktualizacji kursu: {ex.Message}",
                statusCode: 500
            );
        }
    }

    // Endpoint do usuwania kursu - tylko dla właściciela
    public static async Task<IResult> DeleteCourseEndpoint(
        Guid id,
        HttpContext httpContext,
        CourseSystemDbContext dbContext)
    {
        try
        {
            var user = httpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                return Results.Json(new { message = "Użytkownik niezalogowany" }, statusCode: 401);
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
            }

            var course = await dbContext.Courses
                .Include(c => c.Assignments)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                return Results.Json(new { message = "Kurs nie został znaleziony" }, statusCode: 404);
            }

            // Sprawdzenie czy użytkownik jest właścicielem kursu lub adminem
            if (course.OwnerId != userGuid && !IsAdmin(user))
            {
                return Results.Json(new { message = "Tylko właściciel kursu lub administrator może go usunąć" }, statusCode: 403);
            }

            // Sprawdzenie czy kurs ma przypisane zadania lub zapisanych studentów
            if (course.Assignments.Any() || course.Enrollments.Any())
            {
                return Results.Json(new
                {
                    message = "Nie można usunąć kursu, który ma przypisane zadania lub zapisanych studentów"
                }, statusCode: 400);
            }

            dbContext.Courses.Remove(course);
            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Kurs został usunięty pomyślnie"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas usuwania kursu: {ex.Message}",
                statusCode: 500
            );
        }
    }

    // Endpoint do pobierania listy kursów
    public static async Task<IResult> GetCoursesEndpoint(
        HttpContext httpContext,
        CourseSystemDbContext dbContext,
        [FromQuery] int? academicYear = null,
        [FromQuery] CourseStatus? status = null)
    {
        try
        {
            var user = httpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                return Results.Json(new { message = "Użytkownik niezalogowany" }, statusCode: 401);
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
            }

            var query = dbContext.Courses
                .Include(c => c.Owner)
                .AsQueryable();

            // Filtrowanie na podstawie roli
            switch (userRole)
            {
                case Roles.Student:
                    // Student widzi tylko kursy, na które jest zapisany
                    query = query.Where(c => c.Enrollments.Any(e => e.StudentId == userGuid));
                    break;
                case Roles.Teacher:
                    // Nauczyciel widzi swoje kursy
                    query = query.Where(c => c.OwnerId == userGuid);
                    break;
                case Roles.Admin:
                    // Admin widzi wszystkie kursy - nie filtrujemy
                    break;
                default:
                    // Nieznana rola - brak dostępu
                    return Results.Json(new { message = "Nieznana rola użytkownika" }, statusCode: 403);
            }

            // Dodatkowe filtry
            if (academicYear.HasValue)
            {
                query = query.Where(c => c.AcademicYear == academicYear.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            var courses = await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    description = c.Description,
                    academicYear = c.AcademicYear,
                    status = c.Status.ToString(),
                    owner = new
                    {
                        id = c.Owner.Id,
                        name = $"{c.Owner.FirstName} {c.Owner.LastName}",
                        email = c.Owner.Email
                    },
                    enrollmentCount = c.Enrollments.Count(),
                    assignmentCount = c.Assignments.Count(),
                    createdAt = c.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                courses = courses,
                count = courses.Count
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas pobierania kursów: {ex.Message}",
                statusCode: 500
            );
        }
    }

    // Endpoint do pobierania szczegółów kursu
    public static async Task<IResult> GetCourseByIdEndpoint(
        Guid id,
        HttpContext httpContext,
        CourseSystemDbContext dbContext)
    {
        try
        {
            var user = httpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                return Results.Json(new { message = "Użytkownik niezalogowany" }, statusCode: 401);
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
            }

            var course = await dbContext.Courses
                .Include(c => c.Owner)
                .Include(c => c.Assignments)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                return Results.Json(new { message = "Kurs nie został znaleziony" }, statusCode: 404);
            }

            // Sprawdzenie uprawnień dostępu
            bool hasAccess = IsAdmin(user) ||
                           course.OwnerId == userGuid ||
                           course.Enrollments.Any(e => e.StudentId == userGuid);

            if (!hasAccess)
            {
                return Results.Json(new { message = "Brak dostępu do tego kursu" }, statusCode: 403);
            }

            var courseDetails = new
            {
                id = course.Id,
                name = course.Name,
                description = course.Description,
                academicYear = course.AcademicYear,
                status = course.Status.ToString(),
                owner = new
                {
                    id = course.Owner.Id,
                    name = $"{course.Owner.FirstName} {course.Owner.LastName}",
                    email = course.Owner.Email
                },
                assignments = course.Assignments.Select(a => new
                {
                    id = a.Id,
                    title = a.Title,
                    description = a.Description,
                    deadline = a.Deadline,
                    createdAt = a.CreatedAt
                }),
                enrollments = course.Enrollments.Select(e => new
                {
                    id = e.Id,
                    student = new
                    {
                        id = e.Student.Id,
                        name = $"{e.Student.FirstName} {e.Student.LastName}",
                        email = e.Student.Email
                    },
                    status = e.Status.ToString(),
                    enrolledAt = e.EnrolledAt
                }),
                createdAt = course.CreatedAt
            };

            return Results.Ok(courseDetails);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Błąd podczas pobierania szczegółów kursu: {ex.Message}",
                statusCode: 500
            );
        }
    }
}

// DTO dla żądań
public class CreateCourseRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int AcademicYear { get; set; }
}

public class UpdateCourseRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? AcademicYear { get; set; }
    public int?  Status { get; set; }
}