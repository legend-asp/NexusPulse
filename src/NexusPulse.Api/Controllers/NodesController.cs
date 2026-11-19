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

    public NodesController(IAppDbContext dbContext, IIncidentChannel incidentChannel)
    {
        _dbContext = dbContext;
        _incidentChannel = incidentChannel;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllNodes(CancellationToken ct)
    {
        var nodes = await _dbContext.Nodes
            .AsNoTracking()
            .OrderBy(n => n.Name)
            .ToListAsync(ct);
        return Ok(nodes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNodeById(Guid id, CancellationToken ct)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, ct);

        if (node == null) return NotFound();
        return Ok(node);
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
        var node = await _dbContext.Nodes.FindAsync(new object[] { id }, ct);
        if (node == null) return NotFound();

        _dbContext.Nodes.Remove(node);
        await _dbContext.SaveChangesAsync(ct);
        return NoContent();
    }

    public record SimulateFailureRequest(string? ErrorMessage, string? FailureType, int? StatusCode);

    [HttpPost("{id:guid}/simulate-failure")]
    public async Task<IActionResult> SimulateFailure(Guid id, [FromBody] SimulateFailureRequest? req, CancellationToken ct)
    {
        var node = await _dbContext.Nodes.FindAsync(new object[] { id }, ct);
        if (node == null) return NotFound();

        var error = req?.ErrorMessage ?? "Simulated HTTP 504 Gateway Timeout: upstream service did not respond";
        var failureType = req?.FailureType ?? "HttpTimeout";
        var statusCode = req?.StatusCode ?? 504;

        node.RecordFailure(error, isCritical: true);
        var incident = new Incident(node.Id, IncidentSeverity.Critical, error, failureType);
        _dbContext.Incidents.Add(incident);
        await _dbContext.SaveChangesAsync(ct);

        await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
            incident.Id,
            node.Id,
            node.Name,
            node.TargetUrl,
            error,
            failureType,
            statusCode,
            DateTime.UtcNow
        ), ct);

        return Ok(new { message = "Failure simulated and event published to channel.", incidentId = incident.Id });
    }
}
