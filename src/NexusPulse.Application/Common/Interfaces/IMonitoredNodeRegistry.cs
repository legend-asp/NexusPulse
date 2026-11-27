namespace NexusPulse.Application.Common.Interfaces;

using NexusPulse.Application.Common.Models;
using NexusPulse.Domain.Entities;

public interface IMonitoredNodeRegistry
{
    bool HasConfiguredNodes { get; }
    int DefaultFailureThreshold { get; }
    int DefaultAlertCooldownMinutes { get; }
    IReadOnlyList<MonitoredNode> GetNodes();
    MonitoringNodeOptions? GetNodeOptions(Guid nodeId);
    string GetDisplayUrl(Guid nodeId, string fallbackUrl);
    (string Name, string TargetUrl, string DisplayUrl) GetNodeInfo(Guid nodeId);
    void RecordSuccess(Guid nodeId, int latencyMs);
    void RecordFailure(Guid nodeId, string error, bool isCritical);
    int GetLastLatency(Guid nodeId, int defaultLatency = 24);
    int IncrementFailureCount(Guid nodeId);
    void ResetFailureCount(Guid nodeId);
    bool CanSendAlertNow(Guid nodeId, int cooldownMinutes, DateTime? utcNow = null);
    void MarkAlertSent(Guid nodeId, DateTime? utcNow = null);
    void MarkIncidentStarted(Guid nodeId, DateTime? utcNow = null);
    TimeSpan GetAndClearDowntime(Guid nodeId, DateTime? utcNow = null);
}
