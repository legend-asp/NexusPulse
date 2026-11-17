namespace NexusPulse.Application.Common.Models;

public record IncidentDetectedEvent(
    Guid IncidentId,
    Guid NodeId,
    string NodeName,
    string TargetUrl,
    string ErrorMessage,
    string FailureType,
    int? StatusCode,
    DateTime OccurredAt
);
