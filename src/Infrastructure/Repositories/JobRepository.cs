using Infrastructure.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public interface IJobRepository
{
    Task<JobEntity?> GetByIdAsync(Guid id);
    Task AddAsync(JobEntity job);
    Task MarkProcessingAsync(Guid id);
    Task MarkSucceededAsync(Guid id);
    Task MarkFailedAsync(Guid id, string error);
    Task<bool> IsProcessedAsync(Guid id);
    Task SaveChangesAsync();
}

public class JobRepository : IJobRepository
{
    private readonly JobDbContext _context;

    public JobRepository(JobDbContext context)
    {
        _context = context;
    }

    public async Task<JobEntity?> GetByIdAsync(Guid id) =>
        await _context.Jobs.FindAsync(id);

    public async Task AddAsync(JobEntity job) =>
        await _context.Jobs.AddAsync(job);

    public async Task MarkProcessingAsync(Guid id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job is null) return;
        job.Status = "Processing";
        job.Attempts++;
    }

    public async Task MarkSucceededAsync(Guid id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job is null) return;
        job.Status = "Succeeded";
        job.ProcessedAt = DateTime.UtcNow;
    }

    public async Task MarkFailedAsync(Guid id, string error)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job is null) return;
        job.Status = "Failed";
        job.LastError = error;
        job.ProcessedAt = DateTime.UtcNow;
    }

    public async Task<bool> IsProcessedAsync(Guid id)
    {
        var job = await _context.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id);
        return job?.Status is "Succeeded" or "Processing";
    }

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}
