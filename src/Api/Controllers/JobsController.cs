using JobProcessor.Common.Messages;
using JobProcessor.Infrastructure.Entities;
using JobProcessor.Infrastructure.Repositories;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace JobProcessor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;

    public JobsController(IJobRepository repository, IPublishEndpoint publishEndpoint)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>Creates a new background job.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Payload = request.Payload,
            Status = JobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            ScheduledAt = DateTimeOffset.UtcNow
        };

        await _repository.AddJobAsync(job, ct);

        await _publishEndpoint.Publish(
            new EnqueueJobCommand(job.Id, job.Type, job.Payload), ct);

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, new JobResponse(job));
    }

    /// <summary>Gets the status of a job.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken ct)
    {
        var job = await _repository.GetJobAsync(id, ct);
        if (job is null) return NotFound();
        return Ok(new JobResponse(job));
    }
}

public record CreateJobRequest(string Type, string? Payload);

public record JobResponse(
    Guid Id,
    string Type,
    string Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    string? LastError)
{
    public JobResponse(JobEntity e)
        : this(e.Id, e.Type, e.Status, e.Attempts, e.CreatedAt, e.ProcessedAt, e.LastError) { }
}
