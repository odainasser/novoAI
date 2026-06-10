using System.Text.RegularExpressions;
using Application.Features.Apps;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// CRUD for the Apps integration module. Registering an app here is the WHOLE
/// onboarding step on the ByteAI side — the app's tool catalog is discovered live
/// from its BaseUrl, so no code change or redeploy is ever needed per app.
/// </summary>
internal class AppsAdminService : IAppsAdminService
{
    private static readonly Regex CodePattern = new("^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppsAdminService> _logger;

    public AppsAdminService(ApplicationDbContext context, ILogger<AppsAdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppDto>> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _context.Apps.AsNoTracking()
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        return apps.Select(Map).ToList();
    }

    public async Task<AppDto?> GetAppAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var app = await _context.Apps.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return app is null ? null : Map(app);
    }

    public async Task<AppDto> CreateAppAsync(SaveAppRequest request, CancellationToken cancellationToken = default)
    {
        var (code, name, baseUrl) = Validate(request);

        if (await _context.Apps.AnyAsync(a => a.Code == code, cancellationToken))
            throw new ArgumentException($"An app with code '{code}' already exists.");

        var app = new App
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = Blank(request.Description),
            BaseUrl = baseUrl,
            PersonaPrompt = Blank(request.PersonaPrompt),
            Currency = Blank(request.Currency) ?? "AED",
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        _context.Apps.Add(app);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered app '{Code}' -> {BaseUrl}.", app.Code, app.BaseUrl);
        return Map(app);
    }

    public async Task UpdateAppAsync(Guid id, SaveAppRequest request, CancellationToken cancellationToken = default)
    {
        var (code, name, baseUrl) = Validate(request);

        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"App {id} not found.");

        if (await _context.Apps.AnyAsync(a => a.Code == code && a.Id != id, cancellationToken))
            throw new ArgumentException($"An app with code '{code}' already exists.");

        app.Code = code;
        app.Name = name;
        app.Description = Blank(request.Description);
        app.BaseUrl = baseUrl;
        app.PersonaPrompt = Blank(request.PersonaPrompt);
        app.Currency = Blank(request.Currency) ?? "AED";
        app.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"App {id} not found.");
        app.IsActive = isActive;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static (string Code, string Name, string BaseUrl) Validate(SaveAppRequest request)
    {
        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!CodePattern.IsMatch(code))
            throw new ArgumentException("Code must be 3-50 chars of lowercase letters, digits, and hyphens.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100)
            throw new ArgumentException("Name must be 2-100 characters.");

        var baseUrl = request.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("BaseUrl must be an absolute http(s) URL.");

        return (code, name, baseUrl);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static AppDto Map(App a) => new()
    {
        Id = a.Id,
        Code = a.Code,
        Name = a.Name,
        Description = a.Description,
        BaseUrl = a.BaseUrl,
        PersonaPrompt = a.PersonaPrompt,
        Currency = a.Currency,
        IsActive = a.IsActive,
        CreatedAt = a.CreatedAt
    };
}
