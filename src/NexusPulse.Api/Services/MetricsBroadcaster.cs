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
                NodeId = nodeId,
                ResponseTimeMs = responseTimeMs,
                StatusCode = statusCode,
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow
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
                Incident = incidentEvent,
                AiDiagnosis = diagnosis,
                Timestamp = DateTime.UtcNow
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
                NodeId = nodeId,
                NodeName = nodeName,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast recovery via SignalR");
        }
    }
}
