namespace NexusPulse.Application.Common.Interfaces;

public interface ICacheService
{
    Task<bool> TryAcquireIncidentLockAsync(Guid nodeId, Guid incidentId, TimeSpan ttl);
    Task ReleaseIncidentLockAsync(Guid nodeId);
    Task<bool> HasActiveIncidentLockAsync(Guid nodeId);
    Task SetNodeMetricSnapshotAsync(Guid nodeId, int responseTimeMs, int statusCode, bool isSuccess);
}
