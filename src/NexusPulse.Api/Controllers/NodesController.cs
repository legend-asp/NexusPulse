namespace NexusPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;
using NexusPulse.Domain.Entities;
using NexusPulse.Domain.Enums;

[ApiController]
[Route("api/[controller]")]
public class NodesController : ControllerBase
{
    private readonly IAppDbContext _dbContext;
    private readonly IIncidentChannel _incidentChannel;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly IMetricsBroadcaster _broadcaster;
    private readonly IMonitoredNodeRegistry _nodeRegistry;
    private readonly ICacheService _cacheService;

    public NodesController(
        IAppDbContext dbContext,
        IIncidentChannel incidentChannel,
        ITelegramNotifier telegramNotifier,
        IMetricsBroadcaster broadcaster,
        IMonitoredNodeRegistry nodeRegistry,
        ICacheService cacheService)
    {
        _dbContext = dbContext;
        _incidentChannel = incidentChannel;
        _telegramNotifier = telegramNotifier;
        _broadcaster = broadcaster;
        _nodeRegistry = nodeRegistry;
        _cacheService = cacheService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllNodes(CancellationToken ct)
    {
        if (_nodeRegistry.HasConfiguredNodes)
        {
            var configured = _nodeRegistry.GetNodes().Select(n => new
            {
                n.Id,
                n.Name,
                TargetUrl = _nodeRegistry.GetDisplayUrl(n.Id, n.TargetUrl),
                n.CheckIntervalSeconds,
                n.TimeoutSeconds,
                HealthStatus = n.HealthStatus.ToString(),
                LastCheckedAt = n.LastCheckedAt ?? DateTime.UtcNow,
                n.IsActive,
                LastLatencyMs = _nodeRegistry.GetLastLatency(n.Id)
            });

            return Ok(configured);
        }

        try
        {
            var dbNodes = await _dbContext.Nodes
                .AsNoTracking()
                .OrderBy(n => n.Name)
                .ToListAsync(ct);

            var nodes = dbNodes.Select(n => new
            {
                n.Id,
                n.Name,
                n.TargetUrl,
                n.CheckIntervalSeconds,
                n.TimeoutSeconds,
                HealthStatus = n.HealthStatus.ToString(),
                n.LastCheckedAt,
                n.IsActive,
                LastLatencyMs = _nodeRegistry.GetLastLatency(n.Id)
            }).ToList();

            if (nodes.Count > 0)
                return Ok(nodes);
        }
        catch { }

        return Ok(_nodeRegistry.GetNodes().Select(n => new
        {
            n.Id,
            n.Name,
            TargetUrl = _nodeRegistry.GetDisplayUrl(n.Id, n.TargetUrl),
            n.CheckIntervalSeconds,
            n.TimeoutSeconds,
            HealthStatus = n.HealthStatus.ToString(),
            LastCheckedAt = n.LastCheckedAt ?? DateTime.UtcNow,
            n.IsActive,
            LastLatencyMs = _nodeRegistry.GetLastLatency(n.Id)
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNodeById(Guid id, CancellationToken ct)
    {
        if (!_nodeRegistry.HasConfiguredNodes)
        {
            try
            {
                var dbNode = await _dbContext.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
                if (dbNode != null) return Ok(dbNode);
            }
            catch { }
        }

        var fallback = _nodeRegistry.GetNodes().FirstOrDefault(n => n.Id == id);
        if (fallback == null) return NotFound();

        return Ok(new
        {
            fallback.Id,
            fallback.Name,
            TargetUrl = _nodeRegistry.GetDisplayUrl(fallback.Id, fallback.TargetUrl),
            fallback.CheckIntervalSeconds,
            fallback.TimeoutSeconds,
            HealthStatus = fallback.HealthStatus.ToString(),
            fallback.LastCheckedAt,
            fallback.IsActive,
            LastLatencyMs = _nodeRegistry.GetLastLatency(fallback.Id)
        });
    }

    public record CreateNodeRequest(string Name, string TargetUrl, int CheckIntervalSeconds = 30, int TimeoutSeconds = 5);

    [HttpPost]
    public async Task<IActionResult> CreateNode([FromBody] CreateNodeRequest request, CancellationToken ct)
    {
        try
        {
            var node = new MonitoredNode(request.Name, request.TargetUrl, request.CheckIntervalSeconds, request.TimeoutSeconds);
            _dbContext.Nodes.Add(node);
            await _dbContext.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(GetNodeById), new { id = node.Id }, node);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNode(Guid id, CancellationToken ct)
    {
        try
        {
            var node = await _dbContext.Nodes.FindAsync(new object[] { id }, ct);
            if (node == null) return NotFound();

            _dbContext.Nodes.Remove(node);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch { }

        return NoContent();
    }

    public record SimulateFailureRequest(string? ErrorMessage, string? FailureType, int? StatusCode);

    [HttpPost("{id:guid}/simulate-failure")]
    public async Task<IActionResult> SimulateFailure(Guid id, [FromBody] SimulateFailureRequest? req, CancellationToken ct)
    {
        var (nodeName, _, displayUrl) = _nodeRegistry.GetNodeInfo(id);
        var incidentId = Guid.NewGuid();
        var error = req?.ErrorMessage ?? "HTTP 504 Gateway Timeout: Upstream dependency connection aborted after 30000ms";
        var failureType = req?.FailureType ?? "HttpTimeout";
        var statusCode = req?.StatusCode ?? 504;

        await _cacheService.SetSimulationLockAsync(id, TimeSpan.FromHours(1));
        await _cacheService.TryAcquireIncidentLockAsync(id, incidentId, TimeSpan.FromHours(1));
        _nodeRegistry.RecordFailure(id, error, isCritical: true);

        try
        {
            var node = await _dbContext.Nodes.FindAsync(new object[] { id }, ct);
            if (node != null)
            {
                nodeName = node.Name;
                node.RecordFailure(error, isCritical: true);
                var incident = new Incident(node.Id, IncidentSeverity.Critical, error, failureType);
                incidentId = incident.Id;
                _dbContext.Incidents.Add(incident);
                await _dbContext.SaveChangesAsync(ct);
            }
        }
        catch { }

        await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
            incidentId,
            id,
            nodeName,
            displayUrl,
            error,
            failureType,
            statusCode,
            DateTime.UtcNow
        ), ct);

        return Ok(new
        {
            message = "Failure simulated and event published to channel.",
            incidentId,
            nodeId = id,
            nodeName
        });
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreNode(Guid id, CancellationToken ct)
    {
        var (nodeName, _, displayUrl) = _nodeRegistry.GetNodeInfo(id);

        await _cacheService.ReleaseSimulationLockAsync(id);
        await _cacheService.ReleaseIncidentLockAsync(id);
        _nodeRegistry.RecordSuccess(id, 24);

        try
        {
            var node = await _dbContext.Nodes.FindAsync(new object[] { id }, ct);
            if (node != null)
            {
                nodeName = node.Name;
                node.RecordSuccess(24);
                await _dbContext.SaveChangesAsync(ct);
            }
        }
        catch { }

        try
        {
            await _broadcaster.BroadcastRecoveryAsync(id, nodeName);
        }
        catch { }

        try
        {
            await _telegramNotifier.SendRecoveryAlertAsync(nodeName, displayUrl, TimeSpan.Zero);
        }
        catch { }

        return Ok(new { message = "Node restored and recovery notification dispatched.", nodeId = id, nodeName });
    }
}
