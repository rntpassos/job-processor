using JobProcessor.Infrastructure.Entities;
using JobProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobProcessor.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly JobDbContext _db;

    public JobRepository(JobDbContext db)
    {
        _db = db;
    }

    public async Task<JobEntity> AddJobAsync(JobEntity job, CancellationToken ct = default)
    {
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return job;
    }

    public Task<JobEntity?> GetJobAsync(Guid id, CancellationToken ct = default)
        => _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<bool> IsProcessedAsync(Guid id, CancellationToken ct = default)
    {
        var status = await _db.Jobs
            .Where(j => j.Id == id)
            .Select(j => j.Status)
            .FirstOrDefaultAsync(ct);
        return status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.DeadLetter;
    }

    public async Task MarkProcessingAsync(Guid id, CancellationToken ct = default)
    {
        var job = await RequireJobAsync(id, ct);
        job.Status = JobStatus.Processing;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkSucceededAsync(Guid id, CancellationToken ct = default)
    {
        var job = await RequireJobAsync(id, ct);
        job.Status = JobStatus.Succeeded;
        job.ProcessedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task IncrementAttemptsAndMaybeFailAsync(Guid id, string error, CancellationToken ct = default)
    {
        var job = await RequireJobAsync(id, ct);
        job.Attempts++;
        job.LastError = error;

        if (job.Attempts >= job.MaxAttempts)
        {
            job.Status = JobStatus.DeadLetter;
            job.ProcessedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            // Exponential backoff: 2^attempt seconds
            var delay = TimeSpan.FromSeconds(Math.Pow(2, job.Attempts));
            job.NextAttemptAt = DateTimeOffset.UtcNow.Add(delay);
            job.Status = JobStatus.Queued;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<JobEntity> RequireJobAsync(Guid id, CancellationToken ct)
    {
        var job = await _db.Jobs.FindAsync([id], ct);
        return job ?? throw new InvalidOperationException($"Job {id} not found.");
    }
}
