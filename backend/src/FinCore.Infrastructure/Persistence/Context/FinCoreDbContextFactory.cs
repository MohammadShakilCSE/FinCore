using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinCore.Infrastructure.Persistence.Context;

// Allows migrations to run with Infrastructure as both target and startup project.
public sealed class FinCoreDbContextFactory : IDesignTimeDbContextFactory<FinCoreDbContext>
{
    public FinCoreDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(FindApiDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var connectionString = configuration.GetConnectionString("FinCore");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:FinCore is required in FinCore.Api/appsettings.json.");

        return new FinCoreDbContext(new DbContextOptionsBuilder<FinCoreDbContext>()
            .UseNpgsql(connectionString).Options);
    }

    private static string FindApiDirectory()
    {
        // Support running dotnet ef from the repository, backend, or project directories.
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
             directory is not null; directory = directory.Parent)
        {
            string[] candidates =
            [
                directory.FullName,
                Path.Combine(directory.FullName, "FinCore.Api"),
                Path.Combine(directory.FullName, "src", "FinCore.Api"),
                Path.Combine(directory.FullName, "backend", "src", "FinCore.Api")
            ];
            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "FinCore.Api.csproj")) &&
                    File.Exists(Path.Combine(candidate, "appsettings.json")))
                    return candidate;
            }
        }

        throw new DirectoryNotFoundException("Cannot locate FinCore.Api/appsettings.json. Run dotnet ef from the FinCore repository.");
    }
}
