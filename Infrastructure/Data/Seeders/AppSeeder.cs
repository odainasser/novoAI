using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

/// <summary>
/// Seeds the registered client applications (the Apps integration module) with
/// their required integration data. Create-if-missing only — an app row already
/// present (including admin edits via the Apps page) is never overwritten.
/// </summary>
public class AppSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppSeeder> _logger;

    public AppSeeder(ApplicationDbContext context, ILogger<AppSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var apps = new[]
        {
            new App
            {
                Id = Guid.NewGuid(),
                Code = "bytemart",
                Name = "ByteMart",
                Description = "Multi-branch retail management platform (POS, inventory, procurement)",
                // ByteMart's API exposes /api/assistant-data (tools, execute, branch-context).
                BaseUrl = "https://localhost:7050",
                PersonaPrompt = "retail business assistant",
                // Shares ByteAI's JWT signing configuration — its user tokens validate natively.
                JwtAuthority = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new App
            {
                Id = Guid.NewGuid(),
                Code = "novologs",
                Name = "Novologs",
                Description = "Enterprise projects, tasks, CRM, finance, and collaboration platform",
                // The Novologs MCP module exposes /api/assistant-data. Local dev URL;
                // when ByteAI runs inside the Novologs docker network use http://mcp:8080.
                BaseUrl = "http://localhost:5010",
                PersonaPrompt = "workspace assistant for projects, tasks, clients, and finance",
                // Tokens are issued by the Novologs tenant service (OIDC discovery + JWKS).
                // Docker-network issuer; for host-run Novologs set the tenant module's local URL.
                JwtAuthority = "http://tenant:8080",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var app in apps)
        {
            var exists = await _context.Apps.IgnoreQueryFilters().AnyAsync(a => a.Code == app.Code);
            if (exists)
                continue;

            _context.Apps.Add(app);
            _logger.LogInformation("Seeded app '{Code}' -> {BaseUrl}", app.Code, app.BaseUrl);
        }

        await _context.SaveChangesAsync();
    }
}
