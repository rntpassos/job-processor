using JobProcessor.Common.Messages;
using JobProcessor.Infrastructure.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace JobProcessor.Worker.Consumers;

public class ImportCsvConsumer : IConsumer<EnqueueJobCommand>
{
    private readonly ILogger<ImportCsvConsumer> _logger;
    private readonly IJobRepository _repository;

    public ImportCsvConsumer(ILogger<ImportCsvConsumer> logger, IJobRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<EnqueueJobCommand> context)
    {
        var cmd = context.Message;
        _logger.LogInformation("Received job {JobId} of type {Type}", cmd.JobId, cmd.Type);

        // Idempotency check: skip already-processed jobs
        if (await _repository.IsProcessedAsync(cmd.JobId, context.CancellationToken))
        {
            _logger.LogInformation("Job {JobId} already processed — skipping", cmd.JobId);
            return;
        }

        try
        {
            await _repository.MarkProcessingAsync(cmd.JobId, context.CancellationToken);

            // TODO: dispatch to the appropriate handler based on cmd.Type
            // e.g. "ImportCsv" -> parse payload, read file, insert rows to DB
            await Task.Delay(100, context.CancellationToken); // placeholder work

            await _repository.MarkSucceededAsync(cmd.JobId, context.CancellationToken);

            await context.Publish(new JobCompleted(cmd.JobId), context.CancellationToken);
            _logger.LogInformation("Job {JobId} succeeded", cmd.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", cmd.JobId);
            await _repository.IncrementAttemptsAndMaybeFailAsync(
                cmd.JobId, ex.Message, context.CancellationToken);
            throw; // Let MassTransit retry policy kick in
        }
    }
}
