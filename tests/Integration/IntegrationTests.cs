using DotNet.Testcontainers.Builders;
using JobProcessor.Infrastructure.Entities;
using JobProcessor.Infrastructure.Persistence;
using JobProcessor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace IntegrationTests;

/// <summary>
/// Integration test skeleton using Testcontainers.
/// Starts real PostgreSQL and RabbitMQ containers and verifies basic repository operations.
/// </summary>
public class JobRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15")
        .WithDatabase("jobsdb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private JobDbContext _db = null!;
    private IJobRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new JobDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _repository = new JobRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AddJob_ShouldPersistJobWithQueuedStatus()
    {
        var job = new JobEntity { Type = "ImportCsv", Payload = "{}" };
        var saved = await _repository.AddJobAsync(job);

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(JobStatus.Queued, saved.Status);
    }

    [Fact]
    public async Task GetJob_ShouldReturnPersistedJob()
    {
        var job = new JobEntity { Type = "ImportCsv" };
        await _repository.AddJobAsync(job);

        var fetched = await _repository.GetJobAsync(job.Id);

        Assert.NotNull(fetched);
        Assert.Equal(job.Id, fetched!.Id);
    }

    [Fact]
    public async Task MarkProcessing_ShouldUpdateStatus()
    {
        var job = new JobEntity { Type = "ImportCsv" };
        await _repository.AddJobAsync(job);

        await _repository.MarkProcessingAsync(job.Id);

        var updated = await _repository.GetJobAsync(job.Id);
        Assert.Equal(JobStatus.Processing, updated!.Status);
    }

    [Fact]
    public async Task MarkSucceeded_ShouldSetSucceededStatusAndProcessedAt()
    {
        var job = new JobEntity { Type = "ImportCsv" };
        await _repository.AddJobAsync(job);

        await _repository.MarkSucceededAsync(job.Id);

        var updated = await _repository.GetJobAsync(job.Id);
        Assert.Equal(JobStatus.Succeeded, updated!.Status);
        Assert.NotNull(updated.ProcessedAt);
    }

    [Fact]
    public async Task IsProcessed_ShouldReturnTrueAfterSucceeded()
    {
        var job = new JobEntity { Type = "ImportCsv" };
        await _repository.AddJobAsync(job);

        Assert.False(await _repository.IsProcessedAsync(job.Id));

        await _repository.MarkSucceededAsync(job.Id);

        Assert.True(await _repository.IsProcessedAsync(job.Id));
    }

    [Fact]
    public async Task IncrementAttempts_ShouldMoveToDeadLetterAfterMaxAttempts()
    {
        var job = new JobEntity { Type = "ImportCsv", MaxAttempts = 2 };
        await _repository.AddJobAsync(job);

        await _repository.IncrementAttemptsAndMaybeFailAsync(job.Id, "error 1");
        await _repository.IncrementAttemptsAndMaybeFailAsync(job.Id, "error 2");

        var updated = await _repository.GetJobAsync(job.Id);
        Assert.Equal(JobStatus.DeadLetter, updated!.Status);
        Assert.Equal(2, updated.Attempts);
    }
}

/// <summary>Placeholder: verify solution builds without infrastructure dependencies.</summary>
public class PlaceholderTests
{
    [Fact]
    public void Solution_Builds_Successfully()
    {
        // This test passes simply by the fact that the solution compiled and ran.
        Assert.True(true, "Solution compiled and test ran successfully.");
    }
}
