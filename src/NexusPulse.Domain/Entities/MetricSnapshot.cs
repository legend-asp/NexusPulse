namespace NexusPulse.Domain.Entities;

using NexusPulse.Domain.Common;

public class MetricSnapshot : BaseEntity
{
    public Guid NodeId { get; private set; }
    public int ResponseTimeMs { get; private set; }
    public int StatusCode { get; private set; }
    public bool IsSuccess { get; private set; }
    public DateTime Timestamp { get; private set; }

    private MetricSnapshot() { }

    public MetricSnapshot(Guid nodeId, int responseTimeMs, int statusCode, bool isSuccess)
    {
        NodeId = nodeId;
        ResponseTimeMs = responseTimeMs;
        StatusCode = statusCode;
        IsSuccess = isSuccess;
        Timestamp = DateTime.UtcNow;
    }
}
