using CourseSystem.App.Components;
using CourseSystem.App.Endpoints;
using CourseSystem.App.Services;
using CourseSystem.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Konfiguracja serwisów
builder.Services.AddDbContext<CourseSystemDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication & Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
        options.Cookie.Name = "CourseSystemAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization();

// Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "CourseSystemSession";
});

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add AuthenticationService
builder.Services.AddScoped<AuthenticationService>();

var app = builder.Build();

// automatyczne stosowanie migracji przy starcie
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CourseSystemDbContext>();
    try
    {
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();

// middleware w odpowiedniej kolejności
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Add("Pragma", "no-cache");
    context.Response.Headers.Add("Expires", "0");
    await next();
});

app.UseAntiforgery();

// Auth API endpoints
app.MapPost("/api/auth/login", AuthEndpoints.LoginEndpoint);
app.MapPost("/api/auth/register", AuthEndpoints.RegisterEndpoint);
app.MapPost("/api/auth/logout", AuthEndpoints.LogoutEndpoint);
app.MapGet("/api/auth/check", AuthEndpoints.CheckAuthEndpoint);

// Course API endpoints
app.MapPost("/api/courses", CourseEndpoints.CreateCourseEndpoint).RequireAuthorization();
app.MapPut("/api/courses/{id}", CourseEndpoints.UpdateCourseEndpoint).RequireAuthorization();
app.MapDelete("/api/courses/{id}", CourseEndpoints.DeleteCourseEndpoint).RequireAuthorization();
app.MapGet("/api/courses", CourseEndpoints.GetCoursesEndpoint).RequireAuthorization();
app.MapGet("/api/courses/{id}", CourseEndpoints.GetCourseByIdEndpoint).RequireAuthorization();

// Assignment API endpoints
app.MapPost("/api/courses/{courseId}/assignments", AssignmentEndpoints.CreateAssignmentEndpoint).RequireAuthorization();
app.MapPut("/api/assignments/{assignmentId}", AssignmentEndpoints.UpdateAssignmentEndpoint).RequireAuthorization();
app.MapDelete("/api/assignments/{assignmentId}", AssignmentEndpoints.DeleteAssignmentEndpoint).RequireAuthorization();
app.MapGet("/api/courses/{courseId}/assignments", AssignmentEndpoints.GetCourseAssignmentsEndpoint).RequireAuthorization();

// Enrollment API endpoints
app.MapPost("/api/enrollments", EnrollmentEndpoints.EnrollInCourseEndpoint).RequireAuthorization();
app.MapPut("/api/enrollments/{enrollmentId}/approve", EnrollmentEndpoints.ApproveEnrollmentEndpoint).RequireAuthorization();
app.MapDelete("/api/enrollments/{enrollmentId}/reject", EnrollmentEndpoints.RejectEnrollmentEndpoint).RequireAuthorization();
app.MapGet("/api/enrollments/course/{courseId}", EnrollmentEndpoints.GetEnrollmentsForCourseEndpoint).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();