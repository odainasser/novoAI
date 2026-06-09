using Application;
using Api.Endpoints;
using Api.Middleware;
using Api.Authorization;
using Infrastructure;
using Infrastructure.Data.Seeders;
using Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging via Serilog (console sink; configurable from appsettings).
builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Health checks — exposes /health (with a DB connectivity probe) for
// orchestrators and load balancers.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

// =========================
// Services
// =========================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Add Swagger services
builder.Services.AddSwaggerGen();

// Add CORS
var corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient",
        policyBuilder =>
        {
            policyBuilder.WithOrigins(corsOrigins)
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
});

// Rate limiting — brute-force protection on auth, abuse protection on the
// (LLM-backed) assistant. Returns 429 when a partition's window is exhausted.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Per-IP window for authentication flows (login/refresh/reset/…).
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Per-user (fallback per-IP) window for assistant requests.
    options.AddPolicy("assistant", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Add custom authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// =========================
// App
// =========================

var app = builder.Build();

// =========================
// Database Migrations + Seeding
// =========================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        Console.WriteLine("[Startup] Beginning database migration and seeding...");

        // Apply any pending migrations to ensure database schema is up to date
        var db = services.GetRequiredService<ApplicationDbContext>();
        
        Console.WriteLine("[Startup] Applying migrations...");
        try
        {
            await db.Database.MigrateAsync();
            Console.WriteLine("[Startup] Migrations applied successfully.");
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("pending changes", StringComparison.OrdinalIgnoreCase) == true || ex.Message?.Contains("PendingModelChangesWarning", StringComparison.OrdinalIgnoreCase) == true)
        {
            // EF Core detected model changes that are not captured in migrations.
            // Log a clear message and continue startup so the application remains runnable during development.
            var progLogger = services.GetRequiredService<ILogger<Program>>();
            progLogger.LogWarning(ex, "Pending model changes detected. Skipping automatic migration. Add a new migration to update the database schema.");
            Console.WriteLine("[Startup] Pending model changes detected. Skipping automatic migration. Create and apply a new migration to update the database.");
        }

        // ALWAYS run seeders - they handle idempotency internally
        Console.WriteLine("[Startup] Starting DatabaseSeeder...");
        var logger = services.GetRequiredService<ILogger<DatabaseSeeder>>();
        var seeder = new DatabaseSeeder(services, logger);
        await seeder.SeedAsync();
        Console.WriteLine("[Startup] DatabaseSeeder completed successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database");
        Console.WriteLine($"[Startup][ERROR] {ex.Message}");
        Console.WriteLine($"[Startup][ERROR] Stack: {ex.StackTrace}");
        throw;
    }
}

// =========================
// Middleware
// =========================

app.UseExceptionHandler();

// Concise structured request logs (method, path, status, elapsed).
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Retail API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Retail API Documentation";
});

app.UseHttpsRedirection();

// Serve static files from wwwroot (uploads, assets, etc.) so media URLs like /uploads/... are reachable
app.UseStaticFiles();

app.UseCors("AllowBlazorClient");

app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<PermissionMiddleware>();

app.UseAuthorization();

// =========================
// Endpoints
// =========================

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapPermissionEndpoints();
app.MapUserLogEndpoints();
app.MapMediaEndpoints();
app.MapLookupEndpoints();
app.MapDashboardEndpoints();
app.MapNotificationEndpoints();
app.MapAssistantEndpoints();
app.MapAssistantAdminEndpoints();

// Liveness/readiness probe (DB connectivity included).
app.MapHealthChecks("/health");

// SignalR hub for real-time notifications
app.MapHub<NotificationHub>("/hubs/notifications");

// =========================
// Run
// =========================

app.Run();
