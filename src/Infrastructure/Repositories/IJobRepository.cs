using JobProcessor.Infrastructure.Entities;

namespace JobProcessor.Infrastructure.Repositories;

public interface IJobRepository
{
    Task<JobEntity> AddJobAsync(JobEntity job, CancellationToken ct = default);
    Task<JobEntity?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsProcessedAsync(Guid id, CancellationToken ct = default);
    Task MarkProcessingAsync(Guid id, CancellationToken ct = default);
    Task MarkSucceededAsync(Guid id, CancellationToken ct = default);
    Task IncrementAttemptsAndMaybeFailAsync(Guid id, string error, CancellationToken ct = default);
}
