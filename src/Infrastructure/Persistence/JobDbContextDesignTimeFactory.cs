using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobProcessor.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by EF Core tools (dotnet ef migrations add/update).
/// Uses a local development connection string when no environment variable is present.
/// </summary>
public class JobDbContextDesignTimeFactory : IDesignTimeDbContextFactory<JobDbContext>
{
    public JobDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=jobsdb;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new JobDbContext(options);
    }
}
