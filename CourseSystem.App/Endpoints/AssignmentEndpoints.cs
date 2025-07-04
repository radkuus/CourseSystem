using CourseSystem.Data;
using CourseSystem.Data.Models;
using CourseSystem.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CourseSystem.App.Endpoints
{
    public static class AssignmentEndpoints
    {
        // Endpoint do dodawania nowego zadania
        public static async Task<IResult> CreateAssignmentEndpoint(
            Guid courseId,
            [FromBody] CreateAssignmentDto dto,
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

                // Pobierz ID zalogowanego użytkownika
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
                }

                // Sprawdź rolę użytkownika
                var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != UserRole.Teacher.ToString())
                {
                    return Results.Json(new { message = "Tylko nauczyciele mogą tworzyć zadania" }, statusCode: 403);
                }

                // Sprawdź czy kurs istnieje i czy użytkownik jest jego właścicielem
                var course = await dbContext.Courses
                    .FirstOrDefaultAsync(c => c.Id == courseId);

                if (course == null)
                {
                    return Results.Json(new { message = "Kurs nie został znaleziony" }, statusCode: 404);
                }

                if (course.OwnerId != userGuid)
                {
                    return Results.Json(new { message = "Tylko właściciel kursu może dodawać zadania" }, statusCode: 403);
                }

                // Stwórz nowe zadanie
                var assignment = new Assignment
                {
                    Id = Guid.NewGuid(),
                    CourseId = courseId,
                    Title = dto.Title,
                    Description = dto.Description,
                    Deadline = dto.Deadline,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.Assignments.Add(assignment);
                await dbContext.SaveChangesAsync();

                // Powiadom studentów o nowym zadaniu
                var enrolledStudents = await dbContext.Enrollments
                    .Where(e => e.CourseId == courseId && e.Status == EnrollmentStatus.Enrolled)
                    .Select(e => e.StudentId)
                    .ToListAsync();

                foreach (var studentId in enrolledStudents)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientId = studentId,
                        AssignmentId = assignment.Id,
                        CourseId = courseId,
                        Content = $"Nowe zadanie '{assignment.Title}' zostało dodane do kursu '{course.Name}'",
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.Notifications.Add(notification);
                }

                await dbContext.SaveChangesAsync();

                return Results.Ok(new
                {
                    id = assignment.Id,
                    message = "Zadanie zostało utworzone pomyślnie"
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas tworzenia zadania: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        // Endpoint do edycji zadania
        public static async Task<IResult> UpdateAssignmentEndpoint(
            Guid assignmentId,
            [FromBody] UpdateAssignmentDto dto,
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

                // Pobierz ID zalogowanego użytkownika
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
                }

                // Sprawdź rolę użytkownika
                var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != UserRole.Teacher.ToString())
                {
                    return Results.Json(new { message = "Tylko nauczyciele mogą edytować zadania" }, statusCode: 403);
                }

                // Pobierz zadanie wraz z kursem
                var assignment = await dbContext.Assignments
                    .Include(a => a.Course)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId);

                if (assignment == null)
                {
                    return Results.Json(new { message = "Zadanie nie zostało znalezione" }, statusCode: 404);
                }

                // Sprawdź czy użytkownik jest właścicielem kursu
                if (assignment.Course.OwnerId != userGuid)
                {
                    return Results.Json(new { message = "Tylko właściciel kursu może edytować zadania" }, statusCode: 403);
                }

                // Zaktualizuj zadanie
                assignment.Title = dto.Title;
                assignment.Description = dto.Description;
                assignment.Deadline = dto.Deadline;

                await dbContext.SaveChangesAsync();

                return Results.Ok(new { message = "Zadanie zostało zaktualizowane pomyślnie" });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas aktualizacji zadania: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        // Endpoint do usuwania zadania
        public static async Task<IResult> DeleteAssignmentEndpoint(
            Guid assignmentId,
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

                // Pobierz ID zalogowanego użytkownika
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
                }

                // Sprawdź rolę użytkownika
                var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != UserRole.Teacher.ToString())
                {
                    return Results.Json(new { message = "Tylko nauczyciele mogą usuwać zadania" }, statusCode: 403);
                }

                // Pobierz zadanie wraz z kursem
                var assignment = await dbContext.Assignments
                    .Include(a => a.Course)
                    .Include(a => a.Submissions)
                    .Include(a => a.Notifications)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId);

                if (assignment == null)
                {
                    return Results.Json(new { message = "Zadanie nie zostało znalezione" }, statusCode: 404);
                }

                // Sprawdź czy użytkownik jest właścicielem kursu
                if (assignment.Course.OwnerId != userGuid)
                {
                    return Results.Json(new { message = "Tylko właściciel kursu może usuwać zadania" }, statusCode: 403);
                }

                // Usuń powiązane zgłoszenia i notyfikacje
                dbContext.Submissions.RemoveRange(assignment.Submissions);
                dbContext.Notifications.RemoveRange(assignment.Notifications);

                // Usuń zadanie
                dbContext.Assignments.Remove(assignment);
                await dbContext.SaveChangesAsync();

                return Results.Ok(new { message = "Zadanie zostało usunięte pomyślnie" });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas usuwania zadania: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        // Endpoint do wyświetlania wszystkich zadań z kursu
        public static async Task<IResult> GetCourseAssignmentsEndpoint(
            Guid courseId,
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

                // Pobierz ID zalogowanego użytkownika
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    return Results.Json(new { message = "Nieprawidłowy identyfikator użytkownika" }, statusCode: 400);
                }

                var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

                // Sprawdź czy kurs istnieje
                var course = await dbContext.Courses
                    .FirstOrDefaultAsync(c => c.Id == courseId);

                if (course == null)
                {
                    return Results.Json(new { message = "Kurs nie został znaleziony" }, statusCode: 404);
                }

                // Sprawdź dostęp do kursu
                bool hasAccess = false;

                // Nauczyciel-właściciel ma dostęp
                if (userRole == UserRole.Teacher.ToString() && course.OwnerId == userGuid)
                {
                    hasAccess = true;
                }
                // Student zapisany na kurs ma dostęp
                else if (userRole == UserRole.Student.ToString())
                {
                    var enrollment = await dbContext.Enrollments
                        .FirstOrDefaultAsync(e => e.CourseId == courseId &&
                                                  e.StudentId == userGuid &&
                                                  e.Status == EnrollmentStatus.Enrolled);
                    if (enrollment != null)
                    {
                        hasAccess = true;
                    }
                }

                if (!hasAccess)
                {
                    return Results.Json(new { message = "Brak dostępu do tego kursu" }, statusCode: 403);
                }

                // Pobierz zadania
                var assignments = await dbContext.Assignments
                    .Where(a => a.CourseId == courseId)
                    .OrderBy(a => a.Deadline)
                    .Select(a => new
                    {
                        a.Id,
                        a.Title,
                        a.Description,
                        a.Deadline,
                        a.CreatedAt,
                        SubmissionsCount = a.Submissions.Count(),
                        // Dla studenta - czy złożył zadanie
                        HasSubmitted = userRole == UserRole.Student.ToString()
                            ? a.Submissions.Any(s => s.StudentId == userGuid)
                            : (bool?)null
                    })
                    .ToListAsync();

                return Results.Ok(assignments);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas pobierania zadań: {ex.Message}",
                    statusCode: 500
                );
            }
        }
    }

    // DTO dla tworzenia/edycji zadania
    public record CreateAssignmentDto(
        string Title,
        string Description,
        DateTime Deadline
    );

    public record UpdateAssignmentDto(
        string Title,
        string Description,
        DateTime Deadline
    );
}