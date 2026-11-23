namespace NexusPulse.Domain.Entities;

using NexusPulse.Domain.Common;
using NexusPulse.Domain.Enums;

public class MonitoredNode : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string TargetUrl { get; private set; } = null!;
    public int CheckIntervalSeconds { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public NodeStatus HealthStatus { get; private set; }
    public DateTime? LastCheckedAt { get; private set; }
    public bool IsActive { get; private set; }

    private MonitoredNode() { }

    public MonitoredNode(Guid id, string name, string targetUrl, int checkIntervalSeconds = 30, int timeoutSeconds = 5)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Node name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Valid absolute URL is required.", nameof(targetUrl));

        if (checkIntervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(checkIntervalSeconds), "Interval must be positive.");

        if (timeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout must be positive.");

        Name = name.Trim();
        TargetUrl = targetUrl.Trim();
        CheckIntervalSeconds = checkIntervalSeconds;
        TimeoutSeconds = timeoutSeconds;
        HealthStatus = NodeStatus.Healthy;
        IsActive = true;
    }

    public MonitoredNode(string name, string targetUrl, int checkIntervalSeconds = 30, int timeoutSeconds = 5)
        : this(Guid.NewGuid(), name, targetUrl, checkIntervalSeconds, timeoutSeconds)
    {
    }

    public void RecordSuccess(int responseTimeMs)
    {
        LastCheckedAt = DateTime.UtcNow;
        HealthStatus = NodeStatus.Healthy;
    }

    public void RecordFailure(string error, bool isCritical = false)
    {
        LastCheckedAt = DateTime.UtcNow;
        HealthStatus = isCritical ? NodeStatus.Down : NodeStatus.Degraded;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
