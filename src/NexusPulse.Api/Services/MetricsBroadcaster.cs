namespace NexusPulse.Api.Services;

using Microsoft.AspNetCore.SignalR;
using NexusPulse.Api.Hubs;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;

public class MetricsBroadcaster : IMetricsBroadcaster
{
    private readonly IHubContext<MetricsHub> _hubContext;
    private readonly ILogger<MetricsBroadcaster> _logger;

    public MetricsBroadcaster(IHubContext<MetricsHub> hubContext, ILogger<MetricsBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastMetricUpdateAsync(Guid nodeId, int responseTimeMs, int statusCode, bool isSuccess)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MetricUpdated", new
            {
                nodeId = nodeId.ToString(),
                responseTimeMs,
                statusCode,
                isSuccess,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast metric update via SignalR");
        }
    }

    public async Task BroadcastIncidentAsync(IncidentDetectedEvent incidentEvent, AiDiagnosis? diagnosis)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("IncidentAlert", new
            {
                incident = new
                {
                    incidentId = incidentEvent.IncidentId.ToString(),
                    nodeId = incidentEvent.NodeId.ToString(),
                    nodeName = incidentEvent.NodeName,
                    targetUrl = incidentEvent.TargetUrl,
                    errorMessage = incidentEvent.ErrorMessage,
                    failureType = incidentEvent.FailureType,
                    statusCode = incidentEvent.StatusCode,
                    occurredAt = incidentEvent.OccurredAt.ToString("o")
                },
                aiDiagnosis = new
                {
                    rootCause = diagnosis?.RootCause ?? $"Service '{incidentEvent.NodeName}' timed out after 30000ms.",
                    immediateFix = diagnosis?.ImmediateFix ?? "Restart or scale backend instances and cycle connection pools.",
                    prevention = diagnosis?.Prevention ?? "Configure circuit breaker with Polly v8 and configure bulkheads."
                },
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast incident alert via SignalR");
        }
    }

    public async Task BroadcastRecoveryAsync(Guid nodeId, string nodeName)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("NodeRecovered", new
            {
                nodeId = nodeId.ToString(),
                nodeName,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast recovery via SignalR");
        }
    }
}
