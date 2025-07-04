using CourseSystem.Data;
using CourseSystem.Data.Models;
using CourseSystem.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CourseSystem.App.Endpoints
{
    public static class SubmissionEndpoints
    {
        // Endpoint do tworzenia submission przez studenta (wraz z uploadem pliku)
        public static async Task<IResult> CreateSubmissionEndpoint(
            Guid assignmentId,
            IFormFile file,
            HttpContext httpContext,
            CourseSystemDbContext dbContext,
            IWebHostEnvironment environment)
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
                if (userRole != UserRole.Student.ToString())
                {
                    return Results.Json(new { message = "Tylko studenci mogą przesyłać zadania" }, statusCode: 403);
                }

                // Sprawdź czy plik został przesłany
                if (file == null || file.Length == 0)
                {
                    return Results.Json(new { message = "Plik jest wymagany" }, statusCode: 400);
                }

                // Pobierz zadanie wraz z kursem i właścicielem
                var assignment = await dbContext.Assignments
                    .Include(a => a.Course)
                        .ThenInclude(c => c.Owner)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId);

                if (assignment == null)
                {
                    return Results.Json(new { message = "Zadanie nie zostało znalezione" }, statusCode: 404);
                }

                // Sprawdź czy student jest zapisany na kurs
                var enrollment = await dbContext.Enrollments
                    .FirstOrDefaultAsync(e => e.CourseId == assignment.CourseId &&
                                             e.StudentId == userGuid &&
                                             e.Status == EnrollmentStatus.Enrolled);

                if (enrollment == null)
                {
                    return Results.Json(new { message = "Nie jesteś zapisany na ten kurs" }, statusCode: 403);
                }

                // Sprawdź czy student nie złożył już tego zadania
                var existingSubmission = await dbContext.Submissions
                    .AnyAsync(s => s.AssignmentId == assignmentId && s.StudentId == userGuid);

                if (existingSubmission)
                {
                    return Results.Json(new { message = "Już złożyłeś to zadanie" }, statusCode: 400);
                }

                // Sprawdź deadline
                if (DateTime.UtcNow > assignment.Deadline)
                {
                    return Results.Json(new { message = "Termin składania zadania minął" }, statusCode: 400);
                }

                // Pobierz dane studenta
                var student = await dbContext.Users.FindAsync(userGuid);
                if (student == null)
                {
                    return Results.Json(new { message = "Błąd pobierania danych studenta" }, statusCode: 500);
                }

                // Utwórz strukturę folderów
                var basePath = "/app/data/courses"; // Ścieżka do woluminu

                // Format: Nazwisko_NazwaKursu_RokAkademicki
                var courseFolderName = $"{assignment.Course.Owner.LastName}_{assignment.Course.Name.Replace(" ", "_")}_{assignment.Course.AcademicYear}_{assignment.Course.AcademicYear + 1}";

                // Format: Nazwisko_Imie_ID (używamy ID zamiast numeru albumu, którego nie ma w modelu)
                var studentFolderName = $"{student.LastName}_{student.FirstName}_{student.Id.ToString().Substring(0, 8)}";

                // Format: Zadanie_X (numerujemy zadania w ramach kursu)
                var assignmentNumber = await dbContext.Assignments
                    .Where(a => a.CourseId == assignment.CourseId && a.CreatedAt <= assignment.CreatedAt)
                    .CountAsync();
                var assignmentFolderName = $"Zadanie_{assignmentNumber}";

                // Pełna ścieżka
                var fullPath = Path.Combine(basePath, courseFolderName, studentFolderName, assignmentFolderName);

                // Utwórz katalogi jeśli nie istnieją
                Directory.CreateDirectory(fullPath);

                // Zapisz plik
                var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{file.FileName}";
                var filePath = Path.Combine(fullPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Utwórz wpis submission w bazie
                var submission = new Submission
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = assignmentId,
                    StudentId = userGuid,
                    FilePath = filePath.Replace("/app/data/courses/", ""), // Zapisz względną ścieżkę
                    SubmittedAt = DateTime.UtcNow
                };

                dbContext.Submissions.Add(submission);
                await dbContext.SaveChangesAsync();

                return Results.Ok(new
                {
                    id = submission.Id,
                    message = "Zadanie zostało przesłane pomyślnie",
                    submittedAt = submission.SubmittedAt
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas przesyłania zadania: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        // Endpoint do pobierania wszystkich submissions dla nauczyciela
        public static async Task<IResult> GetTeacherSubmissionsEndpoint(
            HttpContext httpContext,
            CourseSystemDbContext dbContext,
            [FromQuery] Guid? courseId = null,
            [FromQuery] Guid? assignmentId = null)
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
                    return Results.Json(new { message = "Tylko nauczyciele mogą przeglądać zgłoszenia" }, statusCode: 403);
                }

                // Podstawowe zapytanie - pobierz submissions z kursów nauczyciela
                var query = dbContext.Submissions
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Course)
                    .Include(s => s.Student)
                    .Where(s => s.Assignment.Course.OwnerId == userGuid);

                // Filtrowanie po kursie
                if (courseId.HasValue)
                {
                    query = query.Where(s => s.Assignment.CourseId == courseId.Value);
                }

                // Filtrowanie po zadaniu
                if (assignmentId.HasValue)
                {
                    query = query.Where(s => s.AssignmentId == assignmentId.Value);
                }

                var submissions = await query
                    .OrderByDescending(s => s.SubmittedAt)
                    .Select(s => new SubmissionDto
                    {
                        Id = s.Id,
                        Student = new StudentInfoDto
                        {
                            Id = s.Student.Id,
                            FirstName = s.Student.FirstName,
                            LastName = s.Student.LastName,
                            Email = s.Student.Email
                        },
                        Assignment = new AssignmentInfoDto
                        {
                            Id = s.Assignment.Id,
                            Title = s.Assignment.Title,
                            Deadline = s.Assignment.Deadline
                        },
                        Course = new CourseInfoDto
                        {
                            Id = s.Assignment.Course.Id,
                            Name = s.Assignment.Course.Name,
                            AcademicYear = s.Assignment.Course.AcademicYear
                        },
                        FilePath = s.FilePath,
                        SubmittedAt = s.SubmittedAt,
                        Grade = s.Grade,
                        IsLate = s.SubmittedAt > s.Assignment.Deadline
                    })
                    .ToListAsync();

                return Results.Ok(new
                {
                    submissions = submissions,
                    count = submissions.Count
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas pobierania zgłoszeń: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        // Endpoint do pobierania pliku submission
        public static async Task<IResult> DownloadSubmissionFileEndpoint(
            Guid submissionId,
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

                var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

                // Pobierz submission
                var submission = await dbContext.Submissions
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Course)
                    .FirstOrDefaultAsync(s => s.Id == submissionId);

                if (submission == null)
                {
                    return Results.Json(new { message = "Zgłoszenie nie zostało znalezione" }, statusCode: 404);
                }

                // Sprawdź uprawnienia
                bool hasAccess = false;

                // Student może pobrać swoje własne zgłoszenie
                if (userRole == UserRole.Student.ToString() && submission.StudentId == userGuid)
                {
                    hasAccess = true;
                }
                // Nauczyciel może pobrać zgłoszenia ze swoich kursów
                else if (userRole == UserRole.Teacher.ToString() && submission.Assignment.Course.OwnerId == userGuid)
                {
                    hasAccess = true;
                }

                if (!hasAccess)
                {
                    return Results.Json(new { message = "Brak dostępu do tego pliku" }, statusCode: 403);
                }

                // Pełna ścieżka do pliku
                var fullPath = Path.Combine("/app/data/courses", submission.FilePath);

                if (!File.Exists(fullPath))
                {
                    return Results.Json(new { message = "Plik nie został znaleziony" }, statusCode: 404);
                }

                var fileBytes = await File.ReadAllBytesAsync(fullPath);
                var fileName = Path.GetFileName(fullPath);

                return Results.File(fileBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas pobierania pliku: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        // Endpoint do oceniania submission przez nauczyciela
        public static async Task<IResult> GradeSubmissionEndpoint(
            Guid submissionId,
            [FromBody] GradeSubmissionDto dto,
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

                var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != UserRole.Teacher.ToString())
                {
                    return Results.Json(new { message = "Tylko nauczyciele mogą oceniać zadania" }, statusCode: 403);
                }

                // Pobierz submission
                var submission = await dbContext.Submissions
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Course)
                    .FirstOrDefaultAsync(s => s.Id == submissionId);

                if (submission == null)
                {
                    return Results.Json(new { message = "Zgłoszenie nie zostało znalezione" }, statusCode: 404);
                }

                // Sprawdź czy nauczyciel jest właścicielem kursu
                if (submission.Assignment.Course.OwnerId != userGuid)
                {
                    return Results.Json(new { message = "Możesz oceniać tylko zadania ze swoich kursów" }, statusCode: 403);
                }

                // Walidacja oceny
                if (dto.Grade < 2.0f || dto.Grade > 5.0f || dto.Grade % 0.5f != 0)
                {
                    return Results.Json(new { message = "Ocena musi być w zakresie 2.0-5.0 z krokiem 0.5" }, statusCode: 400);
                }

                // Zapisz ocenę
                submission.Grade = dto.Grade;
                await dbContext.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "Ocena została zapisana pomyślnie",
                    grade = submission.Grade
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Błąd podczas oceniania zadania: {ex.Message}",
                    statusCode: 500
                );
            }
        }
    }

    // DTOs
    public class SubmissionDto
    {
        public Guid Id { get; set; }
        public StudentInfoDto Student { get; set; } = null!;
        public AssignmentInfoDto Assignment { get; set; } = null!;
        public CourseInfoDto Course { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public float? Grade { get; set; }
        public bool IsLate { get; set; }
    }

    public class StudentInfoDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class AssignmentInfoDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Deadline { get; set; }
    }

    public class CourseInfoDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int AcademicYear { get; set; }
    }

    public class GradeSubmissionDto
    {
        public float Grade { get; set; }
    }
}