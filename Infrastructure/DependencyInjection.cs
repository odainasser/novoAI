using System.Text;
using Application.Common.Interfaces;
using Application.Services;
using Domain.Repositories;
using Infrastructure.Configuration;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
                .UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlServerOptions =>
                {
                    sqlServerOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlServerOptions.CommandTimeout(60);
                }));

        // Persist data protection keys so tokens (password reset, email confirmation) survive app restarts
        var keysPath = Path.Combine(AppContext.BaseDirectory, "DataProtection-Keys");
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("RetailApp");

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // JWT Configuration
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings configuration is missing");

        // Fail fast on weak / placeholder signing keys. Secrets should be supplied
        // out-of-band (environment variable JwtSettings__Secret, user-secrets, or a
        // vault) — never the committed default outside local development.
        const string placeholderSecret = "super_secret_key_12345_make_it_long_enough";
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
            throw new InvalidOperationException(
                "JwtSettings:Secret must be configured and at least 32 characters. " +
                "Provide it via the JwtSettings__Secret environment variable or a secret store.");

        if (!isDevelopment && jwtSettings.Secret == placeholderSecret)
            throw new InvalidOperationException(
                "The default development JWT secret must not be used outside Development. " +
                "Set JwtSettings__Secret via an environment variable or secret store.");

        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();

                    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authorization.Substring("Bearer ".Length).Trim();

                        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            token = token.Substring("Bearer ".Length).Trim();
                        }

                        context.Token = token;
                    }

                    // SignalR WebSocket clients pass the token via the access_token query
                    // string because custom headers are not available on the upgrade request.
                    if (string.IsNullOrEmpty(context.Token))
                    {
                        var accessToken = context.Request.Query["access_token"].ToString();
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                    }

                    return Task.CompletedTask;
                }
            };
        });

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserLogRepository, UserLogRepository>();

        // Register infrastructure services
        services.AddHttpContextAccessor();
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddScoped<IAppConfiguration, AppConfiguration>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IUserLogService, UserLogService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<ITerminalService, TerminalService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Register infrastructure CurrentUserService (uses IHttpContextAccessor only)
        services.AddScoped<Application.Common.Interfaces.ICurrentUserService, CurrentUserService>();

        // Ollama AI Assistant
        services.Configure<Configuration.OllamaSettings>(configuration.GetSection("OllamaSettings"));
        var ollamaSettings = configuration.GetSection("OllamaSettings").Get<Configuration.OllamaSettings>() ?? new();
        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri(ollamaSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(ollamaSettings.TimeoutSeconds);
        });
        services.AddScoped<OllamaClient>();
        // Tool-calling assistant: the model picks read tools (function-calling) and
        // phrases answers; the application owns every query, permission gate, branch
        // lock, and PII redaction. The tool catalog is a code-owned singleton.
        services.AddSingleton<Services.Assistant.ToolCatalog>();
        services.AddScoped<Services.Assistant.AssistantPlanEngine>();
        services.AddScoped<Application.Features.Assistant.IAssistantLearningService, Services.Assistant.AssistantLearningService>();
        services.AddScoped<IAssistantService, AssistantService>();
        // Admin: the assistant turn log + human-in-the-loop tool-candidate queue.
        services.AddScoped<IAssistantAdminService, AssistantAdminService>();

        // SignalR for real-time notifications
        services.AddSignalR();

        return services;
    }
}