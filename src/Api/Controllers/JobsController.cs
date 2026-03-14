using Common.Messages;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobRepository _repository;
    private readonly IBus _bus;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobRepository repository, IBus bus, ILogger<JobsController> logger)
    {
        _repository = repository;
        _bus = bus;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Type = request.JobType,
            Payload = request.Payload,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = DateTime.UtcNow,
            MaxAttempts = 3
        };

        await _repository.AddAsync(job);
        await _repository.SaveChangesAsync();

        await _bus.Publish(new EnqueueJobCommand(job.Id, job.Type, job.Payload), ct);

        _logger.LogInformation("Job {JobId} created and enqueued", job.Id);

        return AcceptedAtAction(nameof(GetJob), new { id = job.Id }, new { jobId = job.Id });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await _repository.GetByIdAsync(id);
        if (job is null) return NotFound();

        return Ok(new
        {
            job.Id,
            job.Type,
            job.Status,
            job.Attempts,
            job.CreatedAt,
            job.ProcessedAt,
            job.LastError
        });
    }
}

public record CreateJobRequest(string JobType, string Payload);
