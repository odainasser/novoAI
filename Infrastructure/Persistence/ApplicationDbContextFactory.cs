using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build an ApplicationDbContext at design time without
/// touching the API's Host pipeline (which requires JwtSettings etc.) and without
/// the ambiguous-constructor error from the runtime DI registration.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Look up the connection string from the Api project's appsettings.json
        // when running from the Infrastructure folder.
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Api");
        if (!Directory.Exists(basePath))
            basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connection = config.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ByteMartDB;Trusted_Connection=true;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new ApplicationDbContext(options);
    }
}
