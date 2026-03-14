using Common.Messages;
using Infrastructure.Repositories;
using MassTransit;

namespace Worker.Consumers;

public class ImportCsvConsumer : IConsumer<EnqueueJobCommand>
{
    private readonly IJobRepository _repository;
    private readonly ILogger<ImportCsvConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public ImportCsvConsumer(IJobRepository repository, ILogger<ImportCsvConsumer> logger, IPublishEndpoint publishEndpoint)
    {
        _repository = repository;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<EnqueueJobCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Received job {JobId} of type {JobType}", command.JobId, command.JobType);

        // Idempotency check
        if (await _repository.IsProcessedAsync(command.JobId))
        {
            _logger.LogWarning("Job {JobId} already processed, skipping", command.JobId);
            return;
        }

        await _repository.MarkProcessingAsync(command.JobId);
        await _repository.SaveChangesAsync();

        try
        {
            // TODO: Implement actual job processing logic based on command.JobType
            await Task.Delay(100, context.CancellationToken);

            await _repository.MarkSucceededAsync(command.JobId);
            await _repository.SaveChangesAsync();

            await _publishEndpoint.Publish(new JobCompleted(command.JobId, "Succeeded"), context.CancellationToken);
            _logger.LogInformation("Job {JobId} completed successfully", command.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", command.JobId);
            await _repository.MarkFailedAsync(command.JobId, ex.Message);
            await _repository.SaveChangesAsync();

            await _publishEndpoint.Publish(new JobCompleted(command.JobId, "Failed"), context.CancellationToken);
            // Do not re-throw; failure state is persisted and event published.
        }
    }
}
