namespace NexusPulse.Application.Common.Interfaces;

using NexusPulse.Application.Common.Models;

public interface IMetricsBroadcaster
{
    Task BroadcastMetricUpdateAsync(Guid nodeId, int responseTimeMs, int statusCode, bool isSuccess);
    Task BroadcastIncidentAsync(IncidentDetectedEvent incidentEvent, AiDiagnosis? diagnosis);
    Task BroadcastRecoveryAsync(Guid nodeId, string nodeName);
}
