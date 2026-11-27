namespace NexusPulse.Application.Common.Interfaces;

using NexusPulse.Application.Common.Models;
using NexusPulse.Domain.Entities;

public interface IMonitoredNodeRegistry
{
    bool HasConfiguredNodes { get; }
    IReadOnlyList<MonitoredNode> GetNodes();
    MonitoringNodeOptions? GetNodeOptions(Guid nodeId);
    string GetDisplayUrl(Guid nodeId, string fallbackUrl);
    (string Name, string TargetUrl, string DisplayUrl) GetNodeInfo(Guid nodeId);
    void RecordSuccess(Guid nodeId, int latencyMs);
    void RecordFailure(Guid nodeId, string error, bool isCritical);
    int GetLastLatency(Guid nodeId, int defaultLatency = 24);
}
