using CourseSystem.Data;
using CourseSystem.Data.Models;
using CourseSystem.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CourseSystem.App.Endpoints
{
    public static class EnrollmentEndpoints
    {
        // Student zapisuje się na kurs
        public static async Task<IResult> EnrollInCourseEndpoint(
            [FromBody] EnrollRequest request,
            CourseSystemDbContext db,
            HttpContext httpContext)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != UserRole.Student.ToString())
            {
                return Results.Forbid();
            }

            // Sprawdź czy kurs istnieje i jest aktywny
            var course = await db.Courses
                .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.Status == CourseStatus.Active);

            if (course == null)
            {
                return Results.NotFound(new { message = "Course not found or inactive" });
            }

            // Sprawdź czy student nie jest już zapisany
            var existingEnrollment = await db.Enrollments
                .AnyAsync(e => e.CourseId == request.CourseId && e.StudentId == userId);

            if (existingEnrollment)
            {
                return Results.BadRequest(new { message = "Already enrolled or pending enrollment exists" });
            }

            // Utwórz nowy wpis enrollment
            var enrollment = new Enrollment
            {
                Id = Guid.NewGuid(),
                CourseId = request.CourseId,
                StudentId = userId,
                Status = EnrollmentStatus.Pending,
                EnrolledAt = DateTime.UtcNow
            };

            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Enrollment request submitted",
                enrollmentId = enrollment.Id
            });
        }

        // Nauczyciel akceptuje studenta
        public static async Task<IResult> ApproveEnrollmentEndpoint(
            Guid enrollmentId,
            CourseSystemDbContext db,
            HttpContext httpContext)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != UserRole.Teacher.ToString())
            {
                return Results.Forbid();
            }

            // Pobierz enrollment z kursem
            var enrollment = await db.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null)
            {
                return Results.NotFound(new { message = "Enrollment not found" });
            }

            // Sprawdź czy nauczyciel jest właścicielem kursu
            if (enrollment.Course.OwnerId != userId)
            {
                return Results.Forbid();
            }

            // Sprawdź czy enrollment jest w statusie Pending
            if (enrollment.Status != EnrollmentStatus.Pending)
            {
                return Results.BadRequest(new { message = "Enrollment is not pending" });
            }

            // Zmień status na Enrolled
            enrollment.Status = EnrollmentStatus.Enrolled;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Student approved successfully" });
        }

        // Nauczyciel odrzuca studenta
        public static async Task<IResult> RejectEnrollmentEndpoint(
            Guid enrollmentId,
            CourseSystemDbContext db,
            HttpContext httpContext)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != UserRole.Teacher.ToString())
            {
                return Results.Forbid();
            }

            // Pobierz enrollment z kursem
            var enrollment = await db.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null)
            {
                return Results.NotFound(new { message = "Enrollment not found" });
            }

            // Sprawdź czy nauczyciel jest właścicielem kursu
            if (enrollment.Course.OwnerId != userId)
            {
                return Results.Forbid();
            }

            // Sprawdź czy enrollment jest w statusie Pending
            if (enrollment.Status != EnrollmentStatus.Pending)
            {
                return Results.BadRequest(new { message = "Only pending enrollments can be rejected" });
            }

            // Usuń enrollment
            db.Enrollments.Remove(enrollment);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Enrollment rejected successfully" });
        }

        // DTO dla żądania zapisu na kurs
        public record EnrollRequest(Guid CourseId);

        // Endpoint do pobierania wszystkich zapisów dla kursu
        public static async Task<IResult> GetEnrollmentsForCourseEndpoint(
            Guid courseId,
            CourseSystemDbContext db,
            HttpContext httpContext)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != UserRole.Teacher.ToString())
            {
                return Results.Forbid();
            }

            // Sprawdź czy kurs istnieje i czy użytkownik jest jego właścicielem
            var course = await db.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == courseId && c.OwnerId == userId);

            if (course == null)
            {
                return Results.NotFound(new { message = "Course not found or you don't have permission" });
            }

            // Pobierz wszystkie zapisy z danymi studentów
            var enrollments = await db.Enrollments
                .Where(e => e.CourseId == courseId)
                .Include(e => e.Student)
                .AsNoTracking()
                .Select(e => new EnrollmentDto
                {
                    Id = e.Id,
                    Student = new StudentDto
                    {
                        Id = e.Student.Id,
                        FirstName = e.Student.FirstName,
                        LastName = e.Student.LastName,
                        Email = e.Student.Email
                    },
                    Status = e.Status,
                    EnrolledAt = e.EnrolledAt
                })
                .ToListAsync();

            return Results.Ok(enrollments);
        }
    }

    // Klasy DTO
    public class EnrollmentDto
    {
        public Guid Id { get; set; }
        public StudentDto Student { get; set; } = null!;
        public EnrollmentStatus Status { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

    public class StudentDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}