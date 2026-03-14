namespace JobProcessor.Common.Messages;

public record EnqueueJobCommand(Guid JobId, string Type, string? Payload);
