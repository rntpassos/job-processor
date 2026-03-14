namespace Common.Messages;

public record EnqueueJobCommand(Guid JobId, string JobType, string Payload);
