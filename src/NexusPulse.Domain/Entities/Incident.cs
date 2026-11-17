namespace NexusPulse.Domain.Entities;

using NexusPulse.Domain.Common;
using NexusPulse.Domain.Enums;

public class Incident : BaseEntity
{
    public Guid NodeId { get; private set; }
    public IncidentSeverity Severity { get; private set; }
    public string ErrorMessage { get; private set; } = null!;
    public string FailureType { get; private set; } = null!;
    public string? AiRootCause { get; private set; }
    public string? AiImmediateFix { get; private set; }
    public string? AiLongTermPrevention { get; private set; }
    public IncidentStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private Incident() { }

    public Incident(Guid nodeId, IncidentSeverity severity, string errorMessage, string failureType)
    {
        NodeId = nodeId;
        Severity = severity;
        ErrorMessage = errorMessage;
        FailureType = failureType;
        Status = IncidentStatus.Active;
        StartedAt = DateTime.UtcNow;
    }

    public void AttachAiDiagnosis(string rootCause, string immediateFix, string longTermPrevention)
    {
        AiRootCause = rootCause;
        AiImmediateFix = immediateFix;
        AiLongTermPrevention = longTermPrevention;
    }

    public void Resolve()
    {
        Status = IncidentStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }
}
