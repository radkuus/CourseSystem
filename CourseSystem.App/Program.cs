using CourseSystem.App.Components;
using CourseSystem.App.Endpoints;
using CourseSystem.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;


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

// Add HttpClient for Blazor components
builder.Services.AddHttpClient();

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
        // Log błąd migracji
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// middleware w odpowiedniej kolejności
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();


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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
